using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;

namespace ETOS.Backend.Explorers;

public static class ExplorerEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadExplorerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/explorers")
            .RequireAuthorization()
            .WithTags("Explorers");

        group.MapGet("/context-view", async (
            ContextViewAnchorKind anchorKind,
            string anchorId,
            string? policyKey,
            IContextViewService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetContextViewAsync(anchorKind, anchorId, policyKey, cancellationToken)));

        group.MapGet("/governance-flow", async (
            ContextViewAnchorKind anchorKind,
            string anchorId,
            IGovernanceFlowService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.BuildFlowAsync(anchorKind, anchorId, cancellationToken)));

        group.MapGet("/graph/nodes", async (
            GraphSpace? graphSpace,
            TrustState? trustState,
            string? objectType,
            int? limit,
            string? policyKey,
            IGraphExplorerService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListNodesAsync(graphSpace, trustState, objectType, limit, policyKey, cancellationToken)));

        group.MapGet("/graph/nodes/{nodeId:guid}", async (
            Guid nodeId,
            string? policyKey,
            IGraphExplorerService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetNodeAsync(nodeId, policyKey, cancellationToken)));

        group.MapGet("/graph/nodes/{nodeId:guid}/relationships", async (
            Guid nodeId,
            string? direction,
            string? policyKey,
            IGraphExplorerService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListRelationshipsAsync(nodeId, direction, policyKey, cancellationToken)));

        group.MapGet("/context-packages", async (
            IContextPackageExplorerService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListPackagesAsync(cancellationToken)));

        group.MapGet("/context-packages/{packageId:guid}", async (
            Guid packageId,
            IContextPackageExplorerService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetPackageAsync(packageId, cancellationToken)));

        group.MapGet("/decisions", async (
            string? status,
            string? participant,
            string? search,
            IDecisionExplorerFoundationService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListDecisionsAsync(status, participant, search, cancellationToken)));

        group.MapGet("/artifacts", async (
            string? artifactType,
            string? lifecycleState,
            string? search,
            IArtifactExplorerService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListArtifactsAsync(artifactType, lifecycleState, search, cancellationToken)));

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
