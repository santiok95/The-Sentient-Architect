using SentientArchitect.Application.Common.Results;

namespace SentientArchitect.API.Extensions;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result)
        => result.Succeeded
            ? Results.NoContent()
            : Results.BadRequest(new { errors = result.Errors });

    public static IResult ToHttpResult<T>(this Result<T> result)
        => result.Succeeded
            ? Results.Ok(result.Data)
            : Results.BadRequest(new { errors = result.Errors });

    public static IResult ToCreatedResult<T>(this Result<T> result, string uri)
        => result.Succeeded
            ? Results.Created(uri, result.Data)
            : Results.BadRequest(new { errors = result.Errors });
}
