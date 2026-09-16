using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ProjectCopilot.IntegrationTests;

public class ProjectCopilotWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "integration-tests-only-jwt-signing-key-32bytes-min";
    public const string TestJwtIssuer = "Test";
    public const string TestJwtAudience = "Test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Port=5433;Database=projectcopilot;Username=postgres;Password=postgres",
                    ["Jwt:Key"] = TestJwtKey,
                    ["Jwt:Issuer"] = TestJwtIssuer,
                    ["Jwt:Audience"] = TestJwtAudience,
                    ["Jwt:AccessTokenExpiryMinutes"] = "60"
                });
        });
    }
}
