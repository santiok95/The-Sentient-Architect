using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

namespace SentientArchitect.API.Extensions;

public static class ValidationExtensions
{
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (invocationContext, next) =>
        {
            var validator = invocationContext.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
            
            if (validator is not null)
            {
                var input = invocationContext.Arguments.OfType<TRequest>().FirstOrDefault();
                if (input is not null)
                {
                    var validationResult = await validator.ValidateAsync(input);
                    if (!validationResult.IsValid)
                    {
                        var errorDictionary = validationResult.Errors
                            .GroupBy(e => e.PropertyName)
                            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                        return Results.ValidationProblem(errorDictionary);
                    }
                }
            }
            
            return await next(invocationContext);
        });
    }
}
