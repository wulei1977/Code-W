namespace CodeW.Models;

internal sealed class SkillDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string EntryPoint { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}
