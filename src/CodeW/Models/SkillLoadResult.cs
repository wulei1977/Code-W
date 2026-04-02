namespace CodeW.Models;

internal sealed class SkillLoadResult
{
    public IReadOnlyList<string> Instructions { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
