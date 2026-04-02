namespace CodeW.UI;

using CodeW.Services;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

[VisualStudioContribution]
internal sealed class CodeWToolWindow : ToolWindow
{
    private readonly CodeWToolWindowContent content;

    public CodeWToolWindow(
        VisualStudioExtensibility extensibility,
        ICodeWConfigurationStore configurationStore,
        ICodeWConversationService conversationService,
        IMcpService mcpService,
        ISkillService skillService)
        : base(extensibility)
    {
        Title = "Code-W";
        content = new CodeWToolWindowContent(configurationStore, conversationService, mcpService, skillService);
    }

    public override ToolWindowConfiguration ToolWindowConfiguration => new()
    {
        Placement = ToolWindowPlacement.Floating,
        DockDirection = Dock.Right,
        AllowAutoCreation = false,
    };

    public override Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        => Task.FromResult<IRemoteUserControl>(content);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            content.Dispose();
        }

        base.Dispose(disposing);
    }
}
