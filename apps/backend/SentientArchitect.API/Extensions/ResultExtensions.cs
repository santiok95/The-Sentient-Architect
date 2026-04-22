using Microsoft.AspNetCore.Http;
using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.API.Extensions;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result)
        => result.Succeeded
            ? Results.NoContent()
            : MapFailure(result.ErrorType, result.Errors);

    public static IResult ToHttpResult<T>(this Result<T> result)
        => result.Succeeded
            ? Results.Ok(result.Data)
            : MapFailure(result.ErrorType, result.Errors);

    public static IResult ToCreatedResult<T>(this Result<T> result, string uri)
        => result.Succeeded
            ? Results.Created(uri, result.Data)
            : MapFailure(result.ErrorType, result.Errors);

    private static IResult MapFailure(ErrorType type, List<string> errors)
    {
        var errorDictionary = new Dictionary<string, string[]> { { "Errors", errors.ToArray() } };

        return type switch
        {
            ErrorType.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado.",
                detail: errors.Count > 0
                    ? string.Join("; ", errors)
                    : "El recurso solicitado no existe o no está disponible."),

            ErrorType.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto en la operación.",
                detail: errors.Count > 0
                    ? string.Join("; ", errors)
                    : "La operación no pudo completarse porque existe un conflicto con el estado actual."),

            ErrorType.Unauthorized => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado.",
                detail: errors.Count > 0
                    ? string.Join("; ", errors)
                    : "Necesitás iniciar sesión para realizar esta acción."),

            ErrorType.Forbidden => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Acceso denegado.",
                detail: errors.Count > 0
                    ? string.Join("; ", errors)
                    : "No tenés permisos para realizar esta acción."),

            ErrorType.Failure => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Ocurrió un error inesperado.",
                detail: "No fue posible completar la operación. Si el problema persiste, contactá al soporte."),

            _ => Results.ValidationProblem(
                errors: errorDictionary,
                title: "Revisá los datos ingresados.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.1")
        };
    }
}
