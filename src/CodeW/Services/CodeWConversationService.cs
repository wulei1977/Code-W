namespace CodeW.Services;

using System.Runtime.CompilerServices;
using System.Text;
using CodeW.Models;

internal sealed class CodeWConversationService : ICodeWConversationService
{
    private readonly IOpenAiCompatibleChatClient chatClient;
    private readonly IMcpService mcpService;
    private readonly ISkillService skillService;

    public CodeWConversationService(
        IOpenAiCompatibleChatClient chatClient,
        IMcpService mcpService,
        ISkillService skillService)
    {
        this.chatClient = chatClient;
        this.mcpService = mcpService;
        this.skillService = skillService;
    }

    public async Task<ConversationResult> SendAsync(ConversationRequest request, CancellationToken cancellationToken)
    {
        StringBuilder messageBuilder = new();
        string statusMessage = "未开始";

        await foreach (ConversationStreamUpdate update in StreamAsync(request, cancellationToken))
        {
            switch (update)
            {
                case ConversationAssistantDeltaUpdate assistantDelta:
                    messageBuilder.Append(assistantDelta.Text);
                    break;
                case ConversationCompletedUpdate completed:
                    statusMessage = completed.StatusMessage;
                    break;
                case ConversationStatusUpdate status:
                    statusMessage = status.Message;
                    break;
            }
        }

        return new ConversationResult
        {
            Message = messageBuilder.ToString(),
            StatusMessage = statusMessage,
        };
    }

