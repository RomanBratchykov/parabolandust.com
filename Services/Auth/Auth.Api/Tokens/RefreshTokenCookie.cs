namespace Auth.Api.Tokens;

public static class RefreshTokenCookie
{
    public const string Name = "__Secure-parabolandust-refresh";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, RefreshTokenGrant grant)
    {
        var options = CreateOptions();
        options.Expires = grant.ExpiresAt;
        response.Cookies.Append(Name, grant.Token, options);
    }

    public static void Clear(HttpResponse response) => response.Cookies.Delete(Name, CreateOptions());

    private static CookieOptions CreateOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/auth",
        IsEssential = true
    };
}
