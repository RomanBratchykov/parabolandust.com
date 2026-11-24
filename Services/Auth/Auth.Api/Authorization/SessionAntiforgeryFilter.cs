using Microsoft.AspNetCore.Antiforgery;

namespace Auth.Api.Authorization;

public sealed class SessionAntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid anti-forgery token.");
        }

        return await next(context);
    }
}
