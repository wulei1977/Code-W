namespace CodeW.Services;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CodeW.Models;

internal sealed class McpService : IMcpService
{
    public async Task<IMcpConversationContext> CreateContextAsync(
        IReadOnlyList<McpServerDefinition> enabledServers,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        List<McpServerSession> sessions = [];
        List<string> warnings = [];

        foreach (McpServerDefinition server in enabledServers)
        {
            McpServerSession? session = null;
            try
            {
                session = new McpServerSession(server, ResolveWorkingDirectory(workingDirectory));
                await session.StartAsync(cancellationToken);
                sessions.Add(session);
            }
            catch (Exception exception)
            {
                if (session is not null)
                {
                    await session.DisposeAsync();
                }

                warnings.Add($"MCP 服务器“{server.Name}”启动失败：{exception.Message}");
            }
        }

        return new McpConversationContext(sessions, warnings);
    }

    private static string ResolveWorkingDirectory(string? workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            return workingDirectory;
        }

        return Environment.CurrentDirectory;
    }

    private sealed class McpConversationContext : IMcpConversationContext
    {
        private readonly IReadOnlyList<McpServerSession> sessions;
        private readonly Dictionary<string, McpToolRegistration> toolByExposedName;

        public McpConversationContext(
            IReadOnlyList<McpServerSession> sessions,
            IReadOnlyList<string> warnings)
        {
            this.sessions = sessions;
            Warnings = warnings;

            List<McpToolRegistration> tools = [];
            foreach (McpServerSession session in sessions)
            {
                tools.AddRange(session.Tools);
            }

            Tools = tools;
            toolByExposedName = tools.ToDictionary(static tool => tool.ExposedName, StringComparer.OrdinalIgnoreCase);
            ServerInstructions = sessions
                .Select(static session => session.ServerInstructions)
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .Cast<string>()
                .ToList();
        }

        public IReadOnlyList<McpToolRegistration> Tools { get; }

        public IReadOnlyList<string> Warnings { get; }

        public IReadOnlyList<string> ServerInstructions { get; }

        public Task<McpToolExecutionResult> CallToolAsync(
            string exposedToolName,
            string argumentsJson,
            CancellationToken cancellationToken)
        {
            if (!toolByExposedName.TryGetValue(exposedToolName, out McpToolRegistration? tool))
            {
                throw new InvalidOperationException($"未找到名为“{exposedToolName}”的 MCP 工具。");
            }

            McpServerSession session = sessions.First(session => session.ServerName == tool.ServerName);
            return session.CallToolAsync(tool, argumentsJson, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (McpServerSession session in sessions)
            {
                await session.DisposeAsync();
            }
        }
    }

    private sealed class McpServerSession : IAsyncDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly McpServerDefinition definition;
        private readonly string workingDirectory;
        private readonly SemaphoreSlim writeLock = new(1, 1);
        private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> pendingRequests = new();
        private readonly ConcurrentQueue<string> stderrLines = new();
        private readonly CancellationTokenSource disposeSource = new();
        private readonly List<McpToolRegistration> tools = [];

        private Process? process;
        private Task? stdoutPump;
        private Task? stderrPump;
        private long nextRequestId;

        public McpServerSession(McpServerDefinition definition, string workingDirectory)
        {
            this.definition = definition;
            this.workingDirectory = workingDirectory;
        }

        public string ServerName => definition.Name;

        public string? ServerInstructions { get; private set; }

        public IReadOnlyList<McpToolRegistration> Tools => tools;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(definition.Command))
            {
                throw new InvalidOperationException($"MCP 服务器“{definition.Name}”缺少启动命令。");
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = definition.Command,
                Arguments = definition.Arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"无法启动 MCP 服务器“{definition.Name}”。");
            }

            stdoutPump = PumpStdoutAsync(process, disposeSource.Token);
            stderrPump = PumpStderrAsync(process, disposeSource.Token);

            JsonElement initializeResult = await SendRequestAsync(
                "initialize",
                new
                {
                    protocolVersion = "2025-03-26",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "Code-W",
                        version = "0.1.2",
                    },
                },
                cancellationToken);

            if (initializeResult.TryGetProperty("instructions", out JsonElement instructions) && instructions.ValueKind == JsonValueKind.String)
            {
                ServerInstructions = instructions.GetString();
            }

            await SendNotificationAsync("notifications/initialized", null, cancellationToken);
            await DiscoverToolsAsync(cancellationToken);
        }

        public async Task<McpToolExecutionResult> CallToolAsync(
            McpToolRegistration tool,
            string argumentsJson,
            CancellationToken cancellationToken)
        {
            JsonElement argumentsElement = ParseArguments(argumentsJson);
            JsonElement result = await SendRequestAsync(
                "tools/call",
                new
                {
                    name = tool.OriginalName,
                    arguments = argumentsElement,
                },
                cancellationToken);

            bool isError = result.TryGetProperty("isError", out JsonElement isErrorElement)
                && isErrorElement.ValueKind == JsonValueKind.True;

            return new McpToolExecutionResult
            {
                ServerName = tool.ServerName,
                ToolName = tool.OriginalName,
                OutputText = FormatToolResult(result),
                IsError = isError,
            };
        }

        public async ValueTask DisposeAsync()
        {
            await disposeSource.CancelAsync();

            foreach ((_, TaskCompletionSource<JsonElement> pendingRequest) in pendingRequests)
            {
                pendingRequest.TrySetCanceled();
            }

            if (process is not null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }

                process.Dispose();
                process = null;
            }

