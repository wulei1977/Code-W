namespace CodeW.Models;

internal sealed class SkillDiscoveryResult
{
    public string? SkillsDirectory { get; init; }

    public string? RepositoryRoot { get; init; }

    public IReadOnlyList<SkillDefinition> Skills { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
