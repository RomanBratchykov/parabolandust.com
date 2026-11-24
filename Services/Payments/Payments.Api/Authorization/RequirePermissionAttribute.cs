using Microsoft.AspNetCore.Authorization;

namespace Payments.Api.Authorization;

public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(permission);
