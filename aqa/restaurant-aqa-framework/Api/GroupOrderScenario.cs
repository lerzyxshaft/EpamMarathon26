using System.Globalization;
using System.Text.Json;
using Restaurant.Aqa.Configuration;

namespace Restaurant.Aqa.Api;

public sealed class GroupOrderScenario : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AqaConfiguration _configuration = AqaConfiguration.Current;
    private readonly List<GuestSession> _guests = [];
    private bool _reservationCreated;
    private bool _orderCreated;

    private GroupOrderScenario(AqaApiClient api)
    {
        Api = api;
    }

    public AqaApiClient Api { get; }
    public SignInResponse OwnerTokens { get; private set; } = null!;
    public string OwnerId { get; private set; } = string.Empty;
    public string ReservationId { get; private set; } = string.Empty;
    public string GroupReservationId { get; private set; } = string.Empty;
    public JsonElement OwnerProfile { get; private set; }
    public IReadOnlyList<GuestSession> Guests => _guests;

    public static GroupOrderScenario Create() => new(new AqaApiClient());

    public async Task InitializeAsync()
    {
        OwnerTokens = await Api.SignInAsync(_configuration.OwnerEmail, _configuration.OwnerPassword);
        OwnerProfile = await Api.GetJsonAsync<JsonElement>("me", OwnerTokens.IdToken);
        OwnerId = OwnerProfile.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("The authenticated owner ID was missing.");

        var slot = await FindAvailableSlotAsync();
        var reservation = await Api.SendJsonAsync<ReservationResponse>(
            HttpMethod.Post,
            "reservations",
            new
            {
                tableId = _configuration.TestTableId,
                customerEmail = _configuration.OwnerEmail,
                customerId = OwnerId,
                startTime = slot.Start,
                endTime = slot.End,
                guestCount = 4
            },
            OwnerTokens.IdToken);
        ReservationId = reservation.Id;
        _reservationCreated = true;

        var invitation = await Api.GetJsonAsync<InviteTokenResponse>(
            $"reservations/{Uri.EscapeDataString(ReservationId)}/invite-token",
            OwnerTokens.IdToken);
        var group = await Api.GetJsonAsync<GroupInvitationResponse>(
            $"guest/invites/{Uri.EscapeDataString(invitation.Token)}");
        GroupReservationId = group.GroupReservationId;

        await Api.GetJsonAsync<JsonElement>(
            $"reservations/{Uri.EscapeDataString(ReservationId)}/guest-session-id",
            OwnerTokens.IdToken);
    }

    public async Task<GuestSession> AddGuestAsync(string name, int quantity, bool submit)
    {
        var guest = await CreateGuestAsync(name);

        await Api.SendJsonAsync<JsonElement>(
            HttpMethod.Post,
            $"guest/sessions/{Uri.EscapeDataString(guest.Id)}/dishes",
            new { dishId = _configuration.TestDishId, quantity },
            guest.SessionToken);

        if (submit)
        {
            using var response = await Api.SendAsync(
                HttpMethod.Post,
                $"guest/sessions/{Uri.EscapeDataString(guest.Id)}/submit",
                new { },
                guest.SessionToken);
            await Api.ReadRequiredJsonAsync<JsonElement>(response);
        }

        return guest;
    }

    public async Task<GuestSession> AddEmptyGuestAsync(string name)
    {
        return await CreateGuestAsync(name);
    }

    public async Task CreateOrderAsync()
    {
        await Api.SendJsonAsync<JsonElement>(
            HttpMethod.Post,
            "orders",
            new
            {
                reservationId = ReservationId,
                orderItems = new[] { new { dishId = _configuration.TestDishId, quantity = 1 } }
            },
            OwnerTokens.IdToken);
        _orderCreated = true;
    }

    public async Task DeleteGuestAsync(GuestSession guest)
    {
        using var response = await Api.SendAsync(
            HttpMethod.Delete,
            $"guest/groups/{Uri.EscapeDataString(GroupReservationId)}/sessions/{Uri.EscapeDataString(guest.Id)}",
            bearerToken: OwnerTokens.IdToken);
        await Api.ReadRequiredJsonAsync<JsonElement>(response);
        _guests.Remove(guest);
    }

    public void ForgetGuest(GuestSession guest) => _guests.Remove(guest);

    public async ValueTask DisposeAsync()
    {
        foreach (var guest in _guests.ToArray())
        {
            await DeleteGuestAsync(guest);
        }

        if (_orderCreated)
        {
            using var response = await Api.SendAsync(
                HttpMethod.Delete,
                $"orders/{Uri.EscapeDataString(ReservationId)}",
                bearerToken: OwnerTokens.IdToken);
            await Api.ReadRequiredJsonAsync<JsonElement>(response);
        }

        if (_reservationCreated)
        {
            using var response = await Api.SendAsync(
                HttpMethod.Delete,
                "reservations",
                new { id = ReservationId },
                OwnerTokens.IdToken);
            await Api.ReadRequiredJsonAsync<JsonElement>(response);
        }

        Api.Dispose();
    }

    private async Task<TimeSlot> FindAvailableSlotAsync()
    {
        for (var day = 1; day <= 14; day++)
        {
            var date = DateTimeOffset.UtcNow.AddDays(day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var slots = await Api.GetJsonAsync<List<TimeSlot>>(
                $"tables/{Uri.EscapeDataString(_configuration.TestTableId)}/available-time-slots?startTime={date}T00%3A00%3A00Z",
                OwnerTokens.IdToken);
            var slot = slots.FirstOrDefault(candidate => candidate.Start > DateTimeOffset.UtcNow.AddMinutes(15));
            if (slot is not null)
            {
                return slot;
            }
        }

        throw new InvalidOperationException(
            "No available reservation slot was returned for the configured test table in the next 14 days.");
    }

    private async Task<GuestSession> CreateGuestAsync(string name)
    {
        var created = await Api.SendJsonAsync<CreateGuestSessionResponse>(
            HttpMethod.Post,
            "guest/sessions",
            new { groupReservationId = GroupReservationId, guestName = name });
        var guest = new GuestSession(created.SessionId, created.SessionToken, name);
        _guests.Add(guest);
        return guest;
    }

    private sealed record CreateGuestSessionResponse(string SessionId, string SessionToken);
    private sealed record InviteTokenResponse(string Token);
    private sealed record GroupInvitationResponse(string GroupReservationId);
    private sealed record ReservationResponse(string Id);
    private sealed record TimeSlot(DateTimeOffset Start, DateTimeOffset End);
}

public sealed record GuestSession(string Id, string SessionToken, string Name);
