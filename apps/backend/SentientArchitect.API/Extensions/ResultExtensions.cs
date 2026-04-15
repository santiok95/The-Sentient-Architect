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
                detail: string.Join("; ", errors)),

            ErrorType.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto en la operación.",
                detail: string.Join("; ", errors)),

            ErrorType.Unauthorized => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado.",
                detail: string.Join("; ", errors)),

            ErrorType.Forbidden => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Acceso denegado.",
                detail: string.Join("; ", errors)),

            _ => Results.ValidationProblem(
                errors: errorDictionary,
                title: "Ha ocurrido uno o más errores de validación/negocio.",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.1")
        };
    }
}
