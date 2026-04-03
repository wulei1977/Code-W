namespace CodeW.UI;

using System.Text;
using CodeW.Models;
using CodeW.Services;
using Microsoft.VisualStudio.Extensibility;

internal sealed partial class CodeWToolWindowData
{
    private async Task SendPromptAsync(object? parameter, IClientContext clientContext, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);

        if (IsBusy)
        {
            StatusMessage = "当前已有请求在运行，请先等待结束或点击“停止”。";
            return;
        }

        CommitAllEditors();

        string prompt = DraftPrompt.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            StatusMessage = "请输入内容后再发送。";
            return;
        }

        CloseOverlayPanels();
        DraftPrompt = string.Empty;
        AddTurn("user", "你", prompt);
        ConversationTurnViewModel assistantTurn = AddTurn("assistant", "Code-W", string.Empty);

        string? selectedPath = await TryGetSelectedPathAsync(clientContext, cancellationToken);
        string? workingDirectory = ResolveWorkingDirectory(selectedPath);

        ContextSummary = string.IsNullOrWhiteSpace(selectedPath)
            ? "本次请求没有拿到明确的 IDE 选中路径。"
            : $"本次请求的 IDE 上下文：{selectedPath}";

        IsBusy = true;

        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        activeRequestSource = linkedSource;

        try
        {
            await SaveCurrentConfigurationAsync(linkedSource.Token, $"正在请求 {SelectedProviderName}...");

            await foreach (ConversationStreamUpdate update in conversationService.StreamAsync(
                               new ConversationRequest
                               {
                                   Configuration = configuration,
                                   Mode = configuration.DefaultMode,
                                   Prompt = prompt,
                                   SelectedPath = selectedPath,
                                   WorkingDirectory = workingDirectory,
                                   History = Transcript
                                       .Where(item => !ReferenceEquals(item, assistantTurn))
                                       .Where(static item => item.Role is "user" or "assistant")
                                       .Where(static item => !string.IsNullOrWhiteSpace(item.Content))
                                       .Select(static item => new ConversationTurnRecord
                                       {
                                           Role = item.Role,
                                           Content = item.Content,
                                           CreatedAt = item.CreatedAt,
                                       })
                                       .ToList(),
                               },
                               linkedSource.Token))
            {
                switch (update)
                {
                    case ConversationStatusUpdate status:
                        StatusMessage = status.Message;
                        break;
                    case ConversationAssistantDeltaUpdate assistantDelta:
                        assistantTurn.AppendContent(assistantDelta.Text);
                        break;
                    case ConversationToolUpdate tool:
                        AddTurn(
                            "tool",
                            tool.ServerName,
                            $"[{tool.ServerName} / {tool.ToolName}] {tool.Phase}\n{tool.Details}");
                        break;
                    case ConversationCompletedUpdate completed:
                        StatusMessage = completed.StatusMessage;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(assistantTurn.Content))
            {
                assistantTurn.Content = "本轮没有产生可展示的文本输出。";
            }
        }
        catch (OperationCanceledException) when (linkedSource.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(assistantTurn.Content))
            {
                assistantTurn.Content = "本次请求已取消。";
            }

            StatusMessage = "请求已取消";
        }
        catch (Exception exception)
        {
            if (string.IsNullOrWhiteSpace(assistantTurn.Content))
            {
                assistantTurn.Content = $"请求执行失败：{exception.Message}";
            }
            else
            {
                AddTurn("tool", "系统", $"请求执行失败：{exception.Message}");
            }

            StatusMessage = "请求失败";
        }
        finally
        {
            activeRequestSource = null;
            IsBusy = false;
        }
    }

    private async Task TestMcpServerAsync(object? parameter, IClientContext clientContext, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);

        if (IsBusy)
        {
            StatusMessage = "请先等待当前对话请求结束，再测试 MCP 连接。";
            return;
        }

        CommitSelectedMcpServer();

        if (selectedMcpServer is null)
        {
            StatusMessage = "当前没有可测试的 MCP Server。";
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedMcpServer.Command))
        {
            StatusMessage = "当前 MCP Server 还没有填写启动命令。";
            return;
        }

        string? workingDirectory = await ResolveWorkingDirectoryFromClientContextAsync(clientContext, cancellationToken);
        StatusMessage = $"正在测试 MCP Server：{selectedMcpServer.Name}";

        try
        {
            await using IMcpConversationContext context = await mcpService.CreateContextAsync([selectedMcpServer], workingDirectory, cancellationToken);

            AddTurn("tool", "MCP 测试", BuildMcpProbeReport(selectedMcpServer, context));
            StatusMessage = context.Tools.Count > 0
                ? $"MCP 测试完成，发现 {context.Tools.Count} 个工具。"
                : context.Warnings.Count > 0
                    ? "MCP 测试完成，但没有成功发现工具。"
                    : "MCP 测试完成，当前服务端没有暴露工具。";
        }
        catch (Exception exception)
        {
            AddTurn("tool", "MCP 测试", $"测试失败：{exception.Message}");
            StatusMessage = "MCP 测试失败";
        }
    }

    private async Task ScanSkillsAsync(object? parameter, IClientContext clientContext, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);

        if (IsBusy)
        {
            StatusMessage = "请先等待当前对话请求结束，再扫描 Skills。";
            return;
        }

        CommitSelectedSkill();

        string? workingDirectory = await ResolveWorkingDirectoryFromClientContextAsync(clientContext, cancellationToken);
        StatusMessage = "正在扫描 skills 目录...";

        SkillDiscoveryResult discoveryResult = await skillService.DiscoverSkillsAsync(workingDirectory, cancellationToken);

        int addedCount = 0;
        int existingCount = 0;
        SkillDefinition? firstAddedSkill = null;

        foreach (SkillDefinition discoveredSkill in discoveryResult.Skills)
        {
            string normalizedEntryPoint = NormalizePath(discoveredSkill.EntryPoint);
            SkillDefinition? existingSkill = configuration.Skills.FirstOrDefault(skill =>
                string.Equals(NormalizePath(skill.EntryPoint), normalizedEntryPoint, StringComparison.OrdinalIgnoreCase));

            if (existingSkill is not null)
            {
                existingCount++;
                if (string.IsNullOrWhiteSpace(existingSkill.Description) && !string.IsNullOrWhiteSpace(discoveredSkill.Description))
                {
                    existingSkill.Description = discoveredSkill.Description;
                }

                continue;
            }

            SkillDefinition skill = new()
            {
                Name = BuildUniqueName(discoveredSkill.Name, configuration.Skills.Select(static item => item.Name)),
                Description = discoveredSkill.Description,
                EntryPoint = discoveredSkill.EntryPoint,
                Enabled = true,
            };

            configuration.Skills.Add(skill);
            firstAddedSkill ??= skill;
            addedCount++;
        }

        selectedSkill = firstAddedSkill ?? selectedSkill ?? configuration.Skills.FirstOrDefault();
        await SaveInternalAsync(cancellationToken, addedCount > 0
            ? $"Skill 扫描完成，新增 {addedCount} 个条目。"
            : "Skill 扫描完成，没有新增条目。");

        AddTurn("tool", "Skill 扫描", BuildSkillScanReport(discoveryResult, addedCount, existingCount));
    }

    private void ResetTranscript()
    {
        Transcript.Clear();
    }

    private void SetMode(ConversationMode mode)
    {
        configuration.DefaultMode = mode;
        switch (mode)
        {
            case ConversationMode.Agent:
                ModeDisplayName = "Agent";
                ModeDescription = "偏向规划、拆解、调用 MCP / Skill 的执行式工作流，支持真实工具调用与结果回灌。";
                break;
            default:
                ModeDisplayName = "Chat";
                ModeDescription = "偏向即时问答、代码解释、设计讨论与非侵入式建议，默认使用流式输出。";
                break;
        }
    }

    private ConversationTurnViewModel AddTurn(string role, string label, string content)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        ConversationTurnViewModel turn = new()
        {
            Role = role,
            RoleLabel = label,
            Content = content,
            CreatedAt = now,
            Timestamp = now.ToString("HH:mm:ss"),
        };

        Transcript.Add(turn);
        return turn;
    }

    private static async Task<string?> TryGetSelectedPathAsync(IClientContext clientContext, CancellationToken cancellationToken)
    {
        try
        {
            Uri? selectedUri = await clientContext.GetSelectedPathAsync(cancellationToken);
            return selectedUri?.LocalPath ?? selectedUri?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ResolveWorkingDirectory(string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return null;
        }

        if (Directory.Exists(selectedPath))
        {
            return selectedPath;
        }

        return File.Exists(selectedPath) ? Path.GetDirectoryName(selectedPath) : null;
    }

    private static async Task<string?> ResolveWorkingDirectoryFromClientContextAsync(
        IClientContext clientContext,
        CancellationToken cancellationToken)
    {
        string? selectedPath = await TryGetSelectedPathAsync(clientContext, cancellationToken);
        return ResolveWorkingDirectory(selectedPath);
    }

    private static string BuildMcpProbeReport(McpServerDefinition server, IMcpConversationContext context)
    {
        StringBuilder builder = new();
        builder.AppendLine($"服务端：{server.Name}");
        builder.AppendLine($"命令：{server.Command} {server.Arguments}".Trim());
        builder.AppendLine($"状态：{(context.Tools.Count > 0 ? "连接成功" : "已完成连接尝试")}");

        if (context.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("警告：");
            foreach (string warning in context.Warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        builder.AppendLine();
        if (context.Tools.Count > 0)
        {
            builder.AppendLine($"发现工具：{context.Tools.Count} 个");
            foreach (McpToolRegistration tool in context.Tools)
            {
                builder.AppendLine($"- {tool.OriginalName} -> {tool.ExposedName}");
            }
        }
        else
        {
            builder.AppendLine("发现工具：0 个");
        }

        if (context.ServerInstructions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("服务端补充说明：");
            foreach (string instruction in context.ServerInstructions.Take(3))
            {
                builder.AppendLine($"- {instruction}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSkillScanReport(
        SkillDiscoveryResult discoveryResult,
        int addedCount,
        int existingCount)
    {
        StringBuilder builder = new();

        if (!string.IsNullOrWhiteSpace(discoveryResult.SkillsDirectory))
        {
            builder.AppendLine($"扫描目录：{discoveryResult.SkillsDirectory}");
        }

        builder.AppendLine($"发现文件：{discoveryResult.Skills.Count}");
        builder.AppendLine($"新增条目：{addedCount}");
        builder.AppendLine($"已存在条目：{existingCount}");

        if (discoveryResult.Skills.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("发现的 Skill：");
            foreach (SkillDefinition skill in discoveryResult.Skills.Take(20))
            {
                builder.AppendLine($"- {skill.Name}: {skill.EntryPoint}");
            }
        }

        if (discoveryResult.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("警告：");
            foreach (string warning in discoveryResult.Warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