#pragma warning disable VSTHRD003 // These background pump tasks are started and owned by this session.
            if (stdoutPump is not null)
            {
                try
                {
                    await stdoutPump;
                }
                catch
                {
                }
            }

            if (stderrPump is not null)
            {
                try
                {
                    await stderrPump;
                }
                catch
                {
                }
            }
#pragma warning restore VSTHRD003

            writeLock.Dispose();
            disposeSource.Dispose();
        }

        private async Task DiscoverToolsAsync(CancellationToken cancellationToken)
        {
            tools.Clear();

            string? cursor = null;
            HashSet<string> usedNames = [];

            do
            {
                object? parameters = cursor is null ? null : new { cursor };
                JsonElement result = await SendRequestAsync("tools/list", parameters, cancellationToken);

                if (result.TryGetProperty("tools", out JsonElement toolsArray) && toolsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement tool in toolsArray.EnumerateArray())
                    {
                        string originalName = tool.GetProperty("name").GetString() ?? "tool";
                        string description = tool.TryGetProperty("description", out JsonElement descriptionElement) && descriptionElement.ValueKind == JsonValueKind.String
                            ? descriptionElement.GetString() ?? string.Empty
                            : string.Empty;

                        JsonElement inputSchema = tool.TryGetProperty("inputSchema", out JsonElement schemaElement) && schemaElement.ValueKind == JsonValueKind.Object
                            ? schemaElement.Clone()
                            : CreateDefaultSchema();

                        string exposedName = BuildUniqueToolName(definition.Name, originalName, usedNames);

                        tools.Add(new McpToolRegistration
                        {
                            ExposedName = exposedName,
                            OriginalName = originalName,
                            ServerName = definition.Name,
                            Description = string.IsNullOrWhiteSpace(description)
                                ? $"来自 MCP 服务器“{definition.Name}”的工具。"
                                : $"{description}（MCP: {definition.Name}）",
                            InputSchema = inputSchema,
                            ServerInstructions = ServerInstructions,
                        });
                    }
                }

                cursor = result.TryGetProperty("nextCursor", out JsonElement cursorElement) && cursorElement.ValueKind == JsonValueKind.String
                    ? cursorElement.GetString()
                    : null;
            }
            while (!string.IsNullOrWhiteSpace(cursor));
        }

        private async Task PumpStdoutAsync(Process runningProcess, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line = await runningProcess.StandardOutput.ReadLineAsync();
                    if (line is null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    using JsonDocument document = JsonDocument.Parse(line);
                    JsonElement root = document.RootElement;

                    if (root.TryGetProperty("id", out JsonElement idElement) && root.TryGetProperty("method", out JsonElement methodElement))
                    {
                        _ = HandleServerRequestAsync(idElement.Clone(), methodElement.GetString() ?? string.Empty, cancellationToken);
                        continue;
                    }

                    if (root.TryGetProperty("id", out JsonElement responseIdElement))
                    {
                        long responseId = ReadRequestId(responseIdElement);
                        if (!pendingRequests.TryRemove(responseId, out TaskCompletionSource<JsonElement>? pendingRequest))
                        {
                            continue;
                        }

                        if (root.TryGetProperty("error", out JsonElement errorElement))
                        {
                            pendingRequest.TrySetException(new InvalidOperationException(BuildRpcError(errorElement)));
                            continue;
                        }

                        JsonElement result = root.TryGetProperty("result", out JsonElement resultElement)
                            ? resultElement.Clone()
                            : default;
                        pendingRequest.TrySetResult(result);
                    }
                }
            }
            catch (Exception exception)
            {
                CancelPending(exception);
            }

            CancelPending(new InvalidOperationException($"MCP 服务器“{definition.Name}”已断开连接。{BuildStderrSuffix()}"));
        }

        private async Task PumpStderrAsync(Process runningProcess, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line = await runningProcess.StandardError.ReadLineAsync();
                    if (line is null)
                    {
                        break;
                    }

                    if (stderrLines.Count >= 20 && stderrLines.TryDequeue(out _))
                    {
                    }

                    stderrLines.Enqueue(line);
                }
            }
            catch
            {
            }
        }

        private async Task HandleServerRequestAsync(JsonElement idElement, string method, CancellationToken cancellationToken)
        {
            if (method.Equals("ping", StringComparison.OrdinalIgnoreCase))
            {
                await WriteMessageAsync(new
                {
                    jsonrpc = "2.0",
                    id = idElement,
                    result = new { },
                }, cancellationToken);

                return;
            }

            await WriteMessageAsync(new
            {
                jsonrpc = "2.0",
                id = idElement,
                error = new
                {
                    code = -32601,
                    message = $"Code-W 不支持处理来自服务器的请求方法“{method}”。",
                },
            }, cancellationToken);
        }

        private async Task<JsonElement> SendRequestAsync(
            string method,
            object? parameters,
            CancellationToken cancellationToken)
        {
            long requestId = Interlocked.Increment(ref nextRequestId);
            TaskCompletionSource<JsonElement> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            pendingRequests[requestId] = completion;

            await WriteMessageAsync(new
            {
                jsonrpc = "2.0",
                id = requestId,
                method,
                @params = parameters,
            }, cancellationToken);

            try
            {
                return await completion.Task.WaitAsync(TimeSpan.FromSeconds(90), cancellationToken);
            }
            catch
            {
                pendingRequests.TryRemove(requestId, out _);
                throw;
            }
        }

        private Task SendNotificationAsync(
            string method,
            object? parameters,
            CancellationToken cancellationToken)
        {
            return WriteMessageAsync(new
            {
                jsonrpc = "2.0",
                method,
                @params = parameters,
            }, cancellationToken);
        }

        private async Task WriteMessageAsync(object payload, CancellationToken cancellationToken)
        {
            if (process is null)
            {
                throw new InvalidOperationException($"MCP 服务器“{definition.Name}”尚未启动。");
            }

            string json = JsonSerializer.Serialize(payload, JsonOptions);

            await writeLock.WaitAsync(cancellationToken);
            try
            {
                await process.StandardInput.WriteLineAsync(json);
                await process.StandardInput.FlushAsync();
            }
            finally
            {
                writeLock.Release();
            }
        }

        private void CancelPending(Exception exception)
        {
            foreach ((long requestId, TaskCompletionSource<JsonElement> pendingRequest) in pendingRequests.ToArray())
            {
                if (pendingRequests.TryRemove(requestId, out _))
                {
                    pendingRequest.TrySetException(exception);
                }
            }
        }

        private static long ReadRequestId(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.GetInt64(),
                JsonValueKind.String when long.TryParse(element.GetString(), out long parsed) => parsed,
                _ => throw new InvalidOperationException("MCP 返回了无法识别的请求标识。"),
            };
        }

        private static JsonElement ParseArguments(string argumentsJson)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                using JsonDocument emptyDocument = JsonDocument.Parse("{}");
                return emptyDocument.RootElement.Clone();
            }

            using JsonDocument document = JsonDocument.Parse(argumentsJson);
            return document.RootElement.Clone();
        }

        private static string BuildUniqueToolName(string serverName, string toolName, HashSet<string> usedNames)
        {
            string baseName = $"{Sanitize(serverName)}__{Sanitize(toolName)}";
            string candidate = baseName;
            int suffix = 2;

            while (!usedNames.Add(candidate))
            {
                candidate = $"{baseName}_{suffix++}";
            }

            return candidate;
        }

        private static string Sanitize(string value)
        {
            StringBuilder builder = new(value.Length);
            foreach (char character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) ? character : '_');
            }

            if (builder.Length == 0)
            {
                return "tool";
            }

            return builder.ToString();
        }

        private static JsonElement CreateDefaultSchema()
        {
            using JsonDocument document = JsonDocument.Parse("""{"type":"object","properties":{}}""");
            return document.RootElement.Clone();
        }

        private static string FormatToolResult(JsonElement result)
        {
            List<string> parts = [];

            if (result.TryGetProperty("content", out JsonElement contentArray) && contentArray.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement content in contentArray.EnumerateArray())
                {
                    if (content.TryGetProperty("type", out JsonElement typeElement)
                        && typeElement.ValueKind == JsonValueKind.String
                        && string.Equals(typeElement.GetString(), "text", StringComparison.OrdinalIgnoreCase)
                        && content.TryGetProperty("text", out JsonElement textElement)
                        && textElement.ValueKind == JsonValueKind.String)
                    {
                        parts.Add(textElement.GetString() ?? string.Empty);
                    }
                    else
                    {
                        parts.Add(content.ToString());
                    }
                }
            }

            if (result.TryGetProperty("structuredContent", out JsonElement structuredContent))
            {
                parts.Add(structuredContent.ToString());
            }

            if (parts.Count == 0)
            {
                parts.Add(result.ToString());
            }

            return string.Join(Environment.NewLine + Environment.NewLine, parts.Where(static part => !string.IsNullOrWhiteSpace(part))).Trim();
        }

        private static string BuildRpcError(JsonElement errorElement)
        {
            string message = errorElement.TryGetProperty("message", out JsonElement messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? "未知错误"
                : "未知错误";

            string? data = errorElement.TryGetProperty("data", out JsonElement dataElement)
                ? dataElement.ToString()
                : null;

            return string.IsNullOrWhiteSpace(data) ? message : $"{message} {data}";
        }

        private string BuildStderrSuffix()
        {
            if (stderrLines.IsEmpty)
            {
                return string.Empty;
            }

            string[] lines = stderrLines.ToArray();
            string joined = string.Join(" | ", lines.TakeLast(5));
            return $" 诊断输出：{joined}";
        }
    }
}
