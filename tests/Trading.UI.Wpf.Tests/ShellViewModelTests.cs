using Trading.UI.Wpf.Navigation;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf.Tests;

[TestFixture]
public sealed class ShellViewModelTests
{
    [Test]
    public void Routes_cover_each_stage_seven_feature_area_with_stable_unique_metadata()
    {
        Assert.That(ShellRoute.All.Select(route => route.Title), Is.EqualTo(
            new[] { "Bots", "Portfolios", "Runs", "Research", "Proposals", "Execution", "Risk", "Settings" }));
        Assert.That(ShellRoute.All.Select(route => route.Key), Is.Unique);
        Assert.That(ShellRoute.All.Select(route => route.AutomationId), Is.Unique);
        Assert.That(ShellRoute.All, Has.All.Matches<ShellRoute>(route => route.AutomationId.StartsWith("Nav.", StringComparison.Ordinal)));
    }

    [Test]
    public async Task Navigation_publishes_one_active_route_and_disposes_the_previous_page()
    {
        var factory = new TestPageFactory();
        await using var shell = new ShellViewModel(factory);

        await shell.NavigateAsync(ShellRoute.All[0]);
        await shell.NavigateAsync(ShellRoute.All[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shell.ActiveRoute, Is.EqualTo(ShellRoute.All[1]));
            Assert.That(shell.Content, Is.EqualTo("Portfolios content"));
            Assert.That(shell.IsBusy, Is.False);
            Assert.That(shell.LifetimeStatus, Is.EqualTo("Showing Portfolios"));
            Assert.That(factory.Pages[0].Disposed, Is.True);
            Assert.That(factory.Pages[1].Disposed, Is.False);
        }
    }

    [Test]
    public async Task New_navigation_cancels_obsolete_loading_and_releases_its_page()
    {
        var factory = new TestPageFactory(blockFirst: true);
        await using var shell = new ShellViewModel(factory);
        var obsolete = shell.NavigateAsync(ShellRoute.All[0]);
        await factory.FirstLoadStarted.Task;

        var current = shell.NavigateAsync(ShellRoute.All[1]);
        await Task.WhenAll(obsolete, current);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(factory.Pages[0].CancellationObserved, Is.True);
            Assert.That(factory.Pages[0].Disposed, Is.True);
            Assert.That(shell.ActiveRoute, Is.EqualTo(ShellRoute.All[1]));
        }
    }

    [Test]
    public async Task Load_failure_is_deterministic_and_preserves_the_current_page()
    {
        var factory = new TestPageFactory(failSecond: true);
        await using var shell = new ShellViewModel(factory);
        await shell.NavigateAsync(ShellRoute.All[0]);

        await shell.NavigateAsync(ShellRoute.All[1]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shell.ActiveRoute, Is.EqualTo(ShellRoute.All[0]));
            Assert.That(shell.ErrorMessage, Is.EqualTo("Portfolios could not be loaded. scripted failure"));
            Assert.That(shell.HasError, Is.True);
            Assert.That(shell.LifetimeStatus, Is.EqualTo("Load failed"));
            Assert.That(factory.Pages[1].Disposed, Is.True);
        }
    }

    private sealed class TestPageFactory(bool blockFirst = false, bool failSecond = false) : INavigationPageFactory
    {
        public List<TestPage> Pages { get; } = [];
        public TaskCompletionSource FirstLoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public INavigationPage Create(ShellRoute route)
        {
            var page = new TestPage(
                $"{route.Title} content",
                blockFirst && Pages.Count == 0,
                failSecond && Pages.Count == 1,
                FirstLoadStarted);
            Pages.Add(page);
            return page;
        }
    }

    private sealed class TestPage(
        object content,
        bool block,
        bool fail,
        TaskCompletionSource firstLoadStarted) : INavigationPage
    {
        public object Content { get; } = content;
        public bool CancellationObserved { get; private set; }
        public bool Disposed { get; private set; }

        public async ValueTask LoadAsync(CancellationToken cancellationToken)
        {
            if (fail) throw new InvalidOperationException("scripted failure");
            if (!block) return;
            firstLoadStarted.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved = true;
                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
