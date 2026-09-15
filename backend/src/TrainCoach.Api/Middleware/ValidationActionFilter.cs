using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TrainCoach.Api.Middleware;

/// <summary>
/// Runs a registered FluentValidation IValidator&lt;T&gt; (if any) against every action argument
/// before the action executes. Replaces the archived FluentValidation.AspNetCore package with a
/// small explicit filter instead of relying on an unmaintained integration.
/// </summary>
public class ValidationActionFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        await next();
    }
}
