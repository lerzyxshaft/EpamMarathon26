using Microsoft.Playwright;
using Restaurant.Aqa.Configuration;
using Restaurant.Aqa.Pages;

namespace Restaurant.Aqa.Tests;

[Category("L1")]
[Category("L2")]
public sealed class DemoGroupOrderTests : UiTestBase
{
    private OwnerGroupOrderPage _groupOrder = null!;

    [SetUp]
    public async Task OpenGroupOrderAsync()
    {
        _groupOrder = new OwnerGroupOrderPage(Page);
        await _groupOrder.OpenDemoAsync(AqaConfiguration.Current.FrontendUrl);
    }

    [Test]
    public async Task OwnerSeesGuestBasketsAndPerGuestTotals()
    {
        Assert.That(await _groupOrder.Participants.GetByRole(AriaRole.Article).CountAsync(), Is.EqualTo(3));
        Assert.That(await _groupOrder.Participants.GetByRole(AriaRole.Heading, new() { Name = "Andrii" }).IsVisibleAsync(), Is.True);
        Assert.That(await _groupOrder.Participants.GetByRole(AriaRole.Heading, new() { Name = "Olena" }).IsVisibleAsync(), Is.True);
        Assert.That(await _groupOrder.Participants.GetByRole(AriaRole.Heading, new() { Name = "Maksym" }).IsVisibleAsync(), Is.True);

        Assert.That(await _groupOrder.Participants.GetByText("$580.00").IsVisibleAsync(), Is.True);
        Assert.That(await _groupOrder.Participants.GetByText("$460.00").IsVisibleAsync(), Is.True);
        Assert.That(await _groupOrder.Participants.GetByText("Choosing").IsVisibleAsync(), Is.True);
        Assert.That(await _groupOrder.Participants.GetByText("No dishes added yet").CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task OwnerSubmitsOnlySubmittedBasketsAsOneOrder()
    {
        Assert.That(await _groupOrder.Summary.GetByText("2 × $180.00").IsVisibleAsync(), Is.True);
        Assert.That(await _groupOrder.Summary.GetByText("2 × $220.00").IsVisibleAsync(), Is.True);
        Assert.That(await _groupOrder.Summary.GetByText("3 × $180.00").CountAsync(), Is.EqualTo(0));
        Assert.That(await _groupOrder.Summary.GetByText("$1,040.00").IsVisibleAsync(), Is.True);

        await _groupOrder.SendOrderAsync();

        await Page.GetByRole(AriaRole.Heading, new() { Name = "Group order sent!" }).WaitForAsync();
        Assert.That(await Page.GetByRole(AriaRole.Button, new() { Name = "Order sent" }).IsDisabledAsync(), Is.True);
    }
}
