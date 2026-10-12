using api.Infrastructure.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace api.Infrastructure.OpenApi;

// Adds Swagger UI's "Authorize" button: the key is entered once and sent with every "Try it out" call.
public class ApiKeySecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[ApiKeyAuthenticationOptions.SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = ApiKeyAuthenticationOptions.HeaderName,
            In = ParameterLocation.Header,
            Description = $"API-nøgle sendt i headeren {ApiKeyAuthenticationOptions.HeaderName}."
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(ApiKeyAuthenticationOptions.SchemeName, document)] = []
        });

        return Task.CompletedTask;
    }
}
