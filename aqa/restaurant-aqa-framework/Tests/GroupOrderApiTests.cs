using System.Net;
using Restaurant.Aqa.Api;
using Restaurant.Aqa.Configuration;

namespace Restaurant.Aqa.Tests;

[NonParallelizable]
public sealed class GroupOrderApiTests
{
    private readonly AqaConfiguration _configuration = AqaConfiguration.Current;
    private GroupOrderScenario? _scenario;

    [SetUp]
    public async Task CreateTestReservationAsync()
    {
        if (!_configuration.HasLiveTestSettings)
        {
            Assert.Ignore("Set AQA_RUN_LIVE_TESTS and the live test account/table/dish settings to run live scenarios.");
        }

        _scenario = GroupOrderScenario.Create();
        await _scenario.InitializeAsync();
    }

    [TearDown]
    public async Task CleanTestReservationAsync()
    {
        if (_scenario is not null)
        {
            await _scenario.DisposeAsync();
            _scenario = null;
        }
    }

    [Test]
    [Category("L1")]
    public async Task OwnerReadsGuestBasketsAndTotals()
    {
        var scenario = Scenario;
        var submitted = await scenario.AddGuestAsync($"Submitted-{Guid.NewGuid():N}"[..16], 2, submit: true);
        await scenario.AddGuestAsync($"Choosing-{Guid.NewGuid():N}"[..16], 1, submit: false);

        var snapshot = await scenario.Api.GetJsonAsync<GroupSnapshot>(
            $"guest/groups/{Uri.EscapeDataString(scenario.GroupReservationId)}/sessions",
            scenario.OwnerTokens.IdToken);

        Assert.That(snapshot.Participants, Has.Length.EqualTo(2));
        var submittedParticipant = snapshot.Participants.Single(participant => participant.Id == submitted.Id);
        Assert.That(submittedParticipant.IsSubmitted, Is.True);
        Assert.That(submittedParticipant.Items, Has.Length.EqualTo(1));
        Assert.That(submittedParticipant.Items[0].Quantity, Is.EqualTo(2));
        Assert.That(snapshot.Total, Is.EqualTo(snapshot.Participants.Sum(participant => participant.Total)));
        Assert.That(snapshot.Participants.Single(participant => participant.Id != submitted.Id).IsSubmitted, Is.False);
    }

