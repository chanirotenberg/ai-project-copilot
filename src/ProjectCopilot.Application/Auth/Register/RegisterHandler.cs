using FluentValidation;
using ProjectCopilot.Application.Abstractions;

namespace ProjectCopilot.Application.Auth.Register;

public sealed class RegisterHandler
{
    private readonly IIdentityService _identityService;
    private readonly IValidator<RegisterCommand> _validator;

    public RegisterHandler(
        IIdentityService identityService,
        IValidator<RegisterCommand> validator)
    {
        _identityService = identityService;
        _validator = validator;
    }

    public async Task<RegisterResult> HandleAsync(
        RegisterCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var userId = await _identityService.RegisterAsync(
            command.Email,
            command.Password,
            cancellationToken);

        return new RegisterResult(userId, command.Email);
    }
}
