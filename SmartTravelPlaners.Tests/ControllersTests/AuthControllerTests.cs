using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using SmartTravelPlaners.BLL.DTOs.Auth;
using SmartTravelPlaners.BLL.Services.Abstract;
using SmartTravelPlaners.PL.Controllers;
using System.Security.Claims;
using Xunit;

namespace SmartTravelPlaners.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService> _serviceMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _serviceMock = new Mock<IAuthService>();
            _configMock = new Mock<IConfiguration>();
            _controller = new AuthController(_serviceMock.Object, _configMock.Object);
        }

        private void SetupUser(string userId = "user-1")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        private AuthResponseDto MakeAuthResponse() => new AuthResponseDto
        {
            UserId = "user-1",
            FullName = "Test User",
            Email = "test@example.com",
            AccessToken = "fake-token",
            RefreshToken = "fake-refresh"
        };

        // ============================================================
        // Register
        // ============================================================

        [Fact]
        public async Task Register_ShouldReturn200_WhenSuccess()
        {
            var dto = new RegisterDto { Email = "test@example.com", Password = "Pass123!", FullName = "Test" };
            _serviceMock.Setup(s => s.RegisterAsync(dto)).ReturnsAsync("user-1");

            var result = await _controller.Register(dto) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task Register_ShouldReturn400_WhenFails()
        {
            var dto = new RegisterDto { Email = "test@example.com", Password = "weak", FullName = "Test" };
            _serviceMock.Setup(s => s.RegisterAsync(dto)).ThrowsAsync(new Exception("Email already exists"));

            var result = await _controller.Register(dto) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // Login
        // ============================================================

        [Fact]
        public async Task Login_ShouldReturn200_WhenSuccess()
        {
            var dto = new LoginDto { Email = "test@example.com", Password = "Pass123!" };
            _serviceMock.Setup(s => s.LoginAsync(dto)).ReturnsAsync(MakeAuthResponse());

            var result = await _controller.Login(dto) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task Login_ShouldReturn400_WhenInvalidCredentials()
        {
            var dto = new LoginDto { Email = "test@example.com", Password = "wrong" };
            _serviceMock.Setup(s => s.LoginAsync(dto)).ThrowsAsync(new Exception("Invalid email or password"));

            var result = await _controller.Login(dto) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // RefreshToken
        // ============================================================

        [Fact]
        public async Task RefreshToken_ShouldReturn200_WhenValid()
        {
            _serviceMock.Setup(s => s.RefreshTokenAsync("valid-token")).ReturnsAsync(MakeAuthResponse());

            var result = await _controller.RefreshToken("valid-token") as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task RefreshToken_ShouldReturn400_WhenInvalid()
        {
            _serviceMock.Setup(s => s.RefreshTokenAsync("invalid-token"))
                .ThrowsAsync(new Exception("Invalid or expired refresh token"));

            var result = await _controller.RefreshToken("invalid-token") as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // Logout
        // ============================================================

        [Fact]
        public async Task Logout_ShouldReturn200_WhenSuccess()
        {
            SetupUser();
            _serviceMock.Setup(s => s.LogoutAsync("valid-token")).ReturnsAsync(true);

            var result = await _controller.Logout("valid-token") as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task Logout_ShouldReturn400_WhenTokenInvalid()
        {
            SetupUser();
            _serviceMock.Setup(s => s.LogoutAsync("invalid-token")).ReturnsAsync(false);

            var result = await _controller.Logout("invalid-token") as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // ConfirmEmail
        // ============================================================

        [Fact]
        public async Task ConfirmEmail_ShouldReturn200_WhenSuccess()
        {
            var dto = new ConfirmEmailDto { UserId = "user-1", Token = "valid-token" };
            _serviceMock.Setup(s => s.ConfirmEmailAsync(dto)).ReturnsAsync(true);

            var result = await _controller.ConfirmEmail(dto) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task ConfirmEmail_ShouldReturn400_WhenFails()
        {
            var dto = new ConfirmEmailDto { UserId = "user-1", Token = "invalid-token" };
            _serviceMock.Setup(s => s.ConfirmEmailAsync(dto)).ReturnsAsync(false);

            var result = await _controller.ConfirmEmail(dto) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // ForgotPassword
        // ============================================================

        [Fact]
        public async Task ForgotPassword_ShouldReturn200_Always()
        {
            var dto = new ForgotPasswordDto { Email = "any@example.com" };
            _serviceMock.Setup(s => s.SendForgotPasswordEmailAsync(dto)).Returns(Task.CompletedTask);

            var result = await _controller.ForgotPassword(dto) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        // ============================================================
        // ResetPassword
        // ============================================================
        [Fact]
        public async Task ResetPassword_ShouldReturn200_WhenSuccess()
        {
            var dto = new ResetPasswordDto { Email = "test@example.com", Token = "valid-token", NewPassword = "NewPass123!", ConfirmPassword = "NewPass123!" };
            _serviceMock.Setup(s => s.ResetPasswordAsync(dto)).ReturnsAsync(true);

            var result = await _controller.ResetPassword(dto) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        

        [Fact]
        public async Task ResetPassword_ShouldReturn400_WhenThrows()
        {
            var dto = new ResetPasswordDto { Email = "test@example.com", Token = "bad", NewPassword = "weak", ConfirmPassword = "weak" };
            _serviceMock.Setup(s => s.ResetPasswordAsync(dto)).ThrowsAsync(new Exception("Reset failed"));

            var result = await _controller.ResetPassword(dto) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }

        // ============================================================
        // GetCurrentUser
        // ============================================================

        [Fact]
        public async Task GetCurrentUser_ShouldReturn200_WhenUserExists()
        {
            SetupUser("user-1");
            _serviceMock.Setup(s => s.GetCurrentUserAsync("user-1"))
                .ReturnsAsync(new UserProfileDto { UserId = "user-1", Email = "test@example.com" });

            var result = await _controller.GetCurrentUser() as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result!.StatusCode);
        }

        [Fact]
        public async Task GetCurrentUser_ShouldReturn400_WhenUserNotFound()
        {
            SetupUser("fake-id");
            _serviceMock.Setup(s => s.GetCurrentUserAsync("fake-id"))
                .ThrowsAsync(new Exception("User not found"));

            var result = await _controller.GetCurrentUser() as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result!.StatusCode);
        }
    }
}