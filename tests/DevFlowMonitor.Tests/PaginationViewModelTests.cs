using DevFlowMonitor.Wpf.ViewModel;

namespace DevFlowMonitor.Tests;

public class PaginationViewModelTests
{
    [Fact]
    public async Task NextAndPreviousCommands_NavigateBetweenAdjacentPages()
    {
        var loadedPages = new List<int>();
        var viewModel = new PaginationViewModel(
            pageSize: 5,
            page =>
            {
                loadedPages.Add(page);
                return Task.CompletedTask;
            });
        viewModel.SetTotalItems(totalItems: 10, currentPage: 1);

        viewModel.GoToNextPageCommand.Execute(null);
        await WaitUntil(() => loadedPages.Count == 1);

        Assert.Equal([2], loadedPages);
        Assert.Equal("Показано 6-10 из 10", viewModel.ItemRangeInfo);
        Assert.False(viewModel.GoToNextPageCommand.CanExecute(null));
        Assert.True(viewModel.GoToPreviousPageCommand.CanExecute(null));

        viewModel.GoToPreviousPageCommand.Execute(null);
        await WaitUntil(() => loadedPages.Count == 2);

        Assert.Equal([2, 1], loadedPages);
        Assert.Equal("Показано 1-5 из 10", viewModel.ItemRangeInfo);
        Assert.True(viewModel.GoToNextPageCommand.CanExecute(null));
        Assert.False(viewModel.GoToPreviousPageCommand.CanExecute(null));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met in time.");
    }
}
