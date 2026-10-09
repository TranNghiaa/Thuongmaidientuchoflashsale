using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ShopFlow.BuildingBlocks.Database;
using ShopFlow.BuildingBlocks;
using ShopFlow.BuildingBlocks.Auth;
using ShopFlow.Modules.Identity.Domain;
using ShopFlow.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace ShopFlow.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IModuleMigrator, IdentityMigrator>();
        services.AddScoped<IModuleSeeder, IdentitySeeder>();

        return services;
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return WebEncoders.Base64UrlEncode(randomBytes);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return WebEncoders.Base64UrlEncode(hash);
    }

    private static string GenerateJwt(User user, JwtOptions jwtOptions, TimeProvider timeProvider)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtOptions.Secret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15),
            Issuer = jwtOptions.Issuer,
            Audience = jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest req, IdentityDbContext db, TimeProvider timeProvider) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(req.Email))
            {
                return Results.BadRequest(new { code = "INVALID_EMAIL", message = "Invalid email format." });
            }

            req.Email = req.Email.ToLowerInvariant();
            
            if (req.Password.Length < 8)
            {
                return Results.BadRequest(new { code = "INVALID_PASSWORD", message = "Password must be at least 8 characters." });
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = req.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                Role = "User",
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            };

            db.Users.Add(user);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex) when (PostgresErrors.IsUniqueViolation(ex, "IX_users_email"))
            {
                return Results.Conflict(new { code = "EMAIL_EXISTS" });
            }

            return Results.Created($"/auth/users/{user.Id}", null);
        });

        group.MapPost("/login", async (LoginRequest req, IdentityDbContext db, IConfiguration configuration, TimeProvider timeProvider) =>
        {
            req.Email = req.Email.ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            {
                return Results.Json(new { code = "INVALID_CREDENTIALS", message = "Invalid credentials." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()!;
            var accessToken = GenerateJwt(user, jwtOptions, timeProvider);

            var refreshToken = GenerateRefreshToken();
            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                TokenHash = HashToken(refreshToken),
                UserId = user.Id,
                ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(7)
            };
            db.RefreshTokens.Add(refreshTokenEntity);
            await db.SaveChangesAsync();

            return Results.Ok(new { accessToken, expiresIn = 15 * 60, refreshToken });
        });

        group.MapPost("/refresh", async (RefreshRequest req, IdentityDbContext db, IConfiguration configuration, TimeProvider timeProvider) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
                return Results.BadRequest(new { code = "INVALID_REQUEST", message = "Refresh token is required." });

            var hash = HashToken(req.RefreshToken);
            var tokenEntity = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (tokenEntity == null || tokenEntity.ExpiresAt < timeProvider.GetUtcNow().UtcDateTime)
            {
                return Results.Json(new { code = "INVALID_REFRESH_TOKEN", message = "Invalid or expired refresh token." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            if (tokenEntity.RevokedAt != null)
            {
                // xoay vòng: token cũ bị thu hồi -> thu hồi toàn bộ refresh token của user đó
                var activeTokens = await db.RefreshTokens.Where(t => t.UserId == tokenEntity.UserId && t.RevokedAt == null).ToListAsync();
                foreach (var t in activeTokens)
                {
                    t.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
                }
                await db.SaveChangesAsync();
                return Results.Json(new { code = "INVALID_REFRESH_TOKEN", message = "Invalid refresh token." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            // Revoke current token
            tokenEntity.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;

            var user = await db.Users.FindAsync(tokenEntity.UserId);
            if (user == null)
            {
                return Results.Json(new { code = "INVALID_REFRESH_TOKEN", message = "User not found." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()!;
            var newAccessToken = GenerateJwt(user, jwtOptions, timeProvider);

            var newRefreshToken = GenerateRefreshToken();
            var newRefreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                TokenHash = HashToken(newRefreshToken),
                UserId = user.Id,
                ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(7)
            };
            db.RefreshTokens.Add(newRefreshTokenEntity);
            await db.SaveChangesAsync();

            return Results.Ok(new { accessToken = newAccessToken, expiresIn = 15 * 60, refreshToken = newRefreshToken });
        });

        group.MapPost("/logout", async (LogoutRequest req, IdentityDbContext db, TimeProvider timeProvider) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
                return Results.NoContent();

            var hash = HashToken(req.RefreshToken);
            var tokenEntity = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (tokenEntity != null && tokenEntity.RevokedAt == null)
            {
                tokenEntity.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync();
            }

            return Results.NoContent();
        });

        group.MapGet("/me", [Authorize] (ClaimsPrincipal principal) =>
        {
            var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email);
            var role = principal.FindFirstValue(ClaimTypes.Role);
            return Results.Ok(new { id, email, role });
        });

        return endpoints;
    }
}

internal class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

internal class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

internal class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

internal class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
