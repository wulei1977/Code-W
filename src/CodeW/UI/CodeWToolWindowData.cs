namespace CodeW.UI;

using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using CodeW.Models;
using CodeW.Services;
using Microsoft.VisualStudio.Extensibility.UI;

[DataContract]
internal sealed partial class CodeWToolWindowData : NotifyPropertyChangedObject
{
    private readonly ICodeWConfigurationStore configurationStore;
    private readonly ICodeWConversationService conversationService;
    private readonly IMcpService mcpService;
    private readonly ISkillService skillService;

    private CodeWConfiguration configuration = new();
    private CancellationTokenSource? activeRequestSource;
    private McpServerDefinition? selectedMcpServer;
    private SkillDefinition? selectedSkill;
    private bool isRefreshingSelections;

    private string selectedProviderName = string.Empty;
    private string providerBaseUrl = string.Empty;
    private string providerModel = string.Empty;
    private string providerApiKey = string.Empty;
    private string providerDescription = string.Empty;
    private bool providerEnabled;

    private string selectedMcpServerName = string.Empty;
    private string mcpServerName = string.Empty;
    private string mcpServerDescription = string.Empty;
    private string mcpServerCommand = string.Empty;
    private string mcpServerArguments = string.Empty;
    private bool mcpServerEnabled;

    private string selectedSkillName = string.Empty;
    private string skillName = string.Empty;
    private string skillDescription = string.Empty;
    private string skillEntryPoint = string.Empty;
    private bool skillEnabled;

    private string draftPrompt = string.Empty;
    private string statusMessage = "正在初始化 Code-W...";
    private string modeDisplayName = "Chat";
    private string modeDescription = "偏向即时问答、代码解释与建议，默认使用流式输出。";
    private string storagePath = string.Empty;
    private string contextSummary = "尚未捕获到 IDE 上下文。";
    private bool isBusy;

    public CodeWToolWindowData(
        ICodeWConfigurationStore configurationStore,
        ICodeWConversationService conversationService,
        IMcpService mcpService,
        ISkillService skillService)
    {
        this.configurationStore = configurationStore;
        this.conversationService = conversationService;
        this.mcpService = mcpService;
        this.skillService = skillService;

        ProviderOptions = [];
        Transcript = [];
        McpServers = [];
        Skills = [];
        McpServerOptions = [];
        SkillOptions = [];

        SwitchToChatCommand = new AsyncCommand((_, cancellationToken) =>
        {
            SetMode(ConversationMode.Chat);
            return SaveCurrentConfigurationAsync(cancellationToken, "已切换到 Chat 模式");
        });

        SwitchToAgentCommand = new AsyncCommand((_, cancellationToken) =>
        {
            SetMode(ConversationMode.Agent);
            return SaveCurrentConfigurationAsync(cancellationToken, "已切换到 Agent 模式");
        });

        SaveConfigurationCommand = new AsyncCommand((_, cancellationToken) => SaveCurrentConfigurationAsync(cancellationToken, "配置已保存"));
        AddMcpServerCommand = new AsyncCommand((_, cancellationToken) => AddMcpServerAsync(cancellationToken));
        RemoveMcpServerCommand = new AsyncCommand((_, cancellationToken) => RemoveMcpServerAsync(cancellationToken));
        TestMcpServerCommand = new AsyncCommand(TestMcpServerAsync);
        AddSkillCommand = new AsyncCommand((_, cancellationToken) => AddSkillAsync(cancellationToken));
        RemoveSkillCommand = new AsyncCommand((_, cancellationToken) => RemoveSkillAsync(cancellationToken));
        ScanSkillsCommand = new AsyncCommand(ScanSkillsAsync);

        ResetConversationCommand = new AsyncCommand((_, _) =>
        {
            if (IsBusy)
            {
                StatusMessage = "请先停止当前请求，再清空会话。";
                return Task.CompletedTask;
            }

            ResetTranscript();
            StatusMessage = "会话已清空";
            return Task.CompletedTask;
        });

        CancelRunCommand = new AsyncCommand((_, _) =>
        {
            if (activeRequestSource is null || activeRequestSource.IsCancellationRequested)
            {
                StatusMessage = "当前没有正在执行的请求。";
                return Task.CompletedTask;
            }

            StatusMessage = "正在取消当前请求...";
            return activeRequestSource.CancelAsync();
        });

        SendPromptCommand = new AsyncCommand(SendPromptAsync);
    }

    [DataMember]
    public ObservableCollection<string> ProviderOptions { get; }

    [DataMember]
    public ObservableCollection<string> McpServerOptions { get; }

    [DataMember]
    public ObservableCollection<string> SkillOptions { get; }

    [DataMember]
    public ObservableCollection<ConversationTurnViewModel> Transcript { get; }

    [DataMember]
    public ObservableCollection<FeatureEntryViewModel> McpServers { get; }

    [DataMember]
    public ObservableCollection<FeatureEntryViewModel> Skills { get; }

    [DataMember]
    public string SelectedProviderName
    {
        get => selectedProviderName;
        set => SetSelectedProviderName(value);
    }

