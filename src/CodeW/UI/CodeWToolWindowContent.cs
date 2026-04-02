namespace CodeW.UI;

using CodeW.Services;
using Microsoft.VisualStudio.Extensibility.UI;

internal sealed class CodeWToolWindowContent : RemoteUserControl
{
    private readonly CodeWToolWindowData data;

    public CodeWToolWindowContent(
        ICodeWConfigurationStore configurationStore,
        ICodeWConversationService conversationService,
        IMcpService mcpService,
        ISkillService skillService)
        : this(new CodeWToolWindowData(configurationStore, conversationService, mcpService, skillService))
    {
    }

    private CodeWToolWindowContent(CodeWToolWindowData data)
        : base(dataContext: data)
    {
        this.data = data;
    }

    public override async Task ControlLoadedAsync(CancellationToken cancellationToken)
    {
        await base.ControlLoadedAsync(cancellationToken);
        await data.LoadAsync(cancellationToken);
    }
}
