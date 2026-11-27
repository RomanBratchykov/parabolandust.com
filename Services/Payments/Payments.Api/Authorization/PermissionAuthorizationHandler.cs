using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace Payments.Api.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        foreach (var claim in context.User.FindAll("authorization"))
        {
            if (HasPermission(claim.Value, requirement))
            {
                context.Succeed(requirement);
                break;
            }
        }
        return Task.CompletedTask;
    }

    private static bool HasPermission(string value, PermissionRequirement requirement)
    {
        if (value.Length > 65536)
            return false;

        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("permissions", out var permissions) ||
                permissions.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var permission in permissions.EnumerateArray())
            {
                if (permission.ValueKind != JsonValueKind.Object ||
                    !permission.TryGetProperty("rsname", out var resource) ||
                    resource.ValueKind != JsonValueKind.String ||
                    !string.Equals(resource.GetString(), requirement.Resource, StringComparison.Ordinal) ||
                    !permission.TryGetProperty("scopes", out var scopes) ||
                    scopes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var scope in scopes.EnumerateArray())
                {
                    if (scope.ValueKind == JsonValueKind.String &&
                        string.Equals(scope.GetString(), requirement.Permission, StringComparison.Ordinal))
                        return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }
        return false;
    }
}
