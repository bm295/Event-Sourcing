namespace EventSourcingBankAccountWeb.Models;

public sealed record ExecuteCommandRequest(
    string CommandType,
    decimal? Amount,
    string? OwnerName);

public sealed record ExecuteCommandResponse(
    bool Success,
    string? Error,
    DemoState State,
    IReadOnlyList<string> NewEventIds);

public sealed record DemoState(
    string StreamId,
    AccountState Account,
    IReadOnlyList<ComponentDescriptor> Components,
    IReadOnlyList<EventRecord> Events,
    IReadOnlyList<FlowStep> LatestFlow,
    IReadOnlyList<string> ActiveComponentIds,
    IReadOnlyList<string> ActiveConnectionIds,
    DateTimeOffset LastUpdatedUtc);

public sealed record AccountState(
    bool IsOpen,
    decimal Balance,
    IReadOnlyList<TransactionHistoryItemDto> History);

public sealed record TransactionHistoryItemDto(
    int SequenceNumber,
    string EventType,
    string Description,
    DateTimeOffset CreatedAtUtc);

public sealed record ComponentDescriptor(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<string> Emits,
    IReadOnlyList<string> Consumes);

public sealed record EventRecord(
    string EventId,
    string StreamId,
    int SequenceNumber,
    string EventType,
    DateTimeOffset CreatedAtUtc,
    EventStatus Status,
    IReadOnlyList<EventStatus> StatusHistory,
    string Payload);

public enum EventStatus
{
    New,
    Persisted,
    Projected,
    Replayed
}

public sealed record FlowStep(
    string Title,
    string SourceComponentId,
    string? TargetComponentId,
    string Detail);
