using ETOS.Backend.Identity;

namespace ETOS.Backend.Classification;

public static class ClassificationEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadClassificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/classification")
            .RequireAuthorization()
            .WithTags("Classification");

        group.MapGet("/schemes", async (IClassificationPolicyService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListSchemesAsync(cancellationToken)));

        group.MapPost("/schemes", async (
            CreateClassificationSchemeRequest request,
            IClassificationPolicyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateSchemeAsync(request, cancellationToken)));

        group.MapGet("/schemes/{schemeId:guid}/versions", async (
            Guid schemeId,
            IClassificationPolicyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListSchemeVersionsAsync(schemeId, cancellationToken)));

        group.MapPost("/schemes/{schemeId:guid}/versions", async (
            Guid schemeId,
            CreateClassificationSchemeVersionRequest request,
            IClassificationPolicyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateSchemeVersionAsync(schemeId, request, cancellationToken)));

        group.MapPost("/schemes/{schemeId:guid}/versions/{versionId:guid}/publish", async (
            Guid schemeId,
            Guid versionId,
            PublishClassificationSchemeVersionRequest request,
            IClassificationPolicyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PublishSchemeVersionAsync(schemeId, versionId, request, cancellationToken)));

        group.MapGet("/policies", async (IClassificationPolicyService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListPolicyVersionsAsync(cancellationToken)));

        group.MapPost("/policies", async (
            CreatePolicyVersionRequest request,
            IClassificationPolicyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreatePolicyVersionAsync(request, cancellationToken)));

        group.MapPost("/policies/{policyVersionId:guid}/publish", async (
            Guid policyVersionId,
            PublishPolicyVersionRequest request,
            IClassificationPolicyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.PublishPolicyVersionAsync(policyVersionId, request, cancellationToken)));

        group.MapGet("/policies/{policyVersionId:guid}/impact", async (
            Guid policyVersionId,
            IClassificationPolicyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.GetPolicyImpactAsync(policyVersionId, cancellationToken)));

        group.MapGet("/rules", async (
            Guid? policyVersionId,
            IClassificationPolicyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListRestrictedRulesAsync(policyVersionId, cancellationToken)));

        group.MapPost("/policies/{policyVersionId:guid}/rules", async (
            Guid policyVersionId,
            CreateRestrictedContextRuleRequest request,
            IClassificationPolicyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AddRestrictedRuleAsync(policyVersionId, request, cancellationToken)));

        group.MapPost("/evaluate", async (
            EvaluatePolicyRequest request,
            IClassificationPolicyService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.EvaluateAsync(request, cancellationToken)));

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
