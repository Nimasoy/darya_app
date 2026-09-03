namespace Darya.Features.Auth.Login;

using Darya.Server.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public record LoginCommand(
    string Email,
    string Password
);

public class LoginCommandHandler
{
    private readonly DaryaDbContext _db;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(DaryaDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<IResult> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return Results.BadRequest(new { message = "Email and Password are required." });
        }

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user == null || !BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash))
        {
            return Results.Json(new { message = "Invalid email or password." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var token = _tokenService.GenerateToken(user);

        return Results.Ok(new
        {
            token,
            user = new
            {
                id = user.Id,
                email = user.Email,
                name = user.Name,
                role = user.Role,
                language = user.Language,
                partnerUserId = user.PartnerUserId
            }
        });
    }
}