    public async IAsyncEnumerable<ConversationStreamUpdate> StreamAsync(
        ConversationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ProviderProfile? provider = request.Configuration.Providers.FirstOrDefault(profile => profile.Id == request.Configuration.ActiveProviderId)
            ?? request.Configuration.Providers.FirstOrDefault();

        if (provider is null)
        {
            yield return new ConversationAssistantDeltaUpdate("当前没有可用的模型 Provider。请先在右侧配置至少一个可用的 Provider。");
            yield return new ConversationCompletedUpdate("未找到 Provider");
            yield break;
        }

        if (!provider.Enabled)
        {
            yield return new ConversationAssistantDeltaUpdate($"当前 Provider “{provider.DisplayName}” 已被禁用，请先启用后再发送。");
            yield return new ConversationCompletedUpdate($"{provider.DisplayName} 已禁用");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            yield return new ConversationAssistantDeltaUpdate(
                $"还没有为 “{provider.DisplayName}” 配置 API Key。你可以在右侧面板填写后保存，Code-W 会用当前 Windows 用户的 DPAPI 做本地加密存储。");
            yield return new ConversationCompletedUpdate($"{provider.DisplayName} 缺少 API Key");
            yield break;
        }

        SkillLoadResult skillLoadResult = await skillService.LoadActiveSkillsAsync(
            request.Configuration.Skills,
            request.WorkingDirectory,
            cancellationToken);

        foreach (string warning in skillLoadResult.Warnings)
        {
            yield return new ConversationToolUpdate("Skill", "加载", "警告", warning);
        }

        IMcpConversationContext? mcpContext = null;

        try
        {
            IReadOnlyList<OpenAiCompatibleToolDefinition> tools = [];
            IReadOnlyList<string> mcpInstructions = [];

            if (request.Mode == ConversationMode.Agent)
            {
                IReadOnlyList<McpServerDefinition> enabledServers = request.Configuration.McpServers
                    .Where(static server => server.Enabled)
                    .ToList();

                if (enabledServers.Count > 0)
                {
                    yield return new ConversationStatusUpdate("正在连接 MCP 服务...");
                    mcpContext = await mcpService.CreateContextAsync(enabledServers, request.WorkingDirectory, cancellationToken);

                    foreach (string warning in mcpContext.Warnings)
                    {
                        yield return new ConversationToolUpdate("MCP", "连接", "警告", warning);
                    }

                    mcpInstructions = mcpContext.ServerInstructions;
                    tools = mcpContext.Tools
                        .Select(static tool => new OpenAiCompatibleToolDefinition
                        {
                            Name = tool.ExposedName,
                            Description = tool.Description,
                            Parameters = tool.InputSchema.Clone(),
                        })
                        .ToList();

                    if (tools.Count > 0)
                    {
                        yield return new ConversationStatusUpdate($"已发现 {tools.Count} 个 MCP 工具，Agent 可以开始调用。");
                    }
                    else
                    {
                        yield return new ConversationStatusUpdate("当前没有发现可用的 MCP 工具，本轮会退化为纯推理模式。");
                    }
                }
                else
                {
                    yield return new ConversationStatusUpdate("Agent 模式当前没有启用 MCP 服务，本轮将按纯推理模式运行。");
                }
            }

            List<OpenAiCompatibleMessage> messages = BuildMessages(request, mcpInstructions, skillLoadResult.Instructions);
            int maxRounds = request.Mode == ConversationMode.Agent ? 6 : 1;

            for (int round = 0; round < maxRounds; round++)
            {
                yield return new ConversationStatusUpdate(round == 0
                    ? $"正在通过 {provider.DisplayName} 请求模型..."
                    : "模型正在结合工具结果继续推理...");

                OpenAiCompatibleMessage? assistantMessage = null;

                await foreach (OpenAiCompatibleCompletionStreamUpdate update in chatClient.StreamCompletionAsync(
                                   provider,
                                   new OpenAiCompatibleChatRequest
                                   {
                                       Messages = messages,
                                       Tools = request.Mode == ConversationMode.Agent ? tools : [],
                                       Temperature = request.Mode == ConversationMode.Agent ? 0.2 : 0.5,
                                       EnableParallelToolCalls = true,
                                   },
                                   cancellationToken))
                {
                    switch (update)
                    {
                        case OpenAiCompatibleTextDeltaUpdate textDelta:
                            yield return new ConversationAssistantDeltaUpdate(textDelta.Text);
                            break;
                        case OpenAiCompatibleCompletionFinishedUpdate completed:
                            assistantMessage = completed.AssistantMessage;
                            break;
                    }
                }

                if (assistantMessage is null)
                {
                    throw new InvalidOperationException("模型流式返回已结束，但没有形成最终消息。");
                }

                bool shouldCallTools = request.Mode == ConversationMode.Agent
                    && assistantMessage.ToolCalls.Count > 0
                    && mcpContext is not null
                    && tools.Count > 0;

                messages.Add(assistantMessage);

                if (!shouldCallTools)
                {
                    yield return new ConversationCompletedUpdate(request.Mode == ConversationMode.Chat
                        ? $"已通过 {provider.DisplayName} 完成一次 Chat 请求"
                        : $"已通过 {provider.DisplayName} 完成一次 Agent 请求");
                    yield break;
                }

                IMcpConversationContext activeMcpContext = mcpContext!;

                foreach (OpenAiCompatibleToolCall toolCall in assistantMessage.ToolCalls)
                {
                    string serverName = ResolveServerName(activeMcpContext.Tools, toolCall.Name);
                    string originalToolName = ResolveOriginalToolName(activeMcpContext.Tools, toolCall.Name);

                    yield return new ConversationToolUpdate(
                        serverName,
                        originalToolName,
                        "开始",
                        toolCall.ArgumentsJson);

                    McpToolExecutionResult result = await CallToolSafelyAsync(
                        activeMcpContext,
                        toolCall,
                        serverName,
                        originalToolName,
                        cancellationToken);

                    yield return new ConversationToolUpdate(
                        result.ServerName,
                        result.ToolName,
                        result.IsError ? "失败" : "完成",
                        result.OutputText);

                    messages.Add(new OpenAiCompatibleMessage
                    {
                        Role = "tool",
                        ToolCallId = toolCall.Id,
                        Content = result.OutputText,
                    });
                }
            }

            yield return new ConversationCompletedUpdate("Agent 达到最大工具轮次限制，已停止继续调用。");
        }
        finally
        {
            if (mcpContext is not null)
            {
                await mcpContext.DisposeAsync();
            }
        }
    }

    private static List<OpenAiCompatibleMessage> BuildMessages(
        ConversationRequest request,
        IReadOnlyList<string> mcpInstructions,
        IReadOnlyList<string> skillInstructions)
    {
        List<OpenAiCompatibleMessage> messages =
        [
            new OpenAiCompatibleMessage
            {
                Role = "system",
                Content = BuildSystemPrompt(request, mcpInstructions, skillInstructions),
            },
        ];

        foreach (ConversationTurnRecord turn in request.History
                     .Where(static turn => turn.Role is "user" or "assistant")
                     .TakeLast(12))
        {
            messages.Add(new OpenAiCompatibleMessage
            {
                Role = turn.Role,
                Content = turn.Content,
            });
        }

        if (!messages.Any(message => message.Role == "user" && string.Equals(message.Content, request.Prompt, StringComparison.Ordinal)))
        {
            messages.Add(new OpenAiCompatibleMessage
            {
                Role = "user",
                Content = request.Prompt,
            });
        }

        return messages;
    }

