namespace CodeW.Models;

internal abstract record OpenAiCompatibleCompletionStreamUpdate;

internal sealed record OpenAiCompatibleTextDeltaUpdate(string Text) : OpenAiCompatibleCompletionStreamUpdate;

internal sealed record OpenAiCompatibleToolCallDeltaUpdate(
    int Index,
    string? ToolCallId,
    string? FunctionName,
    string ArgumentsDelta) : OpenAiCompatibleCompletionStreamUpdate;

internal sealed record OpenAiCompatibleCompletionFinishedUpdate(
    OpenAiCompatibleMessage AssistantMessage,
    string? FinishReason) : OpenAiCompatibleCompletionStreamUpdate;
