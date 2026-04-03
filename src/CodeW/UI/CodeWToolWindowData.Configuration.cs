namespace CodeW.UI;

using System.Collections.ObjectModel;
using CodeW.Models;

internal sealed partial class CodeWToolWindowData
{
    private async Task AddMcpServerAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        CommitSelectedMcpServer();

        McpServerDefinition server = new()
        {
            Name = BuildUniqueName("custom-mcp", configuration.McpServers.Select(static item => item.Name)),
            Description = "自定义 MCP Server",
            Enabled = true,
        };

        configuration.McpServers.Add(server);
        selectedMcpServer = server;
        await SaveInternalAsync(cancellationToken, "已新增 MCP Server，请继续填写启动命令并保存。");
    }

    private async Task RemoveMcpServerAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        if (selectedMcpServer is null)
        {
            StatusMessage = "当前没有可删除的 MCP Server。";
            return;
        }

        string removedName = selectedMcpServer.Name;
        configuration.McpServers.Remove(selectedMcpServer);
        selectedMcpServer = null;
        await SaveInternalAsync(cancellationToken, $"已移除 MCP Server：{removedName}");
    }

    private async Task AddSkillAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        CommitSelectedSkill();

        SkillDefinition skill = new()
        {
            Name = BuildUniqueName("custom-skill", configuration.Skills.Select(static item => item.Name)),
            Description = "自定义 Skill",
            EntryPoint = "skills/custom-skill.md",
            Enabled = true,
        };

        configuration.Skills.Add(skill);
        selectedSkill = skill;
        await SaveInternalAsync(cancellationToken, "已新增 Skill，请继续填写入口文件并保存。");
    }

    private async Task RemoveSkillAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        if (selectedSkill is null)
        {
            StatusMessage = "当前没有可删除的 Skill。";
            return;
        }

        string removedName = selectedSkill.Name;
        configuration.Skills.Remove(selectedSkill);
        selectedSkill = null;
        await SaveInternalAsync(cancellationToken, $"已移除 Skill：{removedName}");
    }

    private async Task SaveCurrentConfigurationAsync(CancellationToken cancellationToken, string successMessage)
    {
        await EnsureLoadedAsync(cancellationToken);
        CommitAllEditors();
        await SaveInternalAsync(cancellationToken, successMessage);
    }

    private async Task SaveInternalAsync(CancellationToken cancellationToken, string successMessage)
    {
        RefreshProviderOptions();
        RefreshMcpCollections(selectedMcpServer);
        RefreshSkillCollections(selectedSkill);

        await configurationStore.SaveAsync(configuration, cancellationToken);
        StatusMessage = successMessage;
    }

    private void CommitAllEditors()
    {
        CommitSelectedProvider();
        CommitSelectedMcpServer();
        CommitSelectedSkill();
    }

    private void CommitSelectedProvider()
    {
        ProviderProfile? provider = GetSelectedProvider();
        if (provider is null)
        {
            return;
        }

        provider.BaseUrl = ProviderBaseUrl.Trim();
        provider.DefaultModel = ProviderModel.Trim();
        provider.ApiKey = ProviderApiKey.Trim();
        provider.Enabled = ProviderEnabled;
        configuration.ActiveProviderId = provider.Id;
    }

    private void CommitSelectedMcpServer()
    {
        if (selectedMcpServer is null)
        {
            return;
        }

        selectedMcpServer.Name = BuildUniqueName(
            string.IsNullOrWhiteSpace(McpServerName) ? "custom-mcp" : McpServerName.Trim(),
            configuration.McpServers
                .Where(server => !ReferenceEquals(server, selectedMcpServer))
                .Select(static server => server.Name));
        selectedMcpServer.Description = McpServerDescription.Trim();
        selectedMcpServer.Command = McpServerCommand.Trim();
        selectedMcpServer.Arguments = McpServerArguments.Trim();
        selectedMcpServer.Enabled = McpServerEnabled;
    }

    private void CommitSelectedSkill()
    {
        if (selectedSkill is null)
        {
            return;
        }

        selectedSkill.Name = BuildUniqueName(
            string.IsNullOrWhiteSpace(SkillName) ? "custom-skill" : SkillName.Trim(),
            configuration.Skills
                .Where(skill => !ReferenceEquals(skill, selectedSkill))
                .Select(static skill => skill.Name));
        selectedSkill.Description = SkillDescription.Trim();
        selectedSkill.EntryPoint = SkillEntryPoint.Trim();
        selectedSkill.Enabled = SkillEnabled;
    }

    private ProviderProfile? GetSelectedProvider()
    {
        return configuration.Providers.FirstOrDefault(provider => string.Equals(provider.DisplayName, SelectedProviderName, StringComparison.Ordinal));
    }

    private void LoadSelectedProviderIntoEditor()
    {
        ProviderProfile? provider = GetSelectedProvider();
        if (provider is null)
        {
            ProviderBaseUrl = string.Empty;
            ProviderModel = string.Empty;
            ProviderApiKey = string.Empty;
            ProviderDescription = string.Empty;
            ProviderEnabled = false;
            return;
        }

        configuration.ActiveProviderId = provider.Id;
        ProviderBaseUrl = provider.BaseUrl;
        ProviderModel = provider.DefaultModel;
        ProviderApiKey = provider.ApiKey;
        ProviderDescription = provider.Description;
        ProviderEnabled = provider.Enabled;
    }

    private void SetSelectedProviderName(string value)
    {
        if (selectedProviderName == value)
        {
            return;
        }

        CommitSelectedProvider();
        selectedProviderName = value;
        LoadSelectedProviderIntoEditor();
        RaiseNotifyPropertyChangedEvent(nameof(SelectedProviderName));
    }

    private void SetSelectedMcpServerName(string value)
    {
        if (selectedMcpServerName == value)
        {
            return;
        }

        if (isRefreshingSelections)
        {
            SetProperty(ref selectedMcpServerName, value);
            return;
        }

        CommitSelectedMcpServer();
        selectedMcpServer = configuration.McpServers.FirstOrDefault(server => string.Equals(server.Name, value, StringComparison.Ordinal));
        RefreshMcpCollections(selectedMcpServer);
    }

    private void SetSelectedSkillName(string value)
    {
        if (selectedSkillName == value)
        {
            return;
        }

        if (isRefreshingSelections)
        {
            SetProperty(ref selectedSkillName, value);
            return;
        }

        CommitSelectedSkill();
        selectedSkill = configuration.Skills.FirstOrDefault(skill => string.Equals(skill.Name, value, StringComparison.Ordinal));
        RefreshSkillCollections(selectedSkill);
    }

    private void RefreshProviderOptions()
    {
        UpdateStringCollection(ProviderOptions, configuration.Providers.Select(static provider => provider.DisplayName));
    }

    private void RefreshMcpCollections(McpServerDefinition? preferredSelection)
    {
        isRefreshingSelections = true;
        try
        {
            UpdateFeatureCollection(McpServers, configuration.McpServers.Select(static server => new FeatureEntryViewModel
            {
                Name = server.Name,
                Description = server.Description,
                Detail = $"{server.Command} {server.Arguments}".Trim(),
                Status = server.Enabled ? "已启用" : "已禁用",
            }));

            UpdateStringCollection(McpServerOptions, configuration.McpServers.Select(static server => server.Name));

            selectedMcpServer = preferredSelection is not null && configuration.McpServers.Contains(preferredSelection)
                ? preferredSelection
                : configuration.McpServers.FirstOrDefault();

            SelectedMcpServerName = selectedMcpServer?.Name ?? string.Empty;
            LoadSelectedMcpIntoEditor();
        }
        finally
        {
            isRefreshingSelections = false;
        }
    }

    private void RefreshSkillCollections(SkillDefinition? preferredSelection)
    {
        isRefreshingSelections = true;
        try
        {
            UpdateFeatureCollection(Skills, configuration.Skills.Select(static skill => new FeatureEntryViewModel
            {
                Name = skill.Name,
                Description = skill.Description,
                Detail = skill.EntryPoint,
                Status = skill.Enabled ? "已启用" : "已禁用",
            }));

            UpdateStringCollection(SkillOptions, configuration.Skills.Select(static skill => skill.Name));

            selectedSkill = preferredSelection is not null && configuration.Skills.Contains(preferredSelection)
                ? preferredSelection
                : configuration.Skills.FirstOrDefault();

            SelectedSkillName = selectedSkill?.Name ?? string.Empty;
            LoadSelectedSkillIntoEditor();
        }
        finally
        {
            isRefreshingSelections = false;
        }
    }

    private void LoadSelectedMcpIntoEditor()
    {
        if (selectedMcpServer is null)
        {
            McpServerName = string.Empty;
            McpServerDescription = string.Empty;
            McpServerCommand = string.Empty;
            McpServerArguments = string.Empty;
            McpServerEnabled = false;
            return;
        }

        McpServerName = selectedMcpServer.Name;
        McpServerDescription = selectedMcpServer.Description;
        McpServerCommand = selectedMcpServer.Command;
        McpServerArguments = selectedMcpServer.Arguments;
        McpServerEnabled = selectedMcpServer.Enabled;
    }

    private void LoadSelectedSkillIntoEditor()
    {
        if (selectedSkill is null)
        {
            SkillName = string.Empty;
            SkillDescription = string.Empty;
            SkillEntryPoint = string.Empty;
            SkillEnabled = false;
            return;
        }

        SkillName = selectedSkill.Name;
        SkillDescription = selectedSkill.Description;
        SkillEntryPoint = selectedSkill.EntryPoint;
        SkillEnabled = selectedSkill.Enabled;
    }

    private static string BuildUniqueName(string seed, IEnumerable<string> existingNames)
    {
        string baseName = string.IsNullOrWhiteSpace(seed) ? "item" : seed.Trim();
        HashSet<string> taken = existingNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        int suffix = 2;
        string candidate;
        do
        {
            candidate = $"{baseName}-{suffix++}";
        }
        while (taken.Contains(candidate));

        return candidate;
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/').Trim();

    private static void UpdateStringCollection(
        ObservableCollection<string> collection,
        IEnumerable<string> items)
    {
        collection.Clear();
        foreach (string item in items)
        {
            collection.Add(item);
        }
    }

    private static void UpdateFeatureCollection<T>(
        ObservableCollection<T> collection,
        IEnumerable<T> items)
    {
        collection.Clear();
        foreach (T item in items)
        {
            collection.Add(item);
        }
    }
}
