using FluentValidation.Results;

namespace Newsletters.Api.Controllers;

internal static class ValidationResultExtensions
{
    public static IDictionary<string, string[]> ToErrorDictionary(this ValidationResult result)
        => result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
}
