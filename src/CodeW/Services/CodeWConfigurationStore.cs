namespace CodeW.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeW.Models;

internal sealed class CodeWConfigurationStore : ICodeWConfigurationStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Code-W");

    private readonly ICodeWProviderCatalog providerCatalog;
    private readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    public CodeWConfigurationStore(ICodeWProviderCatalog providerCatalog)
    {
        this.providerCatalog = providerCatalog;
    }

    public string StoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Code-W",
        "codew.settings.json");

    public async Task<CodeWConfiguration> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StoragePath))
        {
            return CreateDefaultConfiguration();
        }

        await using FileStream stream = File.OpenRead(StoragePath);
        StoredConfiguration? stored = await JsonSerializer.DeserializeAsync<StoredConfiguration>(stream, serializerOptions, cancellationToken);
        if (stored is null)
        {
            return CreateDefaultConfiguration();
        }

        CodeWConfiguration configuration = new()
        {
            ActiveProviderId = stored.ActiveProviderId,
            DefaultMode = stored.DefaultMode,
            Providers = stored.Providers.Select(ToModel).ToList(),
            McpServers = stored.McpServers.Select(ToModel).ToList(),
            Skills = stored.Skills.Select(ToModel).ToList(),
        };

        MergeMissingPresets(configuration);
        EnsureValidActiveProvider(configuration);
        return configuration;
    }

    public async Task SaveAsync(CodeWConfiguration configuration, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);

        StoredConfiguration stored = new()
        {
            ActiveProviderId = configuration.ActiveProviderId,
            DefaultMode = configuration.DefaultMode,
            Providers = configuration.Providers.Select(ToStored).ToList(),
            McpServers = configuration.McpServers.Select(ToStored).ToList(),
            Skills = configuration.Skills.Select(ToStored).ToList(),
        };

        await using FileStream stream = File.Create(StoragePath);
        await JsonSerializer.SerializeAsync(stream, stored, serializerOptions, cancellationToken);
    }

    private CodeWConfiguration CreateDefaultConfiguration()
    {
        return new CodeWConfiguration
        {
            Providers = providerCatalog.GetPresetProfiles().Select(static profile => profile.Clone()).ToList(),
            ActiveProviderId = ProviderIds.OpenAI,
            DefaultMode = ConversationMode.Chat,
            McpServers =
            [
                new McpServerDefinition
                {
                    Name = "filesystem",
                    Description = "为 Agent 暴露当前工作区文件系统，便于做上下文读取与检索。",
                    Command = "npx",
                    Arguments = "-y @modelcontextprotocol/server-filesystem .",
                    Enabled = true,
                },
                new McpServerDefinition
                {
                    Name = "git",
                    Description = "提供提交历史、diff 和变更集语义，适合代码审查与补丁推理。",
                    Command = "uvx",
                    Arguments = "mcp-server-git",
                    Enabled = false,
                },
            ],
            Skills =
            [
                new SkillDefinition
                {
                    Name = "solution-review",
                    Description = "面向 Visual Studio 解决方案的代码审查技能。",
                    EntryPoint = "skills/solution-review.md",
                    Enabled = true,
                },
                new SkillDefinition
                {
                    Name = "migration-helper",
                    Description = "面向大型升级与重构任务的迁移技能。",
                    EntryPoint = "skills/migration-helper.md",
                    Enabled = false,
                },
            ],
        };
    }

    private void EnsureValidActiveProvider(CodeWConfiguration configuration)
    {
        if (configuration.Providers.Any(profile => profile.Id == configuration.ActiveProviderId))
        {
            return;
        }

        configuration.ActiveProviderId = configuration.Providers.FirstOrDefault()?.Id ?? ProviderIds.OpenAI;
    }

    private void MergeMissingPresets(CodeWConfiguration configuration)
    {
        HashSet<string> existingIds = configuration.Providers.Select(static profile => profile.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (ProviderProfile preset in providerCatalog.GetPresetProfiles())
        {
            if (!existingIds.Contains(preset.Id))
            {
                configuration.Providers.Add(preset.Clone());
            }
        }
    }

    private static ProviderProfile ToModel(StoredProviderProfile stored)
    {
        return new ProviderProfile
        {
            Id = stored.Id,
            DisplayName = stored.DisplayName,
            Kind = stored.Kind,
            Description = stored.Description,
            BaseUrl = stored.BaseUrl,
            DefaultModel = stored.DefaultModel,
            Enabled = stored.Enabled,
            ApiKey = Unprotect(stored.ApiKeyProtected),
        };
    }

    private static StoredProviderProfile ToStored(ProviderProfile model)
    {
        return new StoredProviderProfile
        {
            Id = model.Id,
            DisplayName = model.DisplayName,
            Kind = model.Kind,
            Description = model.Description,
            BaseUrl = model.BaseUrl,
            DefaultModel = model.DefaultModel,
            Enabled = model.Enabled,
            ApiKeyProtected = Protect(model.ApiKey),
        };
    }

    private static McpServerDefinition ToModel(StoredMcpServerDefinition stored)
    {
        return new McpServerDefinition
        {
            Name = stored.Name,
            Description = stored.Description,
            Command = stored.Command,
            Arguments = stored.Arguments,
            Enabled = stored.Enabled,
        };
    }

    private static StoredMcpServerDefinition ToStored(McpServerDefinition model)
    {
        return new StoredMcpServerDefinition
        {
            Name = model.Name,
            Description = model.Description,
            Command = model.Command,
            Arguments = model.Arguments,
            Enabled = model.Enabled,
        };
    }

    private static SkillDefinition ToModel(StoredSkillDefinition stored)
    {
        return new SkillDefinition
        {
            Name = stored.Name,
            Description = stored.Description,
            EntryPoint = stored.EntryPoint,
            Enabled = stored.Enabled,
        };
    }

    private static StoredSkillDefinition ToStored(SkillDefinition model)
    {
        return new StoredSkillDefinition
        {
            Name = model.Name,
            Description = model.Description,
            EntryPoint = model.EntryPoint,
            Enabled = model.Enabled,
        };
    }

    private static string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        byte[] protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return string.Empty;
        }

        try
        {
            byte[] protectedBytes = Convert.FromBase64String(protectedValue);
            byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            return string.Empty;
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }

    private sealed class StoredConfiguration
    {
        public ConversationMode DefaultMode { get; set; } = ConversationMode.Chat;

        public string ActiveProviderId { get; set; } = ProviderIds.OpenAI;

        public List<StoredProviderProfile> Providers { get; set; } = [];

        public List<StoredMcpServerDefinition> McpServers { get; set; } = [];

        public List<StoredSkillDefinition> Skills { get; set; } = [];
    }

    private sealed class StoredProviderProfile
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public ModelProviderKind Kind { get; set; }

        public string Description { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = string.Empty;

        public string DefaultModel { get; set; } = string.Empty;

        public string ApiKeyProtected { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;
    }

    private sealed class StoredMcpServerDefinition
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Command { get; set; } = string.Empty;

        public string Arguments { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;
    }

    private sealed class StoredSkillDefinition
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string EntryPoint { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;
    }
}
