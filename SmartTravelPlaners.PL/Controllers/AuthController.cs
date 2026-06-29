using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.DTOs.Auth;
using SmartTravelPlaners.BLL.DTOs.Common;
using SmartTravelPlaners.BLL.Services.Abstract;
using System.Security.Claims;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;

        public AuthController(IAuthService authService, IConfiguration configuration)
        {
            _authService = authService;
            _configuration = configuration;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                var result = await _authService.RegisterAsync(dto);
                return Ok(ApiResponse<string>.Success(result, "Registration successful"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Failure(ex.Message));
            }
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var result = await _authService.LoginAsync(dto);
                return Ok(ApiResponse<AuthResponseDto>.Success(result, "Login successful"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Failure(ex.Message));
            }
        }

        [HttpPost("refresh-token")]
        [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(refreshToken);
                return Ok(ApiResponse<AuthResponseDto>.Success(result, "Token refreshed successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Failure(ex.Message));
            }
        }

        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Logout([FromBody] string refreshToken)
        {
            try
            {
                var result = await _authService.LogoutAsync(refreshToken);
                if (!result)
                    return BadRequest(ApiResponse.Failure("Invalid token"));

                return Ok(ApiResponse.Success("Logged out successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Failure(ex.Message));
            }
        }

        [HttpGet("confirm-email")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmEmail([FromQuery] ConfirmEmailDto dto)
        {
            try
            {
                var result = await _authService.ConfirmEmailAsync(dto);
                if (!result) 
                    return BadRequest(ApiResponse.Failure("Email confirmation failed"));
                return Ok(ApiResponse.Success("Email confirmed successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Failure(ex.Message));
            }
        }

        [HttpPost("resend-confirm-email")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResendConfirmEmail([FromQuery] string userId)
        {
            try
            {
                await _authService.SendConfirmEmailAsync(userId);
                return Ok(ApiResponse.Success("Email sent successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Failure(ex.Message));
            }
        }

        [HttpPost("forgot-password")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            try
            {
                await _authService.SendForgotPasswordEmailAsync(dto);
                return Ok(ApiResponse.Success("If this email exists, a reset link has been sent"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Failure(ex.Message));
            }
        }

        [HttpPost("reset-password")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(dto);
                return Ok(ApiResponse.Success("Password reset successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Failure(ex.Message));
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
            try
            {
                var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
                if (!result.Succeeded)
                {
                    // Redirect to Angular with error
                    return Redirect("http://localhost:4200/auth/login?error=google_auth_failed");
                }

                var email = result.Principal.FindFirstValue(ClaimTypes.Email)!;
                var fullName = result.Principal.FindFirstValue(ClaimTypes.Name)!;
                var providerKey = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;

                var response = await _authService.OAuthLoginAsync(email, fullName, "Google", providerKey);

                // Redirect back to Angular app with tokens in query params
                var redirectUrl = $"http://localhost:4200/google-callback" +
                                  $"?userId={Uri.EscapeDataString(response.UserId)}" +
                                  $"{(!string.IsNullOrEmpty(response.FullName) ? $"&fullName={Uri.EscapeDataString(response.FullName)}" : "")}" +
                                  $"&email={Uri.EscapeDataString(response.Email)}" +
                                  $"&accessToken={Uri.EscapeDataString(response.AccessToken)}" +
                                  $"&refreshToken={Uri.EscapeDataString(response.RefreshToken)}";

                return Redirect(redirectUrl);
            }
            catch
            {
                return Redirect("http://localhost:4200/auth/login?error=google_auth_failed");
            }
        }

        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var result = await _authService.GetCurrentUserAsync(userId!);
                return Ok(ApiResponse<UserProfileDto>.Success(result));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Failure(ex.Message));
            }
        }
        [HttpPut("me")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateProfileDto dto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                await _authService.UpdateProfileAsync(userId!, dto);
                return Ok(ApiResponse.Success("Profile updated successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Failure(ex.Message));
            }
        }
    }
}