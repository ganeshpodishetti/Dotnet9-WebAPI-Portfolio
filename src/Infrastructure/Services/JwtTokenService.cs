using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

internal class JwtTokenService(
    IOptions<JwtTokenOptions> jwtOptions,
    UserManager<User> userManager,
    ILogger<JwtTokenService> logger) : IJwtTokenService
{
    // Generate JWT Token
    public async Task<string> GenerateJwtToken(User user)
    {
        logger.LogInformation("Generating JWT token for user: {UserId}", user.Id);

        var jwtSettings = jwtOptions.Value;
        if (jwtSettings == null || string.IsNullOrEmpty(jwtSettings.Key))
            throw new InvalidOperationException("JWT secret key is not configured.");

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Key));

        var singingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var claims = await GetClaimsAsync(user);
        var tokenOptions = GenerateTokenOptions(singingCredentials, claims);

        var jwtToken = new JwtSecurityTokenHandler().WriteToken(tokenOptions);
        logger.LogInformation("Successfully generated JWT token for user: {UserId}", user.Id);
        return jwtToken;
    }

    // Get User Id from Token
    public Guid GetUserIdFromToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            logger.LogWarning("Empty token provided for user ID extraction");
            throw new ArgumentException("Token is required");
        }

        try
        {
            token = token.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

            var handler = new JwtSecurityTokenHandler();
            var validationParameters = GetValidationParameters();
            var principal = handler.ValidateToken(token, validationParameters, out _);
            var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdString))
                throw new UnauthorizedAccessException("Invalid token");

            if (!Guid.TryParse(userIdString, out var userId))
                throw new InvalidOperationException("Invalid user ID format");

            logger.LogInformation("Successfully extracted user ID from token");
            return userId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract user ID from token");
            throw;
        }
    }

    // Validate Current Token
    public bool ValidateCurrentToken(string token)
    {
        try
        {
            if (string.IsNullOrEmpty(token)) return false;
            var handler = new JwtSecurityTokenHandler();
            var validationParameters = GetValidationParameters();
            handler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Refresh Token
    public async Task<(string AccessToken, string RefreshToken)> RefreshTokenAsync(string accessToken,
        string refreshToken)
    {
        try
        {
            var userId = GetUserIdFromToken(accessToken);
            var user = await userManager.FindByIdAsync(userId.ToString());

            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            // Validate stored refresh token
            if (user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                throw new UnauthorizedAccessException("Invalid or expired refresh token");

            // Generate new tokens
            var newAccessToken = await GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            // Save new refresh token
            await SaveRefreshTokenAsync(user, newRefreshToken);

            return (newAccessToken, newRefreshToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh token");
            throw;
        }
    }

    // Store refresh token in user entity
    public async Task<bool> SaveRefreshTokenAsync(User user, string refreshToken)
    {
        try
        {
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpirationDays);

            var result = await userManager.UpdateAsync(user);
            return result.Succeeded;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save refresh token for user: {UserId}", user.Id);
            return false;
        }
    }

    // Generate Refresh Token
    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    // Get Claims
    private async Task<List<Claim>> GetClaimsAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            // new("FirstName", user.AboutMe.FirstName),
            // new("LastName", user.AboutMe.LastName)
        };

        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return claims;
    }

    // Generate Token Options
    private JwtSecurityToken GenerateTokenOptions(SigningCredentials signingCredentials, List<Claim> claims)
    {
        return new JwtSecurityToken(
            jwtOptions.Value.Issuer,
            jwtOptions.Value.Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenExpirationMinutes),
            signingCredentials: signingCredentials
        );
    }

    // Get Validation Parameters
    private TokenValidationParameters GetValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Value.Key!)),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Value.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }
}