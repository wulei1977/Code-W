namespace CodeW.Commands;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

[VisualStudioContribution]
internal sealed class OpenCodeWWindowCommand : Command
{
    public OpenCodeWWindowCommand(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
    }

    public override CommandConfiguration CommandConfiguration => new("%CodeW.OpenWindow.DisplayName%")
    {
        Placements = [CommandPlacement.KnownPlacements.ExtensionsMenu],
    };

    public override Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        => Extensibility.Shell().ShowToolWindowAsync<UI.CodeWToolWindow>(activate: true, cancellationToken);
}
