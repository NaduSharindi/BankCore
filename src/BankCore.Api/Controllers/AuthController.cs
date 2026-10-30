using BankCore.Api.Authentication;
using BankCore.Api.Extensions;
using BankCore.Application.Dtos;
using BankCore.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankCore.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(AuthService authService, ITokenService tokenService) : ControllerBase
{
    /// <summary>Signs in a staff user and returns a short-lived JWT access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.LoginPolicy)]
    [ProducesResponseType<AccessToken>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AccessToken>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.LoginAsync(request, cancellationToken);
        return Ok(tokenService.CreateToken(user));
    }
}
