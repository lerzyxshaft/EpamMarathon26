using Microsoft.Playwright.NUnit;

namespace Restaurant.Aqa.Tests;

public abstract class UiTestBase : PageTest
{
    private bool _traceStarted;

    [SetUp]
    public async Task StartTraceAsync()
    {
        await Context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });
        _traceStarted = true;
    }

    [TearDown]
    public async Task SaveFailureArtifactsAsync()
    {
        if (!_traceStarted)
        {
            return;
        }

        var failed = TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed;
        var safeName = string.Concat(TestContext.CurrentContext.Test.Name.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var artifactRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../test-artifacts"));
        var artifactDirectory = Path.Combine(artifactRoot, safeName);

        if (failed)
        {
            Directory.CreateDirectory(artifactDirectory);
            var screenshot = Path.Combine(artifactDirectory, "failure.png");
            await Page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
            TestContext.AddTestAttachment(screenshot, "Failure screenshot");
            await Context.Tracing.StopAsync(new()
            {
                Path = Path.Combine(artifactDirectory, "trace.zip")
            });
        }
        else
        {
            await Context.Tracing.StopAsync();
        }
    }
}
