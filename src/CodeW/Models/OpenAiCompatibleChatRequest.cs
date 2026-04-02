namespace CodeW.Models;

internal sealed class OpenAiCompatibleChatRequest
{
    public required IReadOnlyList<OpenAiCompatibleMessage> Messages { get; init; }

    public IReadOnlyList<OpenAiCompatibleToolDefinition> Tools { get; init; } = [];

    public double Temperature { get; init; } = 0.2;

    public bool EnableParallelToolCalls { get; init; }
}
