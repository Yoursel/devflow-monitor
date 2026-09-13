namespace DevFlowMonitor.Contracts;

public sealed record RepositoryResponse(
    Guid Id,
    string Owner,
    string Name,
    string FullName,
    string DefaultBranch,
    bool IsPrivate);
