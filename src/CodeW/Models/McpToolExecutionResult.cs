namespace CodeW.Models;

internal sealed class McpToolExecutionResult
{
    public required string ServerName { get; init; }

    public required string ToolName { get; init; }

    public required string OutputText { get; init; }

    public required bool IsError { get; init; }
}
