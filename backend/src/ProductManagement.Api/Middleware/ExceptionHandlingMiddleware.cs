using System.Text.Json;
using FluentValidation;
using ProductManagement.Api.Exceptions;

namespace ProductManagement.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (status, title, errors) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message, null),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message, null),
            ValidationAppException vae => (StatusCodes.Status400BadRequest, vae.Message, vae.Errors),
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                ve.Errors
                    .GroupBy(e => string.IsNullOrWhiteSpace(e.PropertyName) ? "request" : ToCamel(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray())),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", (IDictionary<string, string[]>?)null)
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = status;

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.com/{status}",
            ["title"] = title,
            ["status"] = status,
            ["traceId"] = context.TraceIdentifier
        };

        if (errors is not null)
        {
            problem["errors"] = errors;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    private static string ToCamel(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return propertyName;
        }

        var parts = propertyName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(".", parts.Select(p =>
        {
            if (p.Length == 1)
            {
                return p.ToLowerInvariant();
            }

            // Keep collection indexers like Variants[0]
            var name = p;
            var bracket = name.IndexOf('[');
            if (bracket > 0)
            {
                var head = name[..bracket];
                var tail = name[bracket..];
                return char.ToLowerInvariant(head[0]) + head[1..] + tail;
            }

            return char.ToLowerInvariant(name[0]) + name[1..];
        }));
    }
}
