using System.ComponentModel.DataAnnotations;
using NotesApi.Validation;

namespace NotesApi.DTOs;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [PasswordValidation]
    public string Password { get; set; } = string.Empty;
}
