using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.DTOs.Auth;
using SmartTravelPlaners.BLL.Services.Abstract;
using System.Security.Claims;

namespace SmartTravelPlaners.PL.Controllers
{
 
       
        [ApiController]
        [Route("api/[controller]")]
        public class AuthController : ControllerBase
        {
            private readonly IAuthService _authService;

            public AuthController(IAuthService authService)
            {
                _authService = authService;
            }

            [HttpPost("register")]
            public async Task<IActionResult> Register([FromBody] RegisterDto dto)
            {
                try
                {
                    var result = await _authService.RegisterAsync(dto);
                    return Ok(result);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = ex.Message });
                }
            }

            [HttpPost("login")]
            [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
            public async Task<IActionResult> Login([FromBody] LoginDto dto)
            {
                try
                {
                    var result = await _authService.LoginAsync(dto);
                    return Ok(result);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = ex.Message });
                }
            }

            [HttpPost("refresh-token")]
            public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
            {
                try
                {
                    var result = await _authService.RefreshTokenAsync(refreshToken);
                    return Ok(result);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = ex.Message });
                }
            }

            [HttpPost("logout")]
            [Authorize]
            public async Task<IActionResult> Logout([FromBody] string refreshToken)
            {
                var result = await _authService.LogoutAsync(refreshToken);
                if (!result)
                    return BadRequest(new { message = "Invalid token" });

                return Ok(new { message = "Logged out successfully" });
            }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] ConfirmEmailDto dto)
        {
            try
            {
                var result = await _authService.ConfirmEmailAsync(dto);
                if (!result) return BadRequest(new { message = "Email confirmation failed" });
                return Ok(new { message = "Email confirmed successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            await _authService.SendForgotPasswordEmailAsync(dto);
            return Ok(new { message = "If this email exists, a reset link has been sent" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(dto);
                return Ok(new { message = "Password reset successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // ============================================================
        // GOOGLE
        // ============================================================
        [HttpGet("google-login")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleCallback")
            };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                return BadRequest(new { message = "Google authentication failed" });

            var email = result.Principal.FindFirstValue(ClaimTypes.Email)!;
            var fullName = result.Principal.FindFirstValue(ClaimTypes.Name)!;

            var response = await _authService.OAuthLoginAsync(email, fullName, "Google");
            return Ok(response);
        }

    

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            try
            {
                var result = await _authService.GetCurrentUserAsync(userId!);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

    }

 }