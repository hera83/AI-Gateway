using Microsoft.AspNetCore.Authentication;

namespace api.Infrastructure.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "x-api-key";
}
