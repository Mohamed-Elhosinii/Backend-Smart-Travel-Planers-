
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SmartTravelPlaners.BLL.DTOs.Auth;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Services;
using SmartTravelPlaners.BLL.Services.Abstract;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SmartTravelPlaners.BLL.Services.Concrete
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(UserManager<ApplicationUser> userManager,
                           IConfiguration configuration,
                           ApplicationDbContext context,
                           IEmailService emailService,
                           ISubscriptionService subscriptionService,
                           ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _emailService = emailService; // صح
            _subscriptionService = subscriptionService; // صح
            _logger = logger;
        }

        // ============================================================
        // REGISTER
        // ============================================================
        public async Task<string> RegisterAsync(RegisterDto dto)
        {
            try
            {
                var existingUser = await _userManager.FindByEmailAsync(dto.Email);
                if (existingUser != null)
                {
                    _logger.LogWarning("Register attempt with existing email: {Email}", dto.Email);
                    throw new Exception("Email already exists");
                }

                var user = new ApplicationUser
                {
                    FullName = dto.FullName,
                    Email = dto.Email,
                    UserName = dto.Email
                };

                var result = await _userManager.CreateAsync(user, dto.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create user {Email}: {Errors}", dto.Email, errors);
                    throw new Exception(errors);
                }

                _context.UserProfiles.Add(new UserProfile
                {
                    Id = Guid.NewGuid(),
                    AspNetUserId = user.Id,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                // Create default Free subscription for the new user
                await _subscriptionService.EnsureDefaultSubscriptionAsync(user.Id);

                await SendConfirmEmailAsync(user.Id);

                _logger.LogInformation("User registered: {Email}, UserId: {UserId}", user.Email, user.Id);

                return user.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Register failed for {Email}", dto.Email);
                throw;
            }
        }

        // ============================================================
        // LOGIN
        // ============================================================
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(dto.Email);
                if (user == null)
                {
                    _logger.LogWarning("Login failed, user not found: {Email}", dto.Email);
                    throw new Exception("Invalid email or password");
                }

                var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
                if (!isPasswordValid)
                {
                    _logger.LogWarning("Login failed, invalid password attempt: {Email}", dto.Email);
                    throw new Exception("Invalid email or password");
                }

                if (!user.EmailConfirmed)
                {
                    _logger.LogWarning("Login failed, email not confirmed: {Email}", dto.Email);
                    throw new Exception("Email is not confirmed. Please verify your email first.");
                }

                var response = await GenerateAuthResponseAsync(user);
                _logger.LogInformation("User logged in: {Email}, UserId: {UserId}", user.Email, user.Id);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for {Email}", dto.Email);
                throw;
            }
        }

        // ============================================================
        // REFRESH TOKEN
        // ============================================================
        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var token = await _context.RefreshTokens
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Token == refreshToken);

                if (token == null || !token.IsActive)
                {
                    _logger.LogWarning("Refresh token invalid or expired");
                    throw new Exception("Invalid or expired refresh token");
                }

                // Revoke old token
                token.IsRevoked = true;
                _context.RefreshTokens.Update(token);
                await _context.SaveChangesAsync();

                var response = await GenerateAuthResponseAsync(token.User);
                _logger.LogInformation("Refresh token used for user {UserId}", token.UserId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token error");
                throw;
            }
        }

        // ============================================================
        // LOGOUT
        // ============================================================
        public async Task<bool> LogoutAsync(string refreshToken)
        {
            try
            {
                var token = await _context.RefreshTokens
                    .FirstOrDefaultAsync(r => r.Token == refreshToken);

                if (token == null || !token.IsActive)
                {
                    _logger.LogWarning("Logout failed: refresh token not found or inactive");
                    return false;
                }

                token.IsRevoked = true;
                _context.RefreshTokens.Update(token);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User logged out: {UserId}", token.UserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed");
                throw;
            }
        }

        // ============================================================
        // SEND CONFIRM EMAIL
        // ============================================================
        public async Task SendConfirmEmailAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("SendConfirmEmailAsync: user not found {UserId}", userId);
                    throw new Exception("User not found");
                }

                _logger.LogInformation("Sending confirmation email to {Email}", user.Email);

                // Generates a 6-digit OTP because we set EmailConfirmationTokenProvider to DefaultEmailProvider
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                var body = $@"
        <div style='font-family: Arial, sans-serif; text-align: center; padding: 20px;'>
            <h2>Confirm Your Email</h2>
            <p>Your one-time verification code is:</p>
            <div style='font-size: 24px; font-weight: bold; padding: 15px; background: #f4f4f4; border-radius: 8px; display: inline-block; letter-spacing: 5px;'>
                {token}
            </div>
            <p style='margin-top: 20px; color: #777;'>This code will expire shortly.</p>
        </div>
    ";

                await _emailService.SendEmailAsync(user.Email!, "Your Verification Code", body);
                _logger.LogInformation("Confirmation email queued for {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendConfirmEmailAsync failed for user {UserId}", userId);
                throw;
            }
        }

        // ============================================================
        // CONFIRM EMAIL
        // ============================================================
        public async Task<bool> ConfirmEmailAsync(ConfirmEmailDto dto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(dto.UserId);
                if (user == null)
                {
                    _logger.LogWarning("ConfirmEmailAsync: user not found {UserId}", dto.UserId);
                    throw new Exception("User not found");
                }

                // dto.Token is now the 6-digit OTP code directly
                var result = await _userManager.ConfirmEmailAsync(user, dto.Token);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Email confirmed for user {UserId}", dto.UserId);
                    return true;
                }

                _logger.LogWarning("Email confirmation failed for user {UserId}", dto.UserId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConfirmEmailAsync error for user {UserId}", dto.UserId);
                throw;
            }
        }

        // ============================================================
        // FORGOT PASSWORD
        // ============================================================
        public async Task SendForgotPasswordEmailAsync(ForgotPasswordDto dto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(dto.Email);
                if (user == null)
                {
                    _logger.LogWarning("Forgot password requested for non-existing email: {Email}", dto.Email);
                    return; // security: do not reveal existence
                }

                // Generate 6-digit OTP
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                var body = $@"
        <div style='font-family: Arial, sans-serif; text-align: center; padding: 20px;'>
            <h2>Reset Your Password</h2>
            <p>Your password reset code is:</p>
            <div style='font-size: 24px; font-weight: bold; padding: 15px; background: #f4f4f4; border-radius: 8px; display: inline-block; letter-spacing: 5px;'>
                {token}
            </div>
            <p style='margin-top: 20px; color: #777;'>This code will expire shortly.</p>
        </div>
    ";

                await _emailService.SendEmailAsync(user.Email!, "Reset Your Password", body);
                _logger.LogInformation("Password reset email queued for {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendForgotPasswordEmailAsync failed for {Email}", dto.Email);
                throw;
            }
        }

        // ============================================================
        // RESET PASSWORD
        // ============================================================
        public async Task<bool> ResetPasswordAsync(ResetPasswordDto dto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(dto.Email);
                if (user == null)
                {
                    _logger.LogWarning("ResetPasswordAsync: user not found for email {Email}", dto.Email);
                    throw new Exception("User not found");
                }

                // dto.Token is now the 6-digit OTP
                var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Reset password failed for {Email}: {Errors}", dto.Email, errors);
                    throw new Exception(errors);
                }

                _logger.LogInformation("Password reset successful for {Email}", dto.Email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ResetPasswordAsync error for {Email}", dto.Email);
                throw;
            }
        }

        public async Task<AuthResponseDto> OAuthLoginAsync(string email, string fullName, string provider, string providerKey)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    // Register تلقائي
                    user = new ApplicationUser
                    {
                        FullName = fullName,
                        Email = email,
                        UserName = email,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded)
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogError("OAuth registration failed for {Email} (provider {Provider}): {Errors}", email, provider, errors);
                        throw new Exception(errors);
                    }

                    _logger.LogInformation("OAuth user created: {Email} (provider: {Provider})", email, provider);
                }

                // Link the external login if it doesn't exist already
                var existingLogins = await _userManager.GetLoginsAsync(user);
                if (!existingLogins.Any(l => l.LoginProvider == provider && l.ProviderKey == providerKey))
                {
                    var loginInfo = new UserLoginInfo(provider, providerKey, provider);
                    var addLoginResult = await _userManager.AddLoginAsync(user, loginInfo);
                    if (!addLoginResult.Succeeded)
                    {
                        var errors = string.Join(", ", addLoginResult.Errors.Select(e => e.Description));
                        _logger.LogError("Failed to add external login for {Email} (provider {Provider}): {Errors}", email, provider, errors);
                        throw new Exception(errors);
                    }
                }

                // Ensure UserProfile exists (fixes accounts that were created without one)
                var userProfileExists = await _context.UserProfiles.AnyAsync(up => up.AspNetUserId == user.Id);
                if (!userProfileExists)
                {
                    _context.UserProfiles.Add(new UserProfile
                    {
                        Id = Guid.NewGuid(),
                        AspNetUserId = user.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    // Create default Free subscription
                    await _subscriptionService.EnsureDefaultSubscriptionAsync(user.Id);
                }

                _logger.LogInformation("OAuth login successful for {Email} (provider: {Provider})", email, provider);
                return await GenerateAuthResponseAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OAuthLoginAsync failed for {Email} (provider: {Provider})", email, provider);
                throw;
            }
        }

        public async Task<UserProfileDto> GetCurrentUserAsync(string userId)
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                    .ThenInclude(p => p.Subscriptions)
                        .ThenInclude(s => s.Plan)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) throw new Exception("User not found");

            var nameParts = user.FullName?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries) ?? new[] { "User", "" };
            var firstName = nameParts.Length > 0 ? nameParts[0] : "";
            var lastName = nameParts.Length > 1 ? nameParts[1] : "";

            var activeSub = user.Profile?.Subscriptions
                .FirstOrDefault(s => s.Status == DAL.Enums.SubscriptionStatus.Active && s.CurrentPeriodEnd >= DateTime.UtcNow);

            return new UserProfileDto
            {
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                Email = user.Email!,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                Country = user.Profile?.Country,
                CurrentPlan = activeSub?.Plan?.Name ?? "Free Plan"
            };
        }

        public async Task UpdateProfileAsync(string userId, UpdateProfileDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) throw new Exception("User not found");

            user.FullName = $"{dto.FirstName.Trim()} {dto.LastName.Trim()}".Trim();
            user.PhoneNumber = dto.PhoneNumber;

            if (user.Profile != null)
            {
                user.Profile.Country = dto.Country;
            }
            else
            {
                user.Profile = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    AspNetUserId = user.Id,
                    Country = dto.Country
                };
                _context.UserProfiles.Add(user.Profile);
            }

            await _context.SaveChangesAsync();
        }



        // ============================================================
        // HELPERS
        // ============================================================
        private async Task<AuthResponseDto> GenerateAuthResponseAsync(ApplicationUser user)
        {
            var accessToken = await GenerateJwtTokenAsync(user);
            var refreshToken = await CreateRefreshTokenAsync(user.Id);

            return new AuthResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                AccessToken = accessToken.Token,
                AccessTokenExpiry = accessToken.Expiry,
                RefreshToken = refreshToken.Token
            };
        }

        private async Task<(string Token, DateTime Expiry)> GenerateJwtTokenAsync(ApplicationUser user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
            var expiry = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpiryInMinutes"]!));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: expiry,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
        }

        private async Task<RefreshToken> CreateRefreshTokenAsync(string userId)
        {
            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                ExpiresAt = DateTime.UtcNow.AddDays(
                    double.Parse(_configuration["JwtSettings:RefreshTokenExpiryInDays"]!)),
                UserId = userId
            };

            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }
    }
}
