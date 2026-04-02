namespace CodeW.Services;

using CodeW.Models;

internal interface ICodeWProviderCatalog
{
    IReadOnlyList<ProviderProfile> GetPresetProfiles();
}

internal interface ICodeWConfigurationStore
{
    string StoragePath { get; }

    Task<CodeWConfiguration> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(CodeWConfiguration configuration, CancellationToken cancellationToken);
}

internal interface IOpenAiCompatibleChatClient
{
    IAsyncEnumerable<OpenAiCompatibleCompletionStreamUpdate> StreamCompletionAsync(
        ProviderProfile provider,
        OpenAiCompatibleChatRequest request,
        CancellationToken cancellationToken);
}

internal interface ICodeWConversationService
{
    Task<ConversationResult> SendAsync(ConversationRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<ConversationStreamUpdate> StreamAsync(
        ConversationRequest request,
        CancellationToken cancellationToken);
}

internal interface IMcpService
{
    Task<IMcpConversationContext> CreateContextAsync(
        IReadOnlyList<McpServerDefinition> enabledServers,
        string? workingDirectory,
        CancellationToken cancellationToken);
}

internal interface IMcpConversationContext : IAsyncDisposable
{
    IReadOnlyList<McpToolRegistration> Tools { get; }

    IReadOnlyList<string> Warnings { get; }

    IReadOnlyList<string> ServerInstructions { get; }

    Task<McpToolExecutionResult> CallToolAsync(
        string exposedToolName,
        string argumentsJson,
        CancellationToken cancellationToken);
}

internal interface ISkillService
{
    Task<SkillLoadResult> LoadActiveSkillsAsync(
        IReadOnlyList<SkillDefinition> skills,
        string? workingDirectory,
        CancellationToken cancellationToken);

    Task<SkillDiscoveryResult> DiscoverSkillsAsync(
        string? workingDirectory,
        CancellationToken cancellationToken);
}
