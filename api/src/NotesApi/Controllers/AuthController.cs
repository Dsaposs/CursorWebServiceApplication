using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.DTOs;
using NotesApi.Models;
using NotesApi.Services;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ApplicationDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService jwtTokenService,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _db = db;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null) return BadRequest(new { errors = new[] { "A user with this email already exists." } });

        var user = new ApplicationUser { UserName = request.Email, Email = request.Email, EmailConfirmed = true };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, "User");

        return Ok(new RegisterResponse { UserId = user.Id, Email = user.Email! });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthWithRefreshResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? await _userManager.FindByNameAsync(request.Email);
        if (user is null) return Unauthorized(new { errors = new[] { "Invalid email or password." } });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized(new { errors = new[] { "Invalid email or password." } });

        var (token, expiresAt) = await _jwtTokenService.CreateTokenAsync(user);
        var refreshToken = await IssueRefreshTokenAsync(user.Id);

        return Ok(new AuthWithRefreshResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            RefreshToken = refreshToken,
        });
    }

    /// <summary>
    /// Exchanges a valid, non-expired refresh token for a new JWT + rotated refresh token.
    /// Implements rotate-on-use: each token may only be used once.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthWithRefreshResponse>> Refresh(RefreshTokenRequest request)
    {
        var hash = HashToken(request.RefreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);

        if (stored is null)
            return Unauthorized(new { errors = new[] { "Refresh token is invalid or expired." } });

        // Rotate: revoke old token and issue a fresh pair
        stored.IsRevoked = true;
        await _db.SaveChangesAsync();

        var (token, expiresAt) = await _jwtTokenService.CreateTokenAsync(stored.User);
        var newRefreshToken = await IssueRefreshTokenAsync(stored.UserId);

        return Ok(new AuthWithRefreshResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            RefreshToken = newRefreshToken,
        });
    }

    /// <summary>Revokes the given refresh token (logout).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshTokenRequest request)
    {
        var hash = HashToken(request.RefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is not null)
        {
            stored.IsRevoked = true;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<string> IssueRefreshTokenAsync(string userId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(raw),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        await _db.SaveChangesAsync();
        return raw;
    }

    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
