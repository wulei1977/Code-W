namespace CodeW.Models;

internal sealed class OpenAiCompatibleMessage
{
    public required string Role { get; init; }

    public string? Content { get; init; }

    public string? ToolCallId { get; init; }

    public IReadOnlyList<OpenAiCompatibleToolCall> ToolCalls { get; init; } = [];
}
