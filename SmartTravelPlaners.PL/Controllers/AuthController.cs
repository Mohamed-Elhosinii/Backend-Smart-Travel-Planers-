using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.RateLimiting;
using SmartTravelPlaners.BLL.DTOs.Auth;
using SmartTravelPlaners.BLL.DTOs.Common;
using SmartTravelPlaners.BLL.Services.Abstract;
using System.Security.Claims;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("AuthPolicy")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                _logger.LogInformation("User registration initiated for email: {Email}", dto.Email);
                var result = await _authService.RegisterAsync(dto);
                _logger.LogInformation("User registered successfully. UserId: {UserId}", result);
                return Ok(ApiResponse<string>.Success(result, "Registration successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User registration failed for email: {Email}. Error: {ErrorMessage}", dto.Email, ex.Message);
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
                _logger.LogInformation("User login attempt for email: {Email}", dto.Email);
                var result = await _authService.LoginAsync(dto);
                _logger.LogInformation("User logged in successfully. UserId: {UserId}", result.UserId);
                return Ok(ApiResponse<AuthResponseDto>.Success(result, "Login successful"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Login failed for email: {Email}. Error: {ErrorMessage}", dto.Email, ex.Message);
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
                _logger.LogInformation("Token refresh requested");
                var result = await _authService.RefreshTokenAsync(refreshToken);
                _logger.LogInformation("Token refreshed successfully for UserId: {UserId}", result.UserId);
                return Ok(ApiResponse<AuthResponseDto>.Success(result, "Token refreshed successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Token refresh failed. Error: {ErrorMessage}", ex.Message);
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
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation("User logout initiated. UserId: {UserId}", userId);
                var result = await _authService.LogoutAsync(refreshToken);
                if (!result)
                {
                    _logger.LogWarning("Logout failed due to invalid token for UserId: {UserId}", userId);
                    return BadRequest(ApiResponse.Failure("Invalid token"));
                }

                _logger.LogInformation("User logged out successfully. UserId: {UserId}", userId);
                return Ok(ApiResponse.Success("Logged out successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed. Error: {ErrorMessage}", ex.Message);
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
                _logger.LogInformation("Email confirmation initiated for UserId: {UserId}", dto.UserId);
                var result = await _authService.ConfirmEmailAsync(dto);
                if (!result) 
                {
                    _logger.LogWarning("Email confirmation failed for UserId: {UserId}", dto.UserId);
                    return BadRequest(ApiResponse.Failure("Email confirmation failed"));
                }

                _logger.LogInformation("Email confirmed successfully for UserId: {UserId}", dto.UserId);
                return Ok(ApiResponse.Success("Email confirmed successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email confirmation failed. Error: {ErrorMessage}", ex.Message);
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
                _logger.LogInformation("Resend confirmation email requested for UserId: {UserId}", userId);
                await _authService.SendConfirmEmailAsync(userId);
                _logger.LogInformation("Confirmation email sent successfully to UserId: {UserId}", userId);
                return Ok(ApiResponse.Success("Email sent successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resend confirmation email failed for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
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
                _logger.LogInformation("Password reset requested for email: {Email}", dto.Email);
                await _authService.SendForgotPasswordEmailAsync(dto);
                _logger.LogInformation("Password reset email sent for email: {Email}", dto.Email);
                return Ok(ApiResponse.Success("If this email exists, a reset link has been sent"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset request failed for email: {Email}. Error: {ErrorMessage}", dto.Email, ex.Message);
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
                _logger.LogInformation("Password reset initiated");
                var result = await _authService.ResetPasswordAsync(dto);
                _logger.LogInformation("Password reset completed successfully");
                return Ok(ApiResponse.Success("Password reset successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset failed. Error: {ErrorMessage}", ex.Message);
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
            var frontendUrl = _configuration["FrontendUrl"] ?? "https://frontend-smart-travel-planers.vercel.app";
            try
            {
                _logger.LogInformation("Google authentication callback initiated");
                var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("Google authentication failed");
                    return Redirect($"{frontendUrl}/login?error=google_auth_failed");
                }

                var email = result.Principal.FindFirstValue(ClaimTypes.Email)!;
                var fullName = result.Principal.FindFirstValue(ClaimTypes.Name)!;
                var providerKey = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;

                _logger.LogInformation("Google user authenticated successfully. Email: {Email}", email);
                var response = await _authService.OAuthLoginAsync(email, fullName, "Google", providerKey);

                _logger.LogInformation("Google OAuth login completed successfully for UserId: {UserId}", response.UserId);
                var redirectUrl = $"{frontendUrl}/google-callback" +
                                  $"?userId={Uri.EscapeDataString(response.UserId)}" +
                                  $"{(!string.IsNullOrEmpty(response.FullName) ? $"&fullName={Uri.EscapeDataString(response.FullName)}" : "")}" +
                                  $"&email={Uri.EscapeDataString(response.Email)}" +
                                  $"&accessToken={Uri.EscapeDataString(response.AccessToken)}" +
                                  $"&refreshToken={Uri.EscapeDataString(response.RefreshToken)}";

                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google authentication callback failed. Error: {ErrorMessage}", ex.Message);
                return Redirect($"{frontendUrl}/login?error=google_auth_failed");
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
                _logger.LogInformation("Current user profile retrieval requested for UserId: {UserId}", userId);
                var result = await _authService.GetCurrentUserAsync(userId!);
                _logger.LogInformation("Current user profile retrieved successfully for UserId: {UserId}", userId);
                return Ok(ApiResponse<UserProfileDto>.Success(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Current user profile retrieval failed. Error: {ErrorMessage}", ex.Message);
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
                _logger.LogInformation("User profile update initiated for UserId: {UserId}", userId);
                await _authService.UpdateProfileAsync(userId!, dto);
                _logger.LogInformation("User profile updated successfully for UserId: {UserId}", userId);
                return Ok(ApiResponse.Success("Profile updated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User profile update failed. Error: {ErrorMessage}", ex.Message);
                return BadRequest(ApiResponse.Failure(ex.Message));
            }
        }
    }
}