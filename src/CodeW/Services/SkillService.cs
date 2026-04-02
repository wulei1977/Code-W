namespace CodeW.Services;

using System.Text;
using CodeW.Models;

internal sealed class SkillService : ISkillService
{
    private const int MaxSkillCharacters = 12_000;
    private const int MaxDescriptionLength = 140;

    public async Task<SkillLoadResult> LoadActiveSkillsAsync(
        IReadOnlyList<SkillDefinition> skills,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        List<string> instructions = [];
        List<string> warnings = [];

        foreach (SkillDefinition skill in skills.Where(static skill => skill.Enabled))
        {
            string? resolvedPath = ResolvePath(skill.EntryPoint, workingDirectory);
            if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
            {
                warnings.Add($"Skill \"{skill.Name}\" 未找到入口文件：{skill.EntryPoint}");
                continue;
            }

            string content;
            try
            {
                content = await File.ReadAllTextAsync(resolvedPath, cancellationToken);
            }
            catch (Exception exception)
            {
                warnings.Add($"Skill \"{skill.Name}\" 读取失败：{exception.Message}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                warnings.Add($"Skill \"{skill.Name}\" 文件为空：{resolvedPath}");
                continue;
            }

            instructions.Add(BuildInstructionBlock(skill, resolvedPath, content));
        }

        return new SkillLoadResult
        {
            Instructions = instructions,
            Warnings = warnings,
        };
    }

    public Task<SkillDiscoveryResult> DiscoverSkillsAsync(
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? skillsDirectory = FindSkillsDirectory(workingDirectory);
        if (string.IsNullOrWhiteSpace(skillsDirectory))
        {
            return Task.FromResult(new SkillDiscoveryResult
            {
                Warnings =
                [
                    "没有找到 skills 目录。请先在解决方案目录下创建 skills 文件夹，或在解决方案树中选中正确路径后再试一次。",
                ],
            });
        }

        string repositoryRoot = Directory.GetParent(skillsDirectory)?.FullName ?? skillsDirectory;
        List<SkillDefinition> discoveredSkills = [];
        List<string> warnings = [];

        IEnumerable<string> files = Directory.EnumerateFiles(skillsDirectory, "*.md", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in files)
        {
            try
            {
                string relativePath = Path.GetRelativePath(repositoryRoot, filePath).Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(filePath);
                string description = BuildDescription(filePath);

                discoveredSkills.Add(new SkillDefinition
                {
                    Name = name,
                    Description = description,
                    EntryPoint = relativePath,
                    Enabled = true,
                });
            }
            catch (Exception exception)
            {
                warnings.Add($"扫描 Skill 文件失败：{filePath}，原因：{exception.Message}");
            }
        }

        if (discoveredSkills.Count == 0)
        {
            warnings.Add($"已找到 skills 目录，但里面没有可用的 .md 文件：{skillsDirectory}");
        }

        return Task.FromResult(new SkillDiscoveryResult
        {
            SkillsDirectory = skillsDirectory,
            RepositoryRoot = repositoryRoot,
            Skills = discoveredSkills,
            Warnings = warnings,
        });
    }

    private static string? ResolvePath(string entryPoint, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(entryPoint))
        {
            return null;
        }

        if (Path.IsPathRooted(entryPoint))
        {
            return entryPoint;
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            return Path.GetFullPath(Path.Combine(workingDirectory, entryPoint));
        }

        return Path.GetFullPath(entryPoint);
    }

    private static string? FindSkillsDirectory(string? workingDirectory)
    {
        string startDirectory = !string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory)
            ? workingDirectory
            : Environment.CurrentDirectory;

        DirectoryInfo? current = new(startDirectory);
        while (current is not null)
        {
            if (string.Equals(current.Name, "skills", StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }

            string candidate = Path.Combine(current.FullName, "skills");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string BuildDescription(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);

        string? heading = lines
            .Select(static line => line.Trim())
            .FirstOrDefault(static line => line.StartsWith('#'));

        if (!string.IsNullOrWhiteSpace(heading))
        {
            string title = heading.TrimStart('#').Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                return TrimDescription(title);
            }
        }

        string? firstContent = lines
            .Select(static line => line.Trim())
            .FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line));

        return string.IsNullOrWhiteSpace(firstContent)
            ? "从本地 skills 目录扫描得到的技能。"
            : TrimDescription(firstContent);
    }

    private static string BuildInstructionBlock(SkillDefinition skill, string resolvedPath, string content)
    {
        string normalized = content.Trim();
        bool truncated = false;
        if (normalized.Length > MaxSkillCharacters)
        {
            normalized = normalized[..MaxSkillCharacters].TrimEnd();
            truncated = true;
        }

        StringBuilder builder = new();
        builder.AppendLine($"Skill: {skill.Name}");
        builder.AppendLine($"Path: {resolvedPath}");
        builder.AppendLine("Instructions:");
        builder.AppendLine(normalized);

        if (truncated)
        {
            builder.AppendLine();
            builder.AppendLine("[内容过长，已截断]");
        }

        return builder.ToString().TrimEnd();
    }

    private static string TrimDescription(string text)
    {
        string normalized = text.Trim();
        if (normalized.Length <= MaxDescriptionLength)
        {
            return normalized;
        }

        return $"{normalized[..MaxDescriptionLength].TrimEnd()}...";
    }
}
