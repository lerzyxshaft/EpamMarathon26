using Microsoft.Playwright;

namespace Restaurant.Aqa.Pages;

public sealed class OwnerGroupOrderPage(IPage page)
{
    public ILocator Participants => page.GetByRole(AriaRole.Region, new() { Name = "Guests’ choices" });
    public ILocator Summary => page.GetByRole(AriaRole.Complementary, new() { Name = "Combined order" });
    public ILocator SubmitButton => page.GetByRole(AriaRole.Button, new() { Name = "Send group order" });

    public async Task OpenDemoAsync(string frontendUrl)
    {
        await page.GotoAsync($"{frontendUrl}/reservations/demo-reservation/group-order?demo");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Group pre-order" }).WaitForAsync();
    }

    public ILocator Participant(string name) =>
        Participants.GetByRole(AriaRole.Article).Filter(new()
        {
            Has = page.GetByRole(AriaRole.Heading, new() { Name = name })
        });

    public async Task SendOrderAsync()
    {
        await SubmitButton.ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(
            AriaRole.Button,
            new() { Name = "Confirm order" }).ClickAsync();
    }
}