    [Test]
    [Category("L1")]
    public async Task GroupSessionsRequireAuthentication()
    {
        var scenario = Scenario;

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Get,
            $"guest/groups/{Uri.EscapeDataString(scenario.GroupReservationId)}/sessions");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Category("L1")]
    public async Task UnknownGroupReturnsNotFound()
    {
        var scenario = Scenario;

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Get,
            $"guest/groups/{Guid.NewGuid()}/sessions",
            bearerToken: scenario.OwnerTokens.IdToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [Category("L1")]
    public async Task OwnerCanReadGroupBeforeAnyGuestJoins()
    {
        var scenario = Scenario;

        var snapshot = await scenario.Api.GetJsonAsync<GroupSnapshot>(
            $"guest/groups/{Uri.EscapeDataString(scenario.GroupReservationId)}/sessions",
            scenario.OwnerTokens.IdToken);

        Assert.That(snapshot.Participants, Is.Empty);
        Assert.That(snapshot.Total, Is.Zero);
    }

    [Test]
    [Category("L1")]
    public async Task NonOwnerCannotReadAnotherReservationGroup()
    {
        if (!_configuration.HasOtherCustomer)
        {
            Assert.Ignore("Set AQA_OTHER_CUSTOMER_EMAIL and AQA_OTHER_CUSTOMER_PASSWORD to run non-owner authorization checks.");
        }

        var scenario = Scenario;
        using var otherCustomerApi = new AqaApiClient();
        var otherCustomer = await otherCustomerApi.SignInAsync(
            _configuration.OtherCustomerEmail,
            _configuration.OtherCustomerPassword);

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Get,
            $"guest/groups/{Uri.EscapeDataString(scenario.GroupReservationId)}/sessions",
            bearerToken: otherCustomer.IdToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [Category("L2")]
    public async Task OwnerSubmitsOnlySubmittedBasketsAndMergesMatchingDishes()
    {
        var scenario = Scenario;
        var submitted = await scenario.AddGuestAsync($"Submitted-{Guid.NewGuid():N}"[..16], 2, submit: true);
        await scenario.AddGuestAsync($"Choosing-{Guid.NewGuid():N}"[..16], 3, submit: false);
        await scenario.CreateOrderAsync();

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Post,
            $"group-reservations/{Uri.EscapeDataString(scenario.GroupReservationId)}/submit",
            new { reservationId = scenario.ReservationId },
            scenario.OwnerTokens.IdToken);
        var items = await scenario.Api.ReadRequiredJsonAsync<List<ConsolidatedItem>>(response);

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].DishId, Is.EqualTo(AqaConfiguration.Current.TestDishId));
        Assert.That(items[0].Quantity, Is.EqualTo(2));

        var status = await scenario.Api.GetJsonAsync<GroupStatus>(
            $"group-reservations/{Uri.EscapeDataString(scenario.GroupReservationId)}/status",
            scenario.OwnerTokens.IdToken);
        Assert.That(status.Status, Is.EqualTo("Submitted"));
        Assert.That(submitted.Id, Is.Not.Empty);
    }

    [Test]
    [Category("L2")]
    public async Task EmptyGuestCartCannotBeSubmitted()
    {
        var scenario = Scenario;
        var created = await scenario.AddEmptyGuestAsync("Empty cart");

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Post,
            $"guest/sessions/{Uri.EscapeDataString(created.Id)}/submit",
            new { },
            created.SessionToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [Category("L2")]
    public async Task GroupCannotBeSubmittedWithoutSubmittedGuests()
    {
        var scenario = Scenario;
        await scenario.AddGuestAsync($"Choosing-{Guid.NewGuid():N}"[..16], 1, submit: false);
        await scenario.CreateOrderAsync();

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Post,
            $"group-reservations/{Uri.EscapeDataString(scenario.GroupReservationId)}/submit",
            new { reservationId = scenario.ReservationId },
            scenario.OwnerTokens.IdToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [Category("L2")]
    public async Task GroupCannotBeSubmittedWhenOrderIsMissing()
    {
        var scenario = Scenario;
        await scenario.AddGuestAsync($"Submitted-{Guid.NewGuid():N}"[..16], 1, submit: true);

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Post,
            $"group-reservations/{Uri.EscapeDataString(scenario.GroupReservationId)}/submit",
            new { reservationId = scenario.ReservationId },
            scenario.OwnerTokens.IdToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [Category("L2")]
    public async Task SubmittedGroupCannotBeSubmittedTwice()
    {
        var scenario = Scenario;
        await scenario.AddGuestAsync($"Submitted-{Guid.NewGuid():N}"[..16], 1, submit: true);
        await scenario.CreateOrderAsync();

        var path = $"group-reservations/{Uri.EscapeDataString(scenario.GroupReservationId)}/submit";
        using var firstResponse = await scenario.Api.SendAsync(
            HttpMethod.Post,
            path,
            new { reservationId = scenario.ReservationId },
            scenario.OwnerTokens.IdToken);
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var secondResponse = await scenario.Api.SendAsync(
            HttpMethod.Post,
            path,
            new { reservationId = scenario.ReservationId },
            scenario.OwnerTokens.IdToken);
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [Category("L2")]
    public async Task NonOwnerCannotSubmitTheGroupOrder()
    {
        if (!_configuration.HasOtherCustomer)
        {
            Assert.Ignore("Set AQA_OTHER_CUSTOMER_EMAIL and AQA_OTHER_CUSTOMER_PASSWORD to run non-owner authorization checks.");
        }

        var scenario = Scenario;
        await scenario.AddGuestAsync($"Submitted-{Guid.NewGuid():N}"[..16], 1, submit: true);
        await scenario.CreateOrderAsync();
        using var otherCustomerApi = new AqaApiClient();
        var otherCustomer = await otherCustomerApi.SignInAsync(
            _configuration.OtherCustomerEmail,
            _configuration.OtherCustomerPassword);

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Post,
            $"group-reservations/{Uri.EscapeDataString(scenario.GroupReservationId)}/submit",
            new { reservationId = scenario.ReservationId },
            otherCustomer.IdToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [Category("L2")]
    public async Task UnknownGroupCannotBeSubmitted()
    {
        var scenario = Scenario;
        await scenario.CreateOrderAsync();

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Post,
            $"group-reservations/{Guid.NewGuid()}/submit",
            new { reservationId = scenario.ReservationId },
            scenario.OwnerTokens.IdToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [Category("L3")]
    public async Task DeletingParticipantReturnsUpdatedSnapshotAndZeroForLastParticipant()
    {
        var scenario = Scenario;
        var removed = await scenario.AddGuestAsync($"Removed-{Guid.NewGuid():N}"[..16], 2, submit: true);
        var remaining = await scenario.AddGuestAsync($"Remaining-{Guid.NewGuid():N}"[..16], 1, submit: false);

        var afterFirstDelete = await scenario.Api.SendJsonAsync<GroupSnapshot>(
            HttpMethod.Delete,
            $"guest/groups/{Uri.EscapeDataString(scenario.GroupReservationId)}/sessions/{Uri.EscapeDataString(removed.Id)}",
            new { },
            scenario.OwnerTokens.IdToken);
        scenario.ForgetGuest(removed);

        Assert.That(afterFirstDelete.Participants, Has.Length.EqualTo(1));
        Assert.That(afterFirstDelete.Participants[0].Id, Is.EqualTo(remaining.Id));
        Assert.That(afterFirstDelete.Total, Is.EqualTo(afterFirstDelete.Participants[0].Total));

        var afterLastDelete = await scenario.Api.SendJsonAsync<GroupSnapshot>(
            HttpMethod.Delete,
            $"guest/groups/{Uri.EscapeDataString(scenario.GroupReservationId)}/sessions/{Uri.EscapeDataString(remaining.Id)}",
            new { },
            scenario.OwnerTokens.IdToken);
        scenario.ForgetGuest(remaining);

        Assert.That(afterLastDelete.Participants, Is.Empty);
        Assert.That(afterLastDelete.Total, Is.Zero);
    }

    [Test]
    [Category("L3")]
    public async Task DeletingUnknownParticipantReturnsNotFound()
    {
        var scenario = Scenario;

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Delete,
            $"guest/groups/{Uri.EscapeDataString(scenario.GroupReservationId)}/sessions/{Guid.NewGuid()}",
            new { },
            scenario.OwnerTokens.IdToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [Category("L3")]
    public async Task ParticipantFromAnotherGroupCannotBeDeleted()
    {
        var scenario = Scenario;
        await using var otherScenario = GroupOrderScenario.Create();
        await otherScenario.InitializeAsync();
        var foreignGuest = await otherScenario.AddGuestAsync($"Foreign-{Guid.NewGuid():N}"[..16], 1, submit: false);

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Delete,
            $"guest/groups/{Uri.EscapeDataString(scenario.GroupReservationId)}/sessions/{Uri.EscapeDataString(foreignGuest.Id)}",
            new { },
            scenario.OwnerTokens.IdToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [Category("L3")]
    public async Task DeletingParticipantRequiresAuthentication()
    {
        var scenario = Scenario;
        var guest = await scenario.AddGuestAsync($"Guest-{Guid.NewGuid():N}"[..16], 1, submit: false);

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Delete,
            $"guest/groups/{Uri.EscapeDataString(scenario.GroupReservationId)}/sessions/{Uri.EscapeDataString(guest.Id)}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Category("L3")]
    public async Task NonOwnerCannotDeleteParticipant()
    {
        if (!_configuration.HasOtherCustomer)
        {
            Assert.Ignore("Set AQA_OTHER_CUSTOMER_EMAIL and AQA_OTHER_CUSTOMER_PASSWORD to run non-owner authorization checks.");
        }

        var scenario = Scenario;
        var guest = await scenario.AddGuestAsync($"Guest-{Guid.NewGuid():N}"[..16], 1, submit: false);
        using var otherCustomerApi = new AqaApiClient();
        var otherCustomer = await otherCustomerApi.SignInAsync(
            _configuration.OtherCustomerEmail,
            _configuration.OtherCustomerPassword);

        using var response = await scenario.Api.SendAsync(
            HttpMethod.Delete,
            $"guest/groups/{Uri.EscapeDataString(scenario.GroupReservationId)}/sessions/{Uri.EscapeDataString(guest.Id)}",
            bearerToken: otherCustomer.IdToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    private GroupOrderScenario Scenario =>
        _scenario ?? throw new InvalidOperationException("The test reservation was not initialized.");
    private sealed record Participant(string Id, decimal Total, bool IsSubmitted, ParticipantItem[] Items);
    private sealed record ParticipantItem(int Quantity);
    private sealed record GroupSnapshot(Participant[] Participants, decimal Total);
    private sealed record ConsolidatedItem(string DishId, int Quantity);
    private sealed record GroupStatus(string Status);
}
