using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using System;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

public class SubmitGroupPreOrderHandler(IGuestSessionService guestSessionService) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out string? groupReservationId))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Missing groupReservationId in path parameters.", context.Logger);
        }

        if (!RequestBodyHelper.TryParseAndValidate<SubmitGroupPreOrderRequest>(request, context, out var submitRequest, out var errorResponse))
        {
            return errorResponse;
        }

        var userId = UserContextHelper.GetAuthenticatedUserId(request);
        if (string.IsNullOrEmpty(userId))
        {
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);
        }

        try
        {
            var dishes = await guestSessionService.SubmitGroupPreOrderAsync(
                userId,
                groupReservationId,
                submitRequest.ReservationId);
            return ResponseBuilder.CreateSuccessResponse(dishes);
        }
        catch (GroupReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (ReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (ActionForbiddenException ex)
        {
            return ResponseBuilder.CreateErrorResponse(403, ex.Message, context.Logger);
        }
        catch (ValidationException ex)
        {
            return ResponseBuilder.CreateErrorResponse(400, ex.Message, context.Logger);
        }
        catch (Exception ex)
        {
            return ResponseBuilder.CreateErrorResponse(500, $"Internal server error: {ex.Message}", context.Logger);
        }
    }
}
