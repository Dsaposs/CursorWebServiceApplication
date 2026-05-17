using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace NotesApi.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed partial class PasswordValidationAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string password)
        {
            ErrorMessage = "Password is required.";
            return false;
        }

        if (password.Length < 7)
        {
            ErrorMessage = "Password must be at least 7 characters long.";
            return false;
        }

        if (!UppercaseRegex().IsMatch(password))
        {
            ErrorMessage = "Password must contain at least one uppercase letter.";
            return false;
        }

        if (!DigitRegex().IsMatch(password))
        {
            ErrorMessage = "Password must contain at least one number.";
            return false;
        }

        return true;
    }

    [GeneratedRegex("[A-Z]")]
    private static partial Regex UppercaseRegex();

    [GeneratedRegex("[0-9]")]
    private static partial Regex DigitRegex();
}
