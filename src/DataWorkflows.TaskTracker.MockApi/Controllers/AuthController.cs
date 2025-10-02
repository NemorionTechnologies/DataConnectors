using DataWorkflows.TaskTracker.MockApi.Models;
using DataWorkflows.TaskTracker.MockApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataWorkflows.TaskTracker.MockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Login with username and password to receive a bearer token.
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        var token = _authService.Login(request.Username, request.Password);

        if (token == null)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        return Ok(new LoginResponse(
            Token: token,
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        ));
    }

    /// <summary>
    /// Logout and revoke the current token.
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var token = ExtractToken();
        if (token != null)
        {
            _authService.RevokeToken(token);
        }

        return Ok(new { message = "Logged out successfully" });
    }

    private string? ExtractToken()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ") == true)
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }
        return null;
    }
}
