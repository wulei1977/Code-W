namespace CodeW.Models;

internal sealed class ConversationRequest
{
    public required CodeWConfiguration Configuration { get; init; }

    public required ConversationMode Mode { get; init; }

    public required string Prompt { get; init; }

    public string? SelectedPath { get; init; }

    public string? WorkingDirectory { get; init; }

    public IReadOnlyList<ConversationTurnRecord> History { get; init; } = [];
}
