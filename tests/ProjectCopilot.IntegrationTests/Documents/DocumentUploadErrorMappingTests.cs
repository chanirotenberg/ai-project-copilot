using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectCopilot.Api.Middleware;
using ProjectCopilot.Application.Common.Exceptions;

namespace ProjectCopilot.IntegrationTests.Documents;

/// <summary>
/// Status-code mappings introduced for document upload, exercised directly against
/// <see cref="ExceptionHandlingMiddleware"/>.
/// </summary>
/// <remarks>
/// The transport-level size limit is enforced by Kestrel, which TestServer does not emulate
/// (it provides no <c>IHttpMaxRequestBodySizeFeature</c>), so an end-to-end integration test
/// cannot produce a real 413. Driving the middleware directly with the exception Kestrel raises
/// is what makes that mapping verified rather than assumed.
/// </remarks>
public class DocumentUploadErrorMappingTests
{
    private static async Task<(int StatusCode, string Body)> InvokeAsync(Exception exception)
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);

        body.Position = 0;

        return (context.Response.StatusCode, await new StreamReader(body).ReadToEndAsync());
    }

    [Fact]
    public async Task OversizedBodyFromTheServer_ShouldMapToPayloadTooLarge()
    {
        var (statusCode, body) = await InvokeAsync(
            new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, statusCode);

        var problem = JsonSerializer.Deserialize<ProblemDetails>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, problem!.Status);
    }

    /// <summary>
    /// When the limit trips while the form reader is consuming the body, the size failure arrives
    /// wrapped — it must still be a 413 rather than degrading to a generic 500.
    /// </summary>
    [Fact]
    public async Task OversizedBodySurfacedByTheFormReader_ShouldMapToPayloadTooLarge()
    {
        var (statusCode, _) = await InvokeAsync(
            new InvalidDataException(
                "Multipart body length limit exceeded.",
                new BadHttpRequestException(
                    "Request body too large.",
                    StatusCodes.Status413PayloadTooLarge)));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, statusCode);
    }

    [Fact]
    public async Task UnparseableMultipartBody_ShouldMapToBadRequest()
    {
        var (statusCode, _) = await InvokeAsync(
            new InvalidDataException("Missing content-disposition header."));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public async Task Conflict_ShouldMapToConflictWithItsMessage()
    {
        var (statusCode, body) = await InvokeAsync(
            new ConflictException("This document already has an uploaded file."));

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Contains("This document already has an uploaded file.", body);
    }
}
