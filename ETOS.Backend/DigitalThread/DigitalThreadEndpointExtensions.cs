using System.Text.Json;
using ETOS.Backend.Identity;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.DigitalThread;

public static class DigitalThreadEndpointExtensions
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapEnterpriseThreadDigitalThreadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/digital-thread")
            .RequireAuthorization()
            .WithTags("DigitalThread");

        group.MapGet("/settings", (
            IOptions<DigitalThreadOptions> options) =>
            Results.Ok(new DigitalThreadSettingsResponse(options.Value.UseLiveProjection)));

        group.MapGet("/summary", async (
            int? windowHours,
            IDigitalThreadProjectionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetSummaryAsync(windowHours, cancellationToken)));

        group.MapGet("/systems", async (
            IDigitalThreadProjectionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListSystemsAsync(cancellationToken)));

        group.MapGet("/events", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? systemId,
            int? limit,
            IDigitalThreadProjectionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListEventsAsync(from, to, systemId, limit, cancellationToken)));

        group.MapGet("/branches", async (
            int? windowHours,
            DateTimeOffset? from,
            DateTimeOffset? to,
            IDigitalThreadProjectionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListBranchesAsync(windowHours, from, to, cancellationToken)));

        group.MapGet("/lineage/{artifactId:guid}", async (
            Guid artifactId,
            IDigitalThreadProjectionService service,
            CancellationToken cancellationToken) =>
            await ExecuteNullableAsync(() => service.GetLineageAsync(artifactId, cancellationToken)));

        group.MapGet("/minimap", async (
            int? windowHours,
            IDigitalThreadProjectionService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetMinimapAsync(windowHours, cancellationToken)));

        group.MapGet("/events/stream", async (
            DateTimeOffset? since,
            string? sinceEventId,
            HttpResponse response,
            IDigitalThreadProjectionService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                response.Headers.ContentType = "text/event-stream";
                response.Headers.CacheControl = "no-cache";
                response.Headers.Append("X-Accel-Buffering", "no");

                await foreach (var envelope in service.StreamEventsAsync(since, sinceEventId, cancellationToken))
                {
                    if (envelope.Heartbeat || envelope.Event is null)
                    {
                        await response.WriteAsync($": heartbeat {envelope.Cursor}\n\n", cancellationToken);
                        await response.Body.FlushAsync(cancellationToken);
                        continue;
                    }

                    var payload = JsonSerializer.Serialize(envelope, StreamJsonOptions);
                    await response.WriteAsync($"id: {envelope.Cursor}\n", cancellationToken);
                    await response.WriteAsync("event: digital-thread\n", cancellationToken);
                    await response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected or host shutting down.
            }
            catch (RequestValidationException exception)
            {
                if (!response.HasStarted)
                {
                    response.StatusCode = StatusCodes.Status400BadRequest;
                    await response.WriteAsJsonAsync(new ProblemResponse(exception.Message), cancellationToken);
                }
            }
            catch (TenantAccessDeniedException exception)
            {
                if (!response.HasStarted)
                {
                    response.StatusCode = StatusCodes.Status403Forbidden;
                    await response.WriteAsJsonAsync(
                        new ProblemResponse(exception.Message),
                        cancellationToken);
                }
            }

            // Keep-alive heartbeats while the enumerator delays between polls are handled
            // by the service loop; emit a comment heartbeat on cancel-safe exit only when started.
            if (!cancellationToken.IsCancellationRequested && !response.HasStarted)
            {
                response.StatusCode = StatusCodes.Status204NoContent;
            }
        });

        group.MapGet("/events/{eventId}", async (
            string eventId,
            IDigitalThreadProjectionService service,
            CancellationToken cancellationToken) =>
            await ExecuteNullableAsync(() => service.GetEventDetailAsync(eventId, cancellationToken)));

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

    private static async Task<IResult> ExecuteNullableAsync<TResponse>(Func<Task<TResponse?>> action)
        where TResponse : class
    {
        try
        {
            var response = await action();
            return response is null ? Results.NotFound() : Results.Ok(response);
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
