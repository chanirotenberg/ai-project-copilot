using FluentValidation;

namespace ProjectCopilot.Application.Auth.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            // Kept in sync with Identity's PasswordOptions.RequiredLength (Program.cs) so this
            // early validator rejects at the same threshold Identity would enforce anyway,
            // instead of a shorter password passing here only to fail later at CreateAsync.
            .MinimumLength(10);
    }
}
