using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace ThreeDPrintStore.Services;

public static class AdminCredentialValidator
{
    private const string CredentialVersionClaim = "admin_credential_version";

    public static bool Verify(IConfiguration configuration, string username, string password)
    {
        var configuredUsername = configuration["Admin:Username"];
        var hash = configuration["Admin:PasswordHash"];
        if (string.IsNullOrWhiteSpace(configuredUsername) || string.IsNullOrWhiteSpace(hash))
            return false;

        try
        {
            var result = new PasswordHasher<string>().VerifyHashedPassword(configuredUsername, hash, password);
            return result != PasswordVerificationResult.Failed &&
                string.Equals(configuredUsername, username, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            return false;
        }
    }

    public static Claim CreateVersionClaim(IConfiguration configuration) =>
        new(CredentialVersionClaim, GetVersion(configuration)!);

    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var version = GetVersion(context.HttpContext.RequestServices.GetRequiredService<IConfiguration>());
        if (version is null || context.Principal?.FindFirstValue(CredentialVersionClaim) != version)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    private static string? GetVersion(IConfiguration configuration)
    {
        var username = configuration["Admin:Username"];
        var hash = configuration["Admin:PasswordHash"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(hash))
            return null;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{username.Length}:{username}{hash}")));
    }
}
