using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WhatsAppBot.Api.Middleware;

// Última red de seguridad: cualquier excepción que se escape de un
// controller sin ser capturada llega acá en vez de tumbar a Kestrel o
// devolver el stack trace crudo al cliente.
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["CorrelationId"] as string ?? "desconocido";

        _logger.LogError(exception,
            "Excepción no manejada procesando {Method} {Path}. CorrelationId: {CorrelationId}",
            httpContext.Request.Method, httpContext.Request.Path, correlationId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Ocurrió un error inesperado.",
            Detail = "Ya quedó registrado del lado del servidor — si necesitás reportarlo, mencioná este ID.",
        };
        problem.Extensions["correlationId"] = correlationId;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken: cancellationToken);

        return true; // "ya lo manejé, no sigas propagando la excepción"
    }
}
