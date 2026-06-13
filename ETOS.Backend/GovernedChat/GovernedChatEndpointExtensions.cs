using ETOS.Backend.Identity;

namespace ETOS.Backend.GovernedChat;

public static class GovernedChatEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadGovernedChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/governed-chat")
            .RequireAuthorization()
            .WithTags("Governed Chat");

        group.MapPost("/sessions", async (
            CreateGovernedChatSessionRequest request,
            IGovernedChatService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateSessionAsync(request, cancellationToken)));

        group.MapGet("/sessions", async (
            IGovernedChatService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListSessionsAsync(cancellationToken)));

        group.MapGet("/sessions/{sessionId:guid}", async (
            Guid sessionId,
            IGovernedChatService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetSessionAsync(sessionId, cancellationToken)));

        group.MapPost("/sessions/{sessionId:guid}/turns", async (
            Guid sessionId,
            CreateGovernedChatTurnRequest request,
            IGovernedChatService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AskAsync(sessionId, request, cancellationToken)));

        group.MapGet("/turns/{turnId:guid}", async (
            Guid turnId,
            IGovernedChatService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetTurnAsync(turnId, cancellationToken)));

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (RequestValidationException exception)
        {
            return Results.BadRequest(new ProblemResponse(exception.Message));
        }
        catch (TenantAccessDeniedException exception)
        {
            return Results.Problem(
                title: "Forbidden",
                detail: exception.Message,
                statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
