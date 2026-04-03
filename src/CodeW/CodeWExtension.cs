namespace CodeW;

using CodeW.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;

[VisualStudioContribution]
internal sealed class CodeWExtension : Extension
{
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            id: "CodeW.63B4802C-426B-4DDE-9B10-9F66D0D02855",
            version: new Version(0, 1, 2),
            publisherName: "Code-W",
            displayName: "Code-W",
            description: "在 Visual Studio 中提供多模型 Chat / Agent 工作流，并支持 MCP 与 Skill 扩展能力的 AI 编码助手。")
        {
            Icon = "art/code-w-icon-32.png",
            PreviewImage = "art/code-w-preview-200.png",
            Tags = ["AI", "Chat", "Agent", "MCP", "Skills", "OpenAI", "Kimi", "Qianwen"],
        },
    };

    protected override void InitializeServices(IServiceCollection serviceCollection)
    {
        base.InitializeServices(serviceCollection);

        serviceCollection.AddSingleton(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90),
        });

        serviceCollection.AddSingleton<ICodeWProviderCatalog, CodeWProviderCatalog>();
        serviceCollection.AddSingleton<ICodeWConfigurationStore, CodeWConfigurationStore>();
        serviceCollection.AddSingleton<IMcpService, McpService>();
        serviceCollection.AddSingleton<ISkillService, SkillService>();
        serviceCollection.AddSingleton<IOpenAiCompatibleChatClient, OpenAiCompatibleChatClient>();
        serviceCollection.AddSingleton<ICodeWConversationService, CodeWConversationService>();
    }
}
