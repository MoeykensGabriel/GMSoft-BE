using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GMSoft.Data.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(AuthUserData user)
    {
        var settings = _configuration.GetSection("JwtSettings");

        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                     ?? settings["SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException(
                "Falta la clave de firma del JWT. Configurá JwtSettings:SecretKey o la variable JWT_SECRET_KEY.");

        // Una jornada de reparto entera. Con la hora que suele usarse por defecto,
        // al chofer se le vence el token a media mañana y pierde la sesión en la calle.
        var expirationMinutes = settings.GetValue<int?>("ExpirationInMinutes") ?? 720;

        var claims = new List<Claim>
        {
            new(AppClaimTypes.UserId, user.UserId.ToString()),
            new(AppClaimTypes.Email,  user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(user.Roles.Select(role => new Claim(AppClaimTypes.Role, role)));

        if (user.DriverId is not null)
            claims.Add(new Claim(AppClaimTypes.DriverId, user.DriverId.Value.ToString()));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             settings["Issuer"],
            audience:           settings["Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
