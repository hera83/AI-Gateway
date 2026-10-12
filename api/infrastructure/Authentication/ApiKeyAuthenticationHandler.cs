using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using api.Repositories.ApiKeys.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace api.Infrastructure.Authentication;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    IApiKeyRepository apiKeyRepository,
    IProblemDetailsService problemDetailsService) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return AuthenticateResult.NoResult();
        }

        var providedKey = headerValue.ToString();

        // An empty master key disables it.
        var masterKey = configuration["ApiKeys:MasterKey"];
        if (!string.IsNullOrEmpty(masterKey) && FixedTimeEquals(providedKey, masterKey))
        {
            return AuthenticateResult.Success(CreateTicket(new Claim(ApiKeyClaimTypes.KeyType, ApiKeyTypes.Master)));
        }

        var apiKey = await apiKeyRepository.GetValidByHashAsync(ApiKeyGenerator.Hash(providedKey), Context.RequestAborted);
        if (apiKey is null)
        {
            return AuthenticateResult.Fail("Invalid, inactive or expired API key.");
        }

        await apiKeyRepository.MarkUsedAsync(apiKey.Id, Context.RequestAborted);

        return AuthenticateResult.Success(CreateTicket(
            new Claim(ApiKeyClaimTypes.KeyType, ApiKeyTypes.Standard),
            new Claim(ApiKeyClaimTypes.KeyId, apiKey.Id.ToString())));
    }

    // The base class would otherwise send 401/403 with an empty body.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(StatusCodes.Status401Unauthorized, $"A valid '{ApiKeyAuthenticationOptions.HeaderName}' header is required.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(StatusCodes.Status403Forbidden, "The provided API key does not have access to this resource.");

    private async Task WriteProblemAsync(int status, string detail)
    {
        Response.StatusCode = status;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = { Status = status, Detail = detail }
        });
    }

    private static AuthenticationTicket CreateTicket(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.SchemeName);
        return new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyAuthenticationOptions.SchemeName);
    }

    // FixedTimeEquals still short-circuits on a length mismatch, which is an accepted trade-off for a bearer secret.
    private static bool FixedTimeEquals(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}
