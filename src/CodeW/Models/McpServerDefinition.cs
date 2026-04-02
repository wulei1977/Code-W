namespace CodeW.Models;

internal sealed class McpServerDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}
