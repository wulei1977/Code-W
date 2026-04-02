namespace CodeW.Models;

using System.Text.Json;

internal sealed class McpToolRegistration
{
    public required string ExposedName { get; init; }

    public required string OriginalName { get; init; }

    public required string ServerName { get; init; }

    public required string Description { get; init; }

    public required JsonElement InputSchema { get; init; }

    public string? ServerInstructions { get; init; }
}
