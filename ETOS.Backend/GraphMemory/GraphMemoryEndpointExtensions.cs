using ETOS.Backend.Identity;
using ETOS.Backend.Imports;

namespace ETOS.Backend.GraphMemory;

public static class GraphMemoryEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadGraphMemoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/graph")
            .RequireAuthorization()
            .WithTags("Graph Memory");

        group.MapPost("/snapshots", async (
            CaptureGraphSnapshotRequest request,
            ITenantContextResolver tenantContextResolver,
            IGraphSnapshotService service,
            CancellationToken cancellationToken) =>
        {
            var context = await tenantContextResolver.ResolveAsync("graph.snapshots.capture", cancellationToken);
            return await ExecuteAsync(() => service.CaptureAsync(context.TenantId, request.GraphSpace, cancellationToken));
        });

        group.MapGet("/snapshots", async (
            ITenantContextResolver tenantContextResolver,
            IGraphSnapshotService service,
            CancellationToken cancellationToken) =>
        {
            var context = await tenantContextResolver.ResolveAsync("graph.snapshots.list", cancellationToken);
            return await ExecuteAsync(() => service.ListAsync(context.TenantId, cancellationToken));
        });

        group.MapGet("/snapshots/{snapshotId:guid}", async (
            Guid snapshotId,
            ITenantContextResolver tenantContextResolver,
            IGraphSnapshotService service,
            CancellationToken cancellationToken) =>
        {
            var context = await tenantContextResolver.ResolveAsync("graph.snapshots.get", cancellationToken);
            return await ExecuteAsync(() => service.GetAsync(context.TenantId, snapshotId, cancellationToken));
        });

        group.MapPost("/diffs", async (
            CreateGraphDiffRequest request,
            ITenantContextResolver tenantContextResolver,
            IGraphDiffService service,
            CancellationToken cancellationToken) =>
        {
            var context = await tenantContextResolver.ResolveAsync("graph.diffs.create", cancellationToken);
            return await ExecuteAsync(() => service.CreateDiffAsync(context.TenantId, request.FromSnapshotId, request.ToSnapshotId, cancellationToken));
        });

        group.MapGet("/diffs/{diffId:guid}", async (
            Guid diffId,
            ITenantContextResolver tenantContextResolver,
            IGraphDiffService service,
            CancellationToken cancellationToken) =>
        {
            var context = await tenantContextResolver.ResolveAsync("graph.diffs.get", cancellationToken);
            return await ExecuteAsync(() => service.GetAsync(context.TenantId, diffId, cancellationToken));
        });

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            var response = await action();
            return Results.Ok(response);
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

public sealed record CaptureGraphSnapshotRequest(GraphSpace GraphSpace);

public sealed record CreateGraphDiffRequest(Guid FromSnapshotId, Guid ToSnapshotId);
