using SmartTravelPlaners.BLL.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Services.Abstract
{
    public  interface IAuthService
    {
        Task<string> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
        Task<bool> LogoutAsync(string refreshToken);

        Task SendConfirmEmailAsync(string userId);
        Task<bool> ConfirmEmailAsync(ConfirmEmailDto dto);
        Task SendForgotPasswordEmailAsync(ForgotPasswordDto dto);
        Task<bool> ResetPasswordAsync(ResetPasswordDto dto);
        Task<UserProfileDto> GetCurrentUserAsync(string userId);
        Task UpdateProfileAsync(string userId, UpdateProfileDto dto);


        Task<AuthResponseDto> OAuthLoginAsync(string email, string fullName, string provider, string providerKey);
    }
}
