using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ShopFlow.BuildingBlocks.Correlation;
using ShopFlow.BuildingBlocks.Errors;

namespace ShopFlow.BuildingBlocks.Middleware;

internal sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    CorrelationContext correlationContext)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred.");
            await HandleExceptionAsync(context, ex, correlationContext.CorrelationId);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string? correlationId)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var code = ErrorCodes.InternalError;
        var type = $"https://shopflow/errors/{code.ToLowerInvariant().Replace('_', '-')}";

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = "An internal server error occurred.",
            Type = type,
            Detail = "An unexpected error occurred."
        };

        problemDetails.Extensions["code"] = code;
        if (!string.IsNullOrEmpty(correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }
}
