using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Saas.Billing.Application.Ports;

namespace Saas.Billing.Api.Endpoints;

internal sealed class TenantDatabaseExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not TenantDatabaseUnavailableException unavailable)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = unavailable.ErrorCode,
            Detail = unavailable.Message,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["code"] = unavailable.ErrorCode;

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
