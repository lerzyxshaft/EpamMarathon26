using Microsoft.Playwright;
using Restaurant.Aqa.Api;
using Restaurant.Aqa.Configuration;
using Restaurant.Aqa.Pages;

namespace Restaurant.Aqa.Tests;

[Category("L1")]
[Category("L2")]
[NonParallelizable]
public sealed class LiveOwnerGroupOrderTests : UiTestBase
{
    [Test]
    public async Task OwnerReviewsAndSubmitsTheLiveGroupOrder()
    {
        var configuration = AqaConfiguration.Current;
        if (!configuration.HasLiveTestSettings)
        {
            Assert.Ignore("Set AQA_RUN_LIVE_TESTS and the live test account/table/dish settings to run live scenarios.");
        }

        await using var scenario = GroupOrderScenario.Create();
        await scenario.InitializeAsync();
        var submittedGuest = await scenario.AddGuestAsync($"Submitted-{Guid.NewGuid():N}"[..16], 2, submit: true);
        await scenario.AddGuestAsync($"Choosing-{Guid.NewGuid():N}"[..16], 1, submit: false);
        await scenario.CreateOrderAsync();

        var groupOrder = new LiveOwnerGroupOrderPage(Page);
        await groupOrder.OpenAsync(
            configuration.FrontendUrl,
            scenario.ReservationId,
            scenario.OwnerTokens.IdToken,
            scenario.OwnerTokens.AccessToken,
            scenario.OwnerProfile);

        await Page.GetByRole(AriaRole.Heading, new() { Name = submittedGuest.Name }).WaitForAsync();
        Assert.That(await Page.GetByRole(AriaRole.Region, new() { Name = "Guests’ choices" })
            .GetByRole(AriaRole.Article).CountAsync(), Is.EqualTo(2));
        Assert.That(await Page.GetByText("Live order").IsVisibleAsync(), Is.True);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Send group order" }).ClickAsync();
        await Page.GetByRole(AriaRole.Dialog)
            .GetByRole(AriaRole.Button, new() { Name = "Confirm order" })
            .ClickAsync();

        await Page.GetByRole(AriaRole.Heading, new() { Name = "Group order sent!" }).WaitForAsync();
        var status = await scenario.Api.GetJsonAsync<GroupReservationStatusResponse>(
            $"group-reservations/{Uri.EscapeDataString(scenario.GroupReservationId)}/status",
            scenario.OwnerTokens.IdToken);
        Assert.That(status.Status, Is.EqualTo("Submitted"));
    }

    private sealed record GroupReservationStatusResponse(string Status);
}
