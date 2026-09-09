using CURE.Domain.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CURE.Api.Controllers;

public sealed class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : IAsyncExceptionFilter
{
    public Task OnExceptionAsync(ExceptionContext context)
    {
        var exception = context.Exception;
        var status = exception switch
        {
            DomainException domainException when domainException.Code == ErrorCodes.NotFound => StatusCodes.Status404NotFound,
            PermissionDeniedException => StatusCodes.Status403Forbidden,
            ConcurrencyException => StatusCodes.Status409Conflict,
            DomainException => StatusCodes.Status422UnprocessableEntity,
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };
        logger.LogError(exception, "Unhandled API exception");
        var domain = exception as DomainException;
        context.Result = new ObjectResult(new { error = new { code = domain?.Code ?? "INTERNAL_ERROR", message = status == 500 ? "An unexpected error occurred." : exception.Message, details = domain?.Details ?? new Dictionary<string, object?>(), requestId = context.HttpContext.TraceIdentifier } }) { StatusCode = status };
        context.ExceptionHandled = true;
        return Task.CompletedTask;
    }
}
