namespace Darya.Features.Auth;

using Darya.Server.Models;

public record UserDto(
    Guid Id,
    string Email,
    string Name,
    Role Role,
    string Language
);

public record AuthResponseDto(
    string Token,
    UserDto User
);
