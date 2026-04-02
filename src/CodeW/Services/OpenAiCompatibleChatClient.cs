namespace CodeW.Services;

using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeW.Models;

internal sealed class OpenAiCompatibleChatClient : IOpenAiCompatibleChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient httpClient;

    public OpenAiCompatibleChatClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async IAsyncEnumerable<OpenAiCompatibleCompletionStreamUpdate> StreamCompletionAsync(
        ProviderProfile provider,
        OpenAiCompatibleChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Uri requestUri = BuildChatCompletionUri(provider.BaseUrl);
        using HttpRequestMessage httpRequest = new(HttpMethod.Post, requestUri);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(BuildRequestPayload(provider, request), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(string.IsNullOrWhiteSpace(errorContent)
                ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {errorContent}");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(stream, Encoding.UTF8);
        ChatStreamAccumulator accumulator = new();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string payload = line[5..].Trim();
            if (payload.Equals("[DONE]", StringComparison.Ordinal))
            {
                break;
            }

            using JsonDocument document = JsonDocument.Parse(payload);
            foreach (OpenAiCompatibleCompletionStreamUpdate update in accumulator.ApplyChunk(document.RootElement))
            {
                yield return update;
            }
        }

        yield return accumulator.BuildCompletedUpdate();
    }

    private static object BuildRequestPayload(
        ProviderProfile provider,
        OpenAiCompatibleChatRequest request)
    {
        return new Dictionary<string, object?>
        {
            ["model"] = provider.DefaultModel,
            ["temperature"] = request.Temperature,
            ["stream"] = true,
            ["parallel_tool_calls"] = request.Tools.Count > 1 && request.EnableParallelToolCalls,
            ["messages"] = request.Messages.Select(static message => new Dictionary<string, object?>
            {
                ["role"] = message.Role,
                ["content"] = message.Content,
                ["tool_call_id"] = message.ToolCallId,
                ["tool_calls"] = message.ToolCalls.Count == 0
                    ? null
                    : message.ToolCalls.Select(static toolCall => new
                    {
                        id = toolCall.Id,
                        type = "function",
                        function = new
                        {
                            name = toolCall.Name,
                            arguments = toolCall.ArgumentsJson,
                        },
                    }),
            }),
            ["tools"] = request.Tools.Count == 0
                ? null
                : request.Tools.Select(static tool => new
                {
                    type = "function",
                    function = new
                    {
                        name = tool.Name,
                        description = tool.Description,
                        parameters = tool.Parameters,
                    },
                }),
        };
    }

    private static Uri BuildChatCompletionUri(string baseUrl)
    {
        string normalized = string.IsNullOrWhiteSpace(baseUrl)
            ? throw new InvalidOperationException("Provider Base URL 不能为空。")
            : baseUrl.TrimEnd('/');

        return normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? new Uri(normalized, UriKind.Absolute)
            : new Uri($"{normalized}/chat/completions", UriKind.Absolute);
    }

    private sealed class ChatStreamAccumulator
    {
        private readonly StringBuilder contentBuilder = new();
        private readonly Dictionary<int, ToolCallBuilder> toolCallsByIndex = [];
        private string role = "assistant";
        private string? finishReason;

        public IEnumerable<OpenAiCompatibleCompletionStreamUpdate> ApplyChunk(JsonElement root)
        {
            List<OpenAiCompatibleCompletionStreamUpdate> updates = [];

            if (!root.TryGetProperty("choices", out JsonElement choices) || choices.ValueKind != JsonValueKind.Array)
            {
                return updates;
            }

            foreach (JsonElement choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("delta", out JsonElement delta))
                {
                    if (delta.TryGetProperty("role", out JsonElement roleElement) && roleElement.ValueKind == JsonValueKind.String)
                    {
                        role = roleElement.GetString() ?? role;
                    }

                    string textDelta = ReadContentDelta(delta);
                    if (!string.IsNullOrWhiteSpace(textDelta))
                    {
                        contentBuilder.Append(textDelta);
                        updates.Add(new OpenAiCompatibleTextDeltaUpdate(textDelta));
                    }

                    if (delta.TryGetProperty("tool_calls", out JsonElement toolCallsElement) && toolCallsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement toolCallElement in toolCallsElement.EnumerateArray())
                        {
                            int index = toolCallElement.TryGetProperty("index", out JsonElement indexElement) && indexElement.ValueKind == JsonValueKind.Number
                                ? indexElement.GetInt32()
                                : 0;

                            if (!toolCallsByIndex.TryGetValue(index, out ToolCallBuilder? builder))
                            {
                                builder = new ToolCallBuilder();
                                toolCallsByIndex[index] = builder;
                            }

                            if (toolCallElement.TryGetProperty("id", out JsonElement toolCallIdElement) && toolCallIdElement.ValueKind == JsonValueKind.String)
                            {
                                builder.Id ??= toolCallIdElement.GetString();
                            }

                            if (toolCallElement.TryGetProperty("function", out JsonElement functionElement))
                            {
                                if (functionElement.TryGetProperty("name", out JsonElement functionNameElement) && functionNameElement.ValueKind == JsonValueKind.String)
                                {
                                    builder.Name ??= functionNameElement.GetString();
                                }

                                if (functionElement.TryGetProperty("arguments", out JsonElement argumentsElement))
                                {
                                    string argumentsDelta = argumentsElement.ValueKind == JsonValueKind.String
                                        ? argumentsElement.GetString() ?? string.Empty
                                        : argumentsElement.ToString();

                                    if (!string.IsNullOrEmpty(argumentsDelta))
                                    {
                                        builder.Arguments.Append(argumentsDelta);
                                        updates.Add(new OpenAiCompatibleToolCallDeltaUpdate(
                                            index,
                                            builder.Id,
                                            builder.Name,
                                            argumentsDelta));
                                    }
                                }
                            }
                        }
                    }
                }

                if (choice.TryGetProperty("finish_reason", out JsonElement finishReasonElement)
                    && finishReasonElement.ValueKind == JsonValueKind.String)
                {
                    finishReason = finishReasonElement.GetString();
                }
            }

            return updates;
        }

        public OpenAiCompatibleCompletionFinishedUpdate BuildCompletedUpdate()
        {
            return new OpenAiCompatibleCompletionFinishedUpdate(
                new OpenAiCompatibleMessage
                {
                    Role = role,
                    Content = contentBuilder.Length == 0 ? null : contentBuilder.ToString(),
                    ToolCalls = toolCallsByIndex
                        .OrderBy(static entry => entry.Key)
                        .Select(static entry => entry.Value.Build(entry.Key))
                        .ToList(),
                },
                finishReason);
        }

        private static string ReadContentDelta(JsonElement delta)
        {
            if (!delta.TryGetProperty("content", out JsonElement contentElement))
            {
                return string.Empty;
            }

            return contentElement.ValueKind switch
            {
                JsonValueKind.String => contentElement.GetString() ?? string.Empty,
                JsonValueKind.Array => string.Join(
                    string.Empty,
                    contentElement.EnumerateArray().Select(ReadSegment)),
                JsonValueKind.Object => ReadSegment(contentElement),
                _ => contentElement.ToString(),
            };
        }

        private static string ReadSegment(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? string.Empty;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("text", out JsonElement textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    return textElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("content", out JsonElement contentElement) && contentElement.ValueKind == JsonValueKind.String)
                {
                    return contentElement.GetString() ?? string.Empty;
                }
            }

            return element.ToString();
        }

        private sealed class ToolCallBuilder
        {
            public string? Id { get; set; }

            public string? Name { get; set; }

            public StringBuilder Arguments { get; } = new();

            public OpenAiCompatibleToolCall Build(int index)
            {
                return new OpenAiCompatibleToolCall
                {
                    Id = string.IsNullOrWhiteSpace(Id) ? $"call_{index}" : Id,
                    Name = string.IsNullOrWhiteSpace(Name) ? $"tool_{index}" : Name,
                    ArgumentsJson = Arguments.Length == 0 ? "{}" : Arguments.ToString(),
                };
            }
        }
    }
}
