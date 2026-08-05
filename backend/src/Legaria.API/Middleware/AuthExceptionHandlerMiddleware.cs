using Legaria.Application.Authentication;
using Legaria.Application.Branches;
using Legaria.Application.Employees;
using Legaria.Application.Documents;
using Legaria.Application.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace Legaria.API.Middleware;

public sealed class AuthExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<AuthExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AuthException exception)
        {
            await WriteProblemAsync(context, exception);
        }
        catch (OrganizationException exception)
        {
            var status = exception.Kind switch
            {
                OrganizationErrorKind.NotFound => StatusCodes.Status404NotFound,
                OrganizationErrorKind.Conflict => StatusCodes.Status409Conflict,
                OrganizationErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };
            await WriteProblemAsync(context, status, exception.Code, exception.Message);
        }
        catch (BranchException exception)
        {
            var status = exception.Kind switch
            {
                BranchErrorKind.NotFound => StatusCodes.Status404NotFound,
                BranchErrorKind.Conflict => StatusCodes.Status409Conflict,
                BranchErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };
            await WriteProblemAsync(context, status, exception.Code, exception.Message);
        }
        catch (EmployeeException exception)
        {
            var status = exception.Kind switch
            {
                EmployeeErrorKind.NotFound => StatusCodes.Status404NotFound,
                EmployeeErrorKind.Conflict => StatusCodes.Status409Conflict,
                EmployeeErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };
            await WriteProblemAsync(context, status, exception.Code, exception.Message);
        }
        catch (DocumentCatalogException exception)
        {
            var status = exception.Kind switch
            {
                DocumentCatalogErrorKind.NotFound => StatusCodes.Status404NotFound,
                DocumentCatalogErrorKind.Conflict => StatusCodes.Status409Conflict,
                DocumentCatalogErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };
            await WriteProblemAsync(context, status, exception.Code, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "La aplicación encontró una configuración o estado inválido.");
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "application.invalid_state",
                "La aplicación no está configurada correctamente.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, AuthException exception)
    {
        var status = exception.Code switch
        {
            AuthErrorCodes.InvalidCredentials or AuthErrorCodes.InvalidRefreshToken =>
                StatusCodes.Status401Unauthorized,
            AuthErrorCodes.EmailNotVerified or AuthErrorCodes.AccountUnavailable or AuthErrorCodes.UntrustedOrigin =>
                StatusCodes.Status403Forbidden,
            AuthErrorCodes.AccountLocked => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest
        };
        return WriteProblemAsync(context, status, exception.Code, exception.Message);
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string code,
        string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status >= 500 ? "Error interno" : "No fue posible completar la solicitud",
            Detail = detail
        };
        problem.Extensions["code"] = code;
        await context.Response.WriteAsJsonAsync(problem);
    }
}
