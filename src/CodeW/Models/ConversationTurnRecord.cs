namespace CodeW.Models;

internal sealed class ConversationTurnRecord
{
    public required string Role { get; init; }

    public required string Content { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
