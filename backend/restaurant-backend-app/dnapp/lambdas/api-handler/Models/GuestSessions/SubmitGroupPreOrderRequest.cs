using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace APIHandler.Models.GuestSessions;

public class SubmitGroupPreOrderRequest
{
    [Required]
    public string ReservationId { get; set; } = string.Empty;
}

public record GetOwnerGuestSessionIdResponse(
    [property: JsonPropertyName("guestSessionId")] string GuestSessionId);

public record ConsolidatedOrderItemResponse(
    [property: JsonPropertyName("dishId")] string DishId,
    [property: JsonPropertyName("quantity")] int Quantity);
