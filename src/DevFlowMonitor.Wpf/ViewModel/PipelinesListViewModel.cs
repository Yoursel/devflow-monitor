using System.Collections.ObjectModel;
using DevFlowMonitor.Wpf.Model;

namespace DevFlowMonitor.Wpf.ViewModel;

public class PipelinesListViewModel
{
    private const int PageSize = 5;

    private readonly List<PipelineViewModel> _allPipelines =
    [
        new() { Status = PipelineStatus.Running, Branch = "branch 1", TimeAgo = "2 мин. назад", PipelineName = "test1" },
        new() { Status = PipelineStatus.Success, Branch = "branch 2", TimeAgo = "2 мин. назад", PipelineName = "test2" },
        new() { Status = PipelineStatus.Failed, Branch = "branch 3", TimeAgo = "5 мин. назад", PipelineName = "test3" },
        new() { Status = PipelineStatus.Running, Branch = "branch 4", TimeAgo = "5 мин. назад", PipelineName = "test4" },
        new() { Status = PipelineStatus.Success, Branch = "branch 5", TimeAgo = "10 мин. назад", PipelineName = "test5" },
        new() { Status = PipelineStatus.Failed, Branch = "branch 6", TimeAgo = "15 мин. назад", PipelineName = "test6" },
        new() { Status = PipelineStatus.Running, Branch = "branch 7", TimeAgo = "20 мин. назад", PipelineName = "test7" },
        new() { Status = PipelineStatus.Success, Branch = "branch 8", TimeAgo = "30 мин. назад", PipelineName = "test8" },
        new() { Status = PipelineStatus.Failed, Branch = "branch 9", TimeAgo = "45 мин. назад", PipelineName = "test9" },
        new() { Status = PipelineStatus.Success, Branch = "branch 10", TimeAgo = "1 ч. назад", PipelineName = "test10" },
        new() { Status = PipelineStatus.Running, Branch = "branch 11", TimeAgo = "1 ч. назад", PipelineName = "test11" },
        new() { Status = PipelineStatus.Failed, Branch = "branch 12", TimeAgo = "2 ч. назад", PipelineName = "test12" },
        new() { Status = PipelineStatus.Success, Branch = "branch 13", TimeAgo = "2 ч. назад", PipelineName = "test13" },
        new() { Status = PipelineStatus.Running, Branch = "branch 14", TimeAgo = "3 ч. назад", PipelineName = "test14" },
        new() { Status = PipelineStatus.Failed, Branch = "branch 15", TimeAgo = "3 ч. назад", PipelineName = "test15" },
        new() { Status = PipelineStatus.Success, Branch = "branch 16", TimeAgo = "4 ч. назад", PipelineName = "test16" },
        new() { Status = PipelineStatus.Running, Branch = "branch 17", TimeAgo = "4 ч. назад", PipelineName = "test17" },
        new() { Status = PipelineStatus.Success, Branch = "branch 18", TimeAgo = "5 ч. назад", PipelineName = "test18" },
        new() { Status = PipelineStatus.Failed, Branch = "branch 19", TimeAgo = "5 ч. назад", PipelineName = "test19" },
        new() { Status = PipelineStatus.Success, Branch = "branch 20", TimeAgo = "6 ч. назад", PipelineName = "test20" },
    ];

    public PipelinesListViewModel()
    {
        Pagination = new PaginationViewModel(PageSize, LoadPage);
        Pagination.SetTotalItems(_allPipelines.Count);
    }

    public ObservableCollection<PipelineViewModel> Pipelines { get; } = [];
    public PaginationViewModel Pagination { get; }

    private void LoadPage(int page)
    {
        Pipelines.Clear();

        foreach (var item in _allPipelines.Skip((page - 1) * PageSize).Take(PageSize))
            Pipelines.Add(item);
    }
}