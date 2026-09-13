namespace DevFlowMonitor.Contracts;

public sealed record WorkflowResponse(
    Guid Id,
    Guid RepositoryId,
    string Name,
    string? Path,
    string State);
