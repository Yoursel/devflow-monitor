using DevFlowMonitor.Contracts;

namespace DevFlowMonitor.Wpf.ViewModel;

public sealed class AnalyticsTrendPointViewModel
{
    public AnalyticsTrendPointViewModel(AnalyticsTrendPointResponse source)
    {
        Date = source.Date;
        TotalRuns = source.TotalRuns;
        SuccessfulRuns = source.SuccessfulRuns;
        FailedRuns = source.FailedRuns;
        AverageDurationSeconds = source.AverageDurationSeconds;
        PeriodDays = source.PeriodDays;
    }

    public DateOnly Date { get; }
    public int TotalRuns { get; }
    public int SuccessfulRuns { get; }
    public int FailedRuns { get; }
    public int OtherRuns => Math.Max(0, TotalRuns - SuccessfulRuns - FailedRuns);
    public double AverageDurationSeconds { get; }
    public int PeriodDays { get; }
    public string DateLabel => PeriodDays > 31 ? Date.ToString("MM.yy") : Date.ToString("dd.MM");
    public string ToolTipText =>
        $"{PeriodLabel}: всего {TotalRuns}, успешно {SuccessfulRuns}, ошибок {FailedRuns}, среднее {AverageDurationSeconds:F0} сек.";

    private string PeriodLabel => PeriodDays == 1
        ? Date.ToString("dd.MM.yyyy")
        : $"{Date:dd.MM.yyyy}–{Date.AddDays(PeriodDays - 1):dd.MM.yyyy}";
}
