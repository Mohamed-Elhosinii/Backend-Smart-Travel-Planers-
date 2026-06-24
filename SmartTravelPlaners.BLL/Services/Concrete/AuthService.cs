using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartTravelPlaners.BLL.DTOs.Auth;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Services.Abstract;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SmartTravelPlaners.BLL.Services.Concrete
{

    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ISubscriptionService _subscriptionService;

        public AuthService(UserManager<ApplicationUser> userManager,
                           IConfiguration configuration,
                           ApplicationDbContext context,
                              IEmailService emailService,
                              ISubscriptionService subscriptionService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _emailService = emailService;
            _subscriptionService = subscriptionService;
        }

        // ============================================================
        // REGISTER
        // ============================================================
        public async Task<string> RegisterAsync(RegisterDto dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                throw new Exception("Email already exists");

            var user = new ApplicationUser
            {
                FullName = dto.FullName,
                Email = dto.Email,
                UserName = dto.Email
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
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

            return user.Id;
        }

        // ============================================================
        // LOGIN
        // ============================================================
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                throw new Exception("Invalid email or password");

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!isPasswordValid)
                throw new Exception("Invalid email or password");

            return await GenerateAuthResponseAsync(user);
        }

        // ============================================================
        // REFRESH TOKEN
        // ============================================================
        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            var token = await _context.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == refreshToken);

            if (token == null || !token.IsActive)
                throw new Exception("Invalid or expired refresh token");

            // Revoke old token
            token.IsRevoked = true;
            _context.RefreshTokens.Update(token);
            await _context.SaveChangesAsync();

            return await GenerateAuthResponseAsync(token.User);
        }

        // ============================================================
        // LOGOUT
        // ============================================================
        public async Task<bool> LogoutAsync(string refreshToken)
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == refreshToken);

            if (token == null || !token.IsActive)
                return false;

            token.IsRevoked = true;
            _context.RefreshTokens.Update(token);
            await _context.SaveChangesAsync();

            return true;
        }

        // ============================================================
        // SEND CONFIRM EMAIL
        // ============================================================
        public async Task SendConfirmEmailAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Exception("User not found");

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
        }

        // ============================================================
        // CONFIRM EMAIL
        // ============================================================
        public async Task<bool> ConfirmEmailAsync(ConfirmEmailDto dto)
        {
            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null) throw new Exception("User not found");

            // dto.Token is now the 6-digit OTP code directly
            var result = await _userManager.ConfirmEmailAsync(user, dto.Token);

            return result.Succeeded;
        }

        // ============================================================
        // FORGOT PASSWORD
        // ============================================================
        public async Task SendForgotPasswordEmailAsync(ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return; // مش بنقول للمستخدم إن الإيميل مش موجود (security)

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
        }

        // ============================================================
        // RESET PASSWORD
        // ============================================================
        public async Task<bool> ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) throw new Exception("User not found");

            // dto.Token is now the 6-digit OTP
            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

            if (!result.Succeeded)
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

            return true;
        }

        public async Task<AuthResponseDto> OAuthLoginAsync(string email, string fullName, string provider)
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
                    throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
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

            return await GenerateAuthResponseAsync(user);
        }

        public async Task<UserProfileDto> GetCurrentUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Exception("User not found");

            return new UserProfileDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                EmailConfirmed = user.EmailConfirmed
            };
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