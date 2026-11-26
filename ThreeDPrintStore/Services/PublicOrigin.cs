namespace ThreeDPrintStore.Services;

public static class PublicOrigin
{
    public static string? Get(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var value = configuration["Store:PublicOrigin"];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !(uri.Scheme == Uri.UriSchemeHttps ||
              (environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            return null;
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
