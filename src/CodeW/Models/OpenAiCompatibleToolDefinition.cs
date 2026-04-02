namespace CodeW.Models;

using System.Text.Json;

internal sealed class OpenAiCompatibleToolDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required JsonElement Parameters { get; init; }
}
