using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.Identity;

/// <summary>
/// Generates signed JWT bearer tokens.
/// Reads Jwt:Key, Jwt:Issuer, Jwt:Audience, Jwt:ExpiresInHours from configuration.
/// </summary>
public sealed class TokenService(IConfiguration configuration) : ITokenService
{
    private const string TenantIdClaimType = "tenantId";

    public string CreateToken(
        Guid userId,
        string email,
        string displayName,
        Guid tenantId,
        IList<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        var issuer   = configuration["Jwt:Issuer"]   ?? "SentientArchitect";
        var audience = configuration["Jwt:Audience"] ?? "SentientArchitect";
        var expiresHours = GetExpiresHours();

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name,  displayName),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim(TenantIdClaimType,             tenantId.ToString()),
        ];

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddHours(expiresHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid? GetUserIdFromToken(string token, bool allowExpired = false)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(
                token,
                CreateValidationParameters(validateLifetime: !allowExpired),
                out _);

            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }

    public int GetAccessTokenLifetimeSeconds() =>
        (int)TimeSpan.FromHours(GetExpiresHours()).TotalSeconds;

    private TokenValidationParameters CreateValidationParameters(bool validateLifetime)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "SentientArchitect";
        var audience = configuration["Jwt:Audience"] ?? "SentientArchitect";

        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSigningKey())),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.Zero,
        };
    }

    private string GetSigningKey() =>
        configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

    // Lee Jwt:ExpiresInHours con fallback a Jwt:ExpiresInDays por compatibilidad.
    // Default: 1 hora (access token corto — recordá que Logout no revoca, así que
    // un token robado vive hasta que expire).
    private int GetExpiresHours()
    {
        if (int.TryParse(configuration["Jwt:ExpiresInHours"], out var hours) && hours > 0)
            return hours;

        if (int.TryParse(configuration["Jwt:ExpiresInDays"], out var days) && days > 0)
            return days * 24;

        return 1;
    }
}
