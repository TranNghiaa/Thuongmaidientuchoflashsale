using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ShopFlow.BuildingBlocks.Correlation;
using ShopFlow.BuildingBlocks.Middleware;

namespace ShopFlow.UnitTests.BuildingBlocks;

public class MiddlewareTests
{
    [Fact]
    public async Task CorrelationIdMiddleware_ShouldKeepExistingHeader_AndSetResponseHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "existing-id";
        
        var correlationContext = new CorrelationContext();
        string? capturedId = null;
        var middleware = new CorrelationIdMiddleware(innerHttpContext => {
            capturedId = correlationContext.CorrelationId;
            return Task.CompletedTask;
        }, correlationContext);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("existing-id", capturedId);
        Assert.Equal("existing-id", context.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task CorrelationIdMiddleware_ShouldGenerateNewHeaderIfMissing_AndSetResponseHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var correlationContext = new CorrelationContext();
        string? capturedId = null;
        var middleware = new CorrelationIdMiddleware(innerHttpContext => {
            capturedId = correlationContext.CorrelationId;
            return Task.CompletedTask;
        }, correlationContext);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.NotNull(capturedId);
        Assert.NotEmpty(capturedId);
        Assert.Equal(capturedId, context.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_ShouldReturn500InternalError_WithProblemDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        
        var logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();
        var correlationContext = new CorrelationContext { CorrelationId = "test-corr-id" };

        var middleware = new ExceptionHandlingMiddleware(
            innerHttpContext => throw new InvalidOperationException("Something went wrong"),
            logger,
            correlationContext);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        
        using var jsonDoc = JsonDocument.Parse(responseBody);
        var root = jsonDoc.RootElement;
        
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("https://shopflow/errors/internal-error", root.GetProperty("type").GetString());
        Assert.Equal("INTERNAL_ERROR", root.GetProperty("code").GetString());
        Assert.Equal("test-corr-id", root.GetProperty("correlationId").GetString());
        Assert.Equal("An unexpected error occurred.", root.GetProperty("detail").GetString());
    }
}
