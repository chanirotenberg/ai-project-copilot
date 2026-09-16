using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace ProjectCopilot.IntegrationTests;

public class ApiSmokeTests
{
    [Fact]
    public async Task GetProjects_ShouldReturnSuccessStatusCode()
    {
        await using var factory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");

                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["ConnectionStrings:DefaultConnection"] =
                                    "Host=localhost;Port=5433;Database=projectcopilot;Username=postgres;Password=postgres"
                            });
                    });
                });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}