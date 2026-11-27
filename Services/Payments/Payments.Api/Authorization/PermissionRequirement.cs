using Microsoft.AspNetCore.Authorization;

namespace Payments.Api.Authorization;

public sealed record PermissionRequirement(string Resource, string Permission) : IAuthorizationRequirement;
