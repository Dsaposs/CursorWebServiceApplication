using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NotesApi.Configuration;
using NotesApi.Models;

namespace NotesApi.Services;

public class JwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly UserManager<ApplicationUser> _userManager;

    public JwtTokenService(IOptions<JwtSettings> settings, UserManager<ApplicationUser> userManager)
    {
        _settings = settings.Value;
        _userManager = userManager;
    }

    public async Task<(string Token, DateTime ExpiresAt)> CreateTokenAsync(ApplicationUser user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpiresMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
        };

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            // Include both claim names so role authorization works regardless of JWT claim mapping behavior.
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims, expires: expiresAt, signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