    [DataMember]
    public string ProviderBaseUrl
    {
        get => providerBaseUrl;
        set => SetProperty(ref providerBaseUrl, value);
    }

    [DataMember]
    public string ProviderModel
    {
        get => providerModel;
        set => SetProperty(ref providerModel, value);
    }

    [DataMember]
    public string ProviderApiKey
    {
        get => providerApiKey;
        set => SetProperty(ref providerApiKey, value);
    }

    [DataMember]
    public string ProviderDescription
    {
        get => providerDescription;
        set => SetProperty(ref providerDescription, value);
    }

    [DataMember]
    public bool ProviderEnabled
    {
        get => providerEnabled;
        set => SetProperty(ref providerEnabled, value);
    }

    [DataMember]
    public string SelectedMcpServerName
    {
        get => selectedMcpServerName;
        set => SetSelectedMcpServerName(value);
    }

    [DataMember]
    public string McpServerName
    {
        get => mcpServerName;
        set => SetProperty(ref mcpServerName, value);
    }

    [DataMember]
    public string McpServerDescription
    {
        get => mcpServerDescription;
        set => SetProperty(ref mcpServerDescription, value);
    }

    [DataMember]
    public string McpServerCommand
    {
        get => mcpServerCommand;
        set => SetProperty(ref mcpServerCommand, value);
    }

    [DataMember]
    public string McpServerArguments
    {
        get => mcpServerArguments;
        set => SetProperty(ref mcpServerArguments, value);
    }

    [DataMember]
    public bool McpServerEnabled
    {
        get => mcpServerEnabled;
        set => SetProperty(ref mcpServerEnabled, value);
    }

    [DataMember]
    public string SelectedSkillName
    {
        get => selectedSkillName;
        set => SetSelectedSkillName(value);
    }

    [DataMember]
    public string SkillName
    {
        get => skillName;
        set => SetProperty(ref skillName, value);
    }

    [DataMember]
    public string SkillDescription
    {
        get => skillDescription;
        set => SetProperty(ref skillDescription, value);
    }

    [DataMember]
    public string SkillEntryPoint
    {
        get => skillEntryPoint;
        set => SetProperty(ref skillEntryPoint, value);
    }

    [DataMember]
    public bool SkillEnabled
    {
        get => skillEnabled;
        set => SetProperty(ref skillEnabled, value);
    }

    [DataMember]
    public string DraftPrompt
    {
        get => draftPrompt;
        set => SetProperty(ref draftPrompt, value);
    }

    [DataMember]
    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    [DataMember]
    public string ModeDisplayName
    {
        get => modeDisplayName;
        set => SetProperty(ref modeDisplayName, value);
    }

    [DataMember]
    public string ModeDescription
    {
        get => modeDescription;
        set => SetProperty(ref modeDescription, value);
    }

    [DataMember]
    public string StoragePath
    {
        get => storagePath;
        set => SetProperty(ref storagePath, value);
    }

    [DataMember]
    public string ContextSummary
    {
        get => contextSummary;
        set => SetProperty(ref contextSummary, value);
    }

    [DataMember]
    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    [DataMember]
    public AsyncCommand SwitchToChatCommand { get; }

    [DataMember]
    public AsyncCommand SwitchToAgentCommand { get; }

    [DataMember]
    public AsyncCommand SaveConfigurationCommand { get; }

    [DataMember]
    public AsyncCommand AddMcpServerCommand { get; }

    [DataMember]
    public AsyncCommand RemoveMcpServerCommand { get; }

    [DataMember]
    public AsyncCommand TestMcpServerCommand { get; }

    [DataMember]
    public AsyncCommand AddSkillCommand { get; }

    [DataMember]
    public AsyncCommand RemoveSkillCommand { get; }

    [DataMember]
    public AsyncCommand ScanSkillsCommand { get; }

    [DataMember]
    public AsyncCommand ResetConversationCommand { get; }

    [DataMember]
    public AsyncCommand CancelRunCommand { get; }

    [DataMember]
    public AsyncCommand SendPromptCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        configuration = await configurationStore.LoadAsync(cancellationToken);
        StoragePath = configurationStore.StoragePath;

        RefreshProviderOptions();
        SetMode(configuration.DefaultMode);

        ProviderProfile? activeProvider = configuration.Providers.FirstOrDefault(provider => provider.Id == configuration.ActiveProviderId)
            ?? configuration.Providers.FirstOrDefault();

        selectedProviderName = activeProvider?.DisplayName ?? string.Empty;
        RaiseNotifyPropertyChangedEvent(nameof(SelectedProviderName));
        LoadSelectedProviderIntoEditor();

        RefreshMcpCollections(configuration.McpServers.FirstOrDefault(static server => server.Enabled) ?? configuration.McpServers.FirstOrDefault());
        RefreshSkillCollections(configuration.Skills.FirstOrDefault(static skill => skill.Enabled) ?? configuration.Skills.FirstOrDefault());

        ResetTranscript();
        StatusMessage = string.IsNullOrWhiteSpace(SelectedProviderName)
            ? "Code-W 已准备就绪"
            : $"Code-W 已准备就绪，当前 Provider：{SelectedProviderName}";
    }
}
