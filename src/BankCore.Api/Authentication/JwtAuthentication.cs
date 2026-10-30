using System.Security.Claims;
using System.Text;
using BankCore.Application.Dtos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BankCore.Api.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 key, minimum 32 bytes. Supply via user-secrets or environment variables - never commit a real key.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 30;
}

public static class BankClaimTypes
{
    public const string Subject = "sub";
    public const string UserName = "unique_name";
    public const string Role = "role";
    public const string TokenId = "jti";
}

public sealed record AccessToken(string Token, string TokenType, DateTime ExpiresAtUtc, string Username, string Role);

public interface ITokenService
{
    AccessToken CreateToken(AuthenticatedUser user);
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    public AccessToken CreateToken(AuthenticatedUser user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(new List<Claim>
            {
                new(BankClaimTypes.Subject, user.UserId.ToString()),
                new(BankClaimTypes.UserName, user.Username),
                new(BankClaimTypes.Role, user.Role),
                new(BankClaimTypes.TokenId, Guid.NewGuid().ToString()),
            }),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, "Bearer", expires, user.Username, user.Role);
    }
}

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey ?? string.Empty) >= 32,
                "Jwt:SigningKey must be at least 32 bytes (256 bits). Use 'dotnet user-secrets' or an environment variable.")
            .Validate(o => o.ExpiryMinutes is > 0 and <= 480, "Jwt:ExpiryMinutes must be between 1 and 480.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = BankClaimTypes.UserName,
                    RoleClaimType = BankClaimTypes.Role,
                };
            });

        return services;
    }
}
