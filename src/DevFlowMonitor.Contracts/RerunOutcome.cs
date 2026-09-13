namespace DevFlowMonitor.Contracts;

public enum RerunOutcome
{
    NotRetried,
    SucceededAfterRetry,
    StillFailingAfterRetry,
    RetriedWithOtherOutcome
}
