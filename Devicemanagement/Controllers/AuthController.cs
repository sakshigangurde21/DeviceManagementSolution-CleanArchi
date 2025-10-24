using Infrastructure.Data;
using Application.DTO;
using DeviceManagementSolution.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;

namespace Devicemanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly DeviceDbContext _context;
        private readonly IConfiguration _config;
        private readonly IUserService _userService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly INotificationService _notificationService;

        public AuthController(DeviceDbContext context, IConfiguration config, IUserService userService,
 IRefreshTokenService refreshTokenService, INotificationService notificationService)
        {
            _context = context;
            _config = config;
            _userService = userService;
            _refreshTokenService = refreshTokenService;
            _notificationService = notificationService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || dto.Username.Length < 2 || dto.Username.Length > 20)
                return BadRequest(new { message = "Username must be 2–20 characters long." });

            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6 || dto.Password.Length > 32)
                return BadRequest(new { message = "Password must be 6–32 characters long." });

            var existingUser = await _userService.GetByUsernameAsync(dto.Username);
            if (existingUser != null)
                return Conflict(new { message = "Username already exists" });

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = string.IsNullOrEmpty(dto.Role) ? "User" : char.ToUpper(dto.Role[0]) + dto.Role.Substring(1).ToLower()
            };

            await _userService.CreateUserAsync(user);

            return Ok(new { message = "User registered successfully" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userService.GetByUsernameAsync(dto.Username);
            if (user == null)
                return NotFound(new { message = "User not found" });

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid password" });

            var accessToken = GenerateJwtToken(user, 15);

            var refreshToken = await _refreshTokenService.CreateAsync(user.Id);

            Response.Cookies.Append("jwt", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddMinutes(15)
            });

            Response.Cookies.Append("refreshToken", refreshToken.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = refreshToken.Expires
            });

            return Ok(new { message = "Login successful", username = user.Username, role = user.Role });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var token))
                return Unauthorized(new { message = "No refresh token provided" });

            var existing = await _refreshTokenService.GetByTokenAsync(token);
            if (existing == null || !existing.IsActive)
                return Unauthorized(new { message = "Invalid or expired refresh token" });

            // Create new refresh token
            var newRefresh = await _refreshTokenService.CreateAsync(existing.UserId);
            // Revoke old token
            await _refreshTokenService.RevokeAsync(existing);

            var accessToken = GenerateJwtToken(existing.User!, 15);

            Response.Cookies.Append("jwt", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddMinutes(15)
            });

            Response.Cookies.Append("refreshToken", newRefresh.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = newRefresh.Expires
            });

            return Ok(new { message = "Token refreshed successfully" });
        }

        [Authorize]
        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            var username = User.Identity?.Name;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = User.FindFirst("UserId")?.Value;

            return Ok(new { userId, username, role });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.TryGetValue("refreshToken", out var token))
                await _refreshTokenService.RevokeByTokenAsync(token);

            Response.Cookies.Delete("refreshToken", new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None });
            Response.Cookies.Delete("jwt", new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None });

            return Ok(new { message = "Logged out successfully" });
        }

        private string GenerateJwtToken(User user, int minutesValid)
        {
            var key = _config["JWT:Secret"];
            var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("UserId", user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: _config["JWT:ValidIssuer"],       
                audience: _config["JWT:ValidAudience"],   
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(minutesValid),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}