namespace Darya.Features.Auth;

using System.Security.Claims;
using System.Text;
using Darya.Server.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public interface ITokenService
{
    string GenerateToken(User user);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(User user)
    {
        var jwtKey = _config["Jwt:Key"] ?? "super_secret_darya_key_must_be_long_and_secure_256_bits";
        var jwtIssuer = _config["Jwt:Issuer"] ?? "DaryaIssuer";
        var jwtAudience = _config["Jwt:Audience"] ?? "DaryaAudience";
        var expireMinutes = Convert.ToInt32(_config["Jwt:ExpireMinutes"] ?? "1440");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
