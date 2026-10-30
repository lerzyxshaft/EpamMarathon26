using System.Text.Json;
using Microsoft.Playwright;

namespace Restaurant.Aqa.Pages;

public sealed class LiveOwnerGroupOrderPage(IPage page)
{
    public async Task OpenAsync(
        string frontendUrl,
        string reservationId,
        string idToken,
        string accessToken,
        JsonElement userProfile)
    {
        var authJson = JsonSerializer.Serialize(new { idToken, accessToken });
        var userJson = userProfile.GetRawText();
        await page.AddInitScriptAsync(
            $"() => {{ localStorage.setItem('auth', {JsonSerializer.Serialize(authJson)}); localStorage.setItem('user', {JsonSerializer.Serialize(userJson)}); }}");

        await page.GotoAsync(
            $"{frontendUrl}/reservations/{Uri.EscapeDataString(reservationId)}/group-order");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Group pre-order" }).WaitForAsync();
    }
}
