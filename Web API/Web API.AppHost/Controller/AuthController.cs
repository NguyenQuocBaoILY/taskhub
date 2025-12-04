using BLL.DTOs;
using BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Web_API.AppHost.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RegisterAsync(request);
            
            return result.User != null 
                ? Ok(result) 
                : BadRequest(new { message = result.Message });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var ipAddress = GetIpAddress();
            var userAgent = Request.Headers["User-Agent"].ToString();

            var result = await _authService.LoginAsync(request, ipAddress, userAgent);
            
            return !string.IsNullOrEmpty(result.AccessToken) 
                ? Ok(result) 
                : Unauthorized(new { message = result.Message });
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var ipAddress = GetIpAddress();
            var userAgent = Request.Headers["User-Agent"].ToString();

            var result = await _authService.GoogleLoginAsync(request, ipAddress, userAgent);
            
            return !string.IsNullOrEmpty(result.AccessToken) 
                ? Ok(result) 
                : BadRequest(new { message = result.Message });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RefreshTokenAsync(request);
            
            return !string.IsNullOrEmpty(result.AccessToken) 
                ? Ok(result) 
                : Unauthorized(new { message = result.Message });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            string userId = null;
            
            
            if (User.Identity.IsAuthenticated)
            {
                userId = User.FindFirst("userId")?.Value;
            }


            if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(request?.RefreshToken))
            {
                return BadRequest(new { message = "Unable to identify user" });
            }

            var success = await _authService.LogoutAsync(request, userId);
            
            return success 
                ? Ok(new { message = "Logout successful" }) 
                : BadRequest(new { message = "Logout failed" });
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            var userId = User.FindFirst("userId")?.Value;
            var name = User.FindFirst("name")?.Value;
            var email = User.FindFirst("email")?.Value;
            var isAdmin = User.FindFirst("isAdmin")?.Value;
            return Ok(new { userId, name, email, isAdmin});
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ForgotPasswordAsync(request);
            
            return result.Success 
                ? Ok(result) 
                : BadRequest(result);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ResetPasswordAsync(request);
            
            return result.User != null 
                ? Ok(result) 
                : BadRequest(new { message = result.Message });
        }

        [HttpPost("cognito-register")]
        public async Task<IActionResult> CognitoRegister([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RegisterWithCognitoAsync(request); // ✅ Đúng
            
            return result.User != null 
                ? Ok(result) 
                : BadRequest(new { message = result.Message });
        }

        [HttpPost("cognito-login")]
        public async Task<IActionResult> CognitoLogin([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var ipAddress = GetIpAddress();
            var userAgent = Request.Headers["User-Agent"].ToString();

            var result = await _authService.CognitoLoginAsync(request, ipAddress, userAgent);
            
            return !string.IsNullOrEmpty(result.AccessToken) 
                ? Ok(result) 
                : Unauthorized(new { message = result.Message });
        }

        [HttpPost("cognito-confirm")]
        public async Task<IActionResult> CognitoConfirmSignUp([FromBody] CognitoConfirmSignUpRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ConfirmCognitoSignUpAsync(request);
            
            return result.Success 
                ? Ok(result) 
                : BadRequest(new { message = result.Message });
        }

        [HttpPost("cognito-resend-confirmation")]
        public async Task<IActionResult> ResendConfirmationCode([FromBody] CognitoResendConfirmationRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ResendCognitoConfirmationCodeAsync(request);
            
            return result.Success 
                ? Ok(result) 
                : BadRequest(new { message = result.Message });
        }

        private string GetIpAddress()
        {
            return Request.Headers.ContainsKey("X-Forwarded-For")
                ? Request.Headers["X-Forwarded-For"]
                : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}