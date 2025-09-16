using Application.Authentication.DTO;
using Application.Authentication.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationApplicationService _authService;

        public AuthController(IAuthenticationApplicationService authenticationApplicationService)
        {
            _authService = authenticationApplicationService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            var result = await _authService.Authenticate(loginRequest.Login, loginRequest.Password);

            if (result.IsSuccess)
                return Ok(result.Value);

            return Unauthorized(result.Error);
        }

        [HttpPost("refresh")]
        public IActionResult RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = _authService.RefreshToken(request.RefreshToken);

            if (result.IsSuccess)
                return Ok(result.Value);

            return Unauthorized(result.Error);
        }

        [HttpPost("logout")]
        public IActionResult Logout([FromBody] RefreshTokenRequest request)
        {
            var result = _authService.Logout(request.RefreshToken);
            return result.IsSuccess ? Ok() : BadRequest(result.Error);
        }
    }
}
