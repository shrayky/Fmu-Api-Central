using Application.Authentication.DTO;
using Application.Authentication.Interfaces;
using Domain.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationApplicationService _authService;

        public AuthController(IAuthenticationApplicationService authenticationApplicationService)
        {
            _authService = authenticationApplicationService;
        }

        [HttpPost("login")]
        [EnableRateLimiting(AuthRateLimit.Login)]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            var result = await _authService.Authenticate(loginRequest.Login, loginRequest.Password);

            if (result.IsSuccess)
                return Ok(result.Value);

            return Unauthorized(result.Error);
        }

        [HttpPost("refresh")]
        [EnableRateLimiting(AuthRateLimit.Refresh)]
        public IActionResult RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = _authService.RefreshToken(request.RefreshToken);

            if (result.IsSuccess)
                return Ok(result.Value);

            return Unauthorized(result.Error);
        }

        [HttpPost("change-password")]
        [EnableRateLimiting(AuthRateLimit.ChangePassword)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var result = await _authService.ChangePassword(
                request.Login,
                request.CurrentPassword,
                request.NewPassword);

            if (result.IsSuccess)
                return Ok();

            if (result.Error == "Неверный логин или пароль")
                return Unauthorized(result.Error);

            return BadRequest(result.Error);
        }

        [HttpPost("logout")]
        public IActionResult Logout([FromBody] RefreshTokenRequest request)
        {
            var result = _authService.Logout(request.RefreshToken);
            return result.IsSuccess ? Ok() : BadRequest(result.Error);
        }
    }
}
