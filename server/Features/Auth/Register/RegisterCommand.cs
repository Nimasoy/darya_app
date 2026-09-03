namespace Darya.Features.Auth.Register;

using Darya.Server.Data;
using Darya.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public record RegisterCommand(
    string Email,
    string Password,
    string Name,
    string Role
);

public class RegisterCommandHandler
{
    private readonly DaryaDbContext _db;
    private readonly ITokenService _tokenService;

    public RegisterCommandHandler(DaryaDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<IResult> HandleAsync(RegisterCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return Results.BadRequest(new { message = "Email and Password are required." });
        }

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var emailExists = await _db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (emailExists)
        {
            return Results.Conflict(new { message = "Email is already registered." });
        }

        var role = Role.PrimaryUser;
        if (!string.IsNullOrWhiteSpace(command.Role) && Enum.TryParse<Role>(command.Role, true, out var parsedRole))
        {
            role = parsedRole;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.Password),
            Name = string.IsNullOrWhiteSpace(command.Name) ? "User" : command.Name.Trim(),
            Role = role,
            LastPeriodStartDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-10), DateTimeKind.Utc),
            Language = "fa"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

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
                language = user.Language
            }
        });
    }
}
