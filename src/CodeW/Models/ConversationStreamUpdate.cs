namespace CodeW.Models;

internal abstract record ConversationStreamUpdate;

internal sealed record ConversationStatusUpdate(string Message) : ConversationStreamUpdate;

internal sealed record ConversationAssistantDeltaUpdate(string Text) : ConversationStreamUpdate;

internal sealed record ConversationToolUpdate(
    string ServerName,
    string ToolName,
    string Phase,
    string Details) : ConversationStreamUpdate;

internal sealed record ConversationCompletedUpdate(string StatusMessage) : ConversationStreamUpdate;
