namespace api.Infrastructure.Authentication;

public static class AuthorizationPolicies
{
    public const string MasterKeyOnly = "MasterKeyOnly";

    // For endpoints that own data per key (the knowledge base). The master key has no ApiKeys row.
    public const string StandardKeyOnly = "StandardKeyOnly";
}
