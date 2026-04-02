namespace CodeW.Models;

internal sealed class OpenAiCompatibleToolCall
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string ArgumentsJson { get; init; }
}
