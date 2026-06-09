namespace ETOS.Backend.Identity;

public static class IdentityEndpointExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseThreadIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/identity")
            .RequireAuthorization()
            .WithTags("Identity");

        group.MapGet("/tenants", async (IIdentityAdminService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListTenantsAsync(cancellationToken)));

        group.MapPost("/tenants", async (CreateTenantRequest request, IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateTenantAsync(request, cancellationToken)));

        group.MapGet("/users", async (IIdentityAdminService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListUsersAsync(cancellationToken)));

        group.MapPost("/users", async (CreateUserRequest request, IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateUserAsync(request, cancellationToken)));

        group.MapGet("/permissions", async (IIdentityAdminService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListPermissionsAsync(cancellationToken)));

        group.MapPost("/permissions", async (CreatePermissionRequest request, IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreatePermissionAsync(request, cancellationToken)));

        group.MapGet("/roles", async (IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListRolesAsync(cancellationToken)));

        group.MapPost("/roles", async (CreateTenantRoleRequest request, IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateRoleAsync(request, cancellationToken)));

        group.MapPost("/roles/{roleId:guid}/permissions", async (
            Guid roleId,
            AssignRolePermissionRequest request,
            IIdentityAdminService service,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.AssignRolePermissionAsync(roleId, request, cancellationToken)));

        group.MapGet("/memberships", async (IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListMembershipsAsync(cancellationToken)));

        group.MapPost("/memberships", async (CreateTenantMembershipRequest request, IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateMembershipAsync(request, cancellationToken)));

        group.MapGet("/grants", async (IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListGrantsAsync(cancellationToken)));

        group.MapPost("/grants", async (CreateAccessGrantRequest request, IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateGrantAsync(request, cancellationToken)));

        group.MapGet("/access-requests", async (IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.ListAccessRequestsAsync(cancellationToken)));

        group.MapPost("/access-requests", async (CreateAccessRequestRequest request, IIdentityAdminService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(() => service.CreateAccessRequestAsync(request, cancellationToken)));

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