    private static string BuildSystemPrompt(
        ConversationRequest request,
        IReadOnlyList<string> mcpInstructions,
        IReadOnlyList<string> skillInstructions)
    {
        StringBuilder prompt = new();
        prompt.AppendLine("你是 Code-W，一名运行在 Visual Studio 内部的 AI 编码助手。");
        prompt.AppendLine("请始终使用中文回答，优先给出可执行、可落地的建议。");
        prompt.AppendLine();

        if (request.Mode == ConversationMode.Agent)
        {
            prompt.AppendLine("当前模式：Agent");
            prompt.AppendLine("你可以在需要时主动调用 MCP 工具，不要假装已经读取或修改了文件。");
            prompt.AppendLine("如果工具结果不足以完成任务，请明确说明还缺少什么。");
        }
        else
        {
            prompt.AppendLine("当前模式：Chat");
            prompt.AppendLine("请直接、高密度地回答用户问题，避免无意义寒暄。");
        }

        if (!string.IsNullOrWhiteSpace(request.SelectedPath))
        {
            prompt.AppendLine();
            prompt.AppendLine($"IDE 当前选中路径：{request.SelectedPath}");
        }

        IEnumerable<McpServerDefinition> enabledMcp = request.Configuration.McpServers.Where(static server => server.Enabled);
        IEnumerable<SkillDefinition> enabledSkills = request.Configuration.Skills.Where(static skill => skill.Enabled);

        prompt.AppendLine();
        prompt.AppendLine("已启用 MCP：");
        if (enabledMcp.Any())
        {
            foreach (McpServerDefinition server in enabledMcp)
            {
                prompt.AppendLine($"- {server.Name}: {server.Description}");
            }
        }
        else
        {
            prompt.AppendLine("- 暂无，将按纯推理模式回答。");
        }

        if (mcpInstructions.Count > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("MCP 服务补充说明：");
            foreach (string instruction in mcpInstructions)
            {
                prompt.AppendLine($"- {instruction}");
            }
        }

        prompt.AppendLine();
        prompt.AppendLine("已启用 Skill：");
        if (enabledSkills.Any())
        {
            foreach (SkillDefinition skill in enabledSkills)
            {
                prompt.AppendLine($"- {skill.Name}: {skill.Description}");
            }
        }
        else
        {
            prompt.AppendLine("- 暂无。");
        }

        if (skillInstructions.Count > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("以下是本轮已加载的 Skill 指令，请优先遵守：");
            foreach (string instruction in skillInstructions)
            {
                prompt.AppendLine();
                prompt.AppendLine("[Skill Start]");
                prompt.AppendLine(instruction);
                prompt.AppendLine("[Skill End]");
            }
        }

        return prompt.ToString().Trim();
    }

    private static string ResolveServerName(
        IReadOnlyList<McpToolRegistration> tools,
        string exposedToolName)
    {
        return tools.FirstOrDefault(tool => string.Equals(tool.ExposedName, exposedToolName, StringComparison.OrdinalIgnoreCase))?.ServerName
            ?? "MCP";
    }

    private static string ResolveOriginalToolName(
        IReadOnlyList<McpToolRegistration> tools,
        string exposedToolName)
    {
        return tools.FirstOrDefault(tool => string.Equals(tool.ExposedName, exposedToolName, StringComparison.OrdinalIgnoreCase))?.OriginalName
            ?? exposedToolName;
    }

    private static async Task<McpToolExecutionResult> CallToolSafelyAsync(
        IMcpConversationContext mcpContext,
        OpenAiCompatibleToolCall toolCall,
        string serverName,
        string originalToolName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await mcpContext.CallToolAsync(toolCall.Name, toolCall.ArgumentsJson, cancellationToken);
        }
        catch (Exception exception)
        {
            return new McpToolExecutionResult
            {
                ServerName = serverName,
                ToolName = originalToolName,
                OutputText = $"工具调用失败：{exception.Message}",
                IsError = true,
            };
        }
    }
}
