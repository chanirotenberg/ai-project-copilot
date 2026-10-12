using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ProjectCopilot.IntegrationTests;

/// <summary>
/// Regression tests for AUDIT-5: a malformed (truncated/invalid) JSON request body previously
/// fell through the catch-all branch in <see cref="ProjectCopilot.Api.Middleware.ExceptionHandlingMiddleware"/>
/// and surfaced as a generic 500, instead of the 400 a client error deserves. The fix adds a
/// dedicated catch branch for <see cref="Microsoft.AspNetCore.Http.BadHttpRequestException"/>
/// wrapping a <see cref="System.Text.Json.JsonException"/> (the concrete type empirically
/// confirmed to reach the middleware for minimal-API parameter binding failures in this
/// environment).
/// </summary>
public class MalformedRequestBodyTests : IClassFixture<ProjectCopilotWebApplicationFactory>
{
    private readonly ProjectCopilotWebApplicationFactory _factory;

    public MalformedRequestBodyTests(ProjectCopilotWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Deliberately truncated/invalid JSON (unterminated object) - not merely a wrong field type,
    // which would be a 400 ValidationException-style failure elsewhere. This exercises the
    // parser-level failure path.
    private const string MalformedJsonBody = "{\"title\": \"x\"";

    public static TheoryData<string> MalformedBodyRoutes => new()
    {
        "/api/v1/tasks",
        "/api/v1/projects",
        "/api/v1/documents"
    };

    [Theory]
    [MemberData(nameof(MalformedBodyRoutes))]
    public async Task Post_WithMalformedJsonBody_ShouldReturnBadRequestWithoutLeakingParserDetails(string route)
    {
        using var client = _factory.CreateClient();

        var (accessToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = new StringContent(MalformedJsonBody, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(route, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var bodyText = await response.Content.ReadAsStringAsync();

        // Must never echo raw JSON-parser error text (byte/line position, token fragments) or
        // any fragment of the client's literal malformed input back to the caller.
        Assert.DoesNotContain("BytePositionInLine", bodyText);
        Assert.DoesNotContain("LineNumber", bodyText);
        Assert.DoesNotContain("JsonException", bodyText);
        Assert.DoesNotContain("JsonReaderException", bodyText);
        Assert.DoesNotContain("\"title\": \"x\"", bodyText);
        Assert.DoesNotContain("System.Text.Json", bodyText);
    }

    [Theory]
    [MemberData(nameof(MalformedBodyRoutes))]
    public async Task Post_WithWrongContentType_ShouldStillReturnUnsupportedMediaType(string route)
    {
        using var client = _factory.CreateClient();

        var (accessToken, _, _, _) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // A JSON-looking body sent with the wrong Content-Type must still be rejected at the
        // 415 stage (before model binding ever runs) - this fix must not change that behavior.
        using var content = new StringContent("{\"title\":\"x\"}", Encoding.UTF8, "text/plain");
        var response = await client.PostAsync(route, content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
}
