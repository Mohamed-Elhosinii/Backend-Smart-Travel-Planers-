using SmartTravelPlaners.BLL.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartTravelPlaners.BLL.Services.Abstract
{
    public  interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
        Task<bool> LogoutAsync(string refreshToken);

        Task SendConfirmEmailAsync(string userId);
        Task<bool> ConfirmEmailAsync(ConfirmEmailDto dto);
        Task SendForgotPasswordEmailAsync(ForgotPasswordDto dto);
        Task<bool> ResetPasswordAsync(ResetPasswordDto dto);
        Task<UserProfileDto> GetCurrentUserAsync(string userId);


        Task<AuthResponseDto> OAuthLoginAsync(string email, string fullName, string provider);
    }
}
