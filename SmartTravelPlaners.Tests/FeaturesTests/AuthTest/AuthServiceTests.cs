using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using SmartTravelPlaners.BLL.DTOs.Auth;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Services.Abstract;
using SmartTravelPlaners.BLL.Services.Concrete;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SmartTravelPlaners.Tests.Features.Auth
{
    public class AuthServiceTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IEmailService> _emailMock;
        private readonly Mock<ISubscriptionService> _subscriptionMock;
        private readonly ApplicationDbContext _context;
        private readonly AuthService _service;

        public AuthServiceTests()
        {
            // UserManager Mock
            var store = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);

            // Configuration Mock
            _configMock = new Mock<IConfiguration>();
            var jwtSection = new Mock<IConfigurationSection>();
            jwtSection.Setup(s => s["SecretKey"]).Returns("SuperSecretKey12345678901234567890AbCdEf");
            jwtSection.Setup(s => s["Issuer"]).Returns("TestIssuer");
            jwtSection.Setup(s => s["Audience"]).Returns("TestAudience");
            jwtSection.Setup(s => s["ExpiryInMinutes"]).Returns("60");
            _configMock.Setup(c => c.GetSection("JwtSettings")).Returns(jwtSection.Object);
            _configMock.Setup(c => c["JwtSettings:RefreshTokenExpiryInDays"]).Returns("7");

            // InMemory DbContext
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            _emailMock = new Mock<IEmailService>();
            _subscriptionMock = new Mock<ISubscriptionService>();

            _service = new AuthService(
                _userManagerMock.Object,
                _configMock.Object,
                _context,
                _emailMock.Object,
                _subscriptionMock.Object);
        }

        private ApplicationUser MakeUser(string id = "user-1") => new ApplicationUser
        {
            Id = id,
            FullName = "Test User",
            Email = "test@example.com",
            UserName = "test@example.com",
            EmailConfirmed = false
        };

        // ============================================================
        // RegisterAsync
        // ============================================================

        [Fact]
        public async Task RegisterAsync_ShouldThrow_WhenEmailAlreadyExists()
        {
            var dto = new RegisterDto { Email = "test@example.com", Password = "Pass123!", FullName = "Test" };
            _userManagerMock.Setup(u => u.FindByEmailAsync(dto.Email)).ReturnsAsync(MakeUser());

            await Assert.ThrowsAsync<Exception>(() => _service.RegisterAsync(dto));
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrow_WhenCreateFails()
        {
            var dto = new RegisterDto { Email = "new@example.com", Password = "weak", FullName = "Test" };
            _userManagerMock.Setup(u => u.FindByEmailAsync(dto.Email)).ReturnsAsync((ApplicationUser?)null);
            _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), dto.Password))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

            await Assert.ThrowsAsync<Exception>(() => _service.RegisterAsync(dto));
        }

        [Fact]
        public async Task RegisterAsync_ShouldCallEnsureDefaultSubscription_WhenSuccess()
        {
          
            var dto = new RegisterDto
            {
                Email = "new@example.com",
                Password = "Pass123!",
                FullName = "Test"
            };

            ApplicationUser createdUser = null;

            _userManagerMock
                .Setup(u => u.FindByEmailAsync(dto.Email))
                .ReturnsAsync((ApplicationUser?)null);

            _userManagerMock
                .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Callback<ApplicationUser, string>((user, password) =>
                {
                    user.Id = "test-user-id";   
                    user.Email = dto.Email;
                    user.FullName = dto.FullName;

                    createdUser = user;
                })
                .ReturnsAsync(IdentityResult.Success);

            _userManagerMock
                .Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync("fake-token");

            _userManagerMock
                .Setup(u => u.GetRolesAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(new List<string>());

            _userManagerMock
                .Setup(u => u.FindByIdAsync("test-user-id"))
                .ReturnsAsync(new ApplicationUser
                {
                    Id = "test-user-id",
                    Email = dto.Email,
                    FullName = dto.FullName
                });

            _emailMock
                .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _subscriptionMock
                .Setup(s => s.EnsureDefaultSubscriptionAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

          
            var result = await _service.RegisterAsync(dto);


            Assert.NotNull(result);
          
            Assert.Equal("test-user-id", result);

            Assert.NotNull(createdUser);
            Assert.Equal("test-user-id", createdUser.Id);

            _subscriptionMock.Verify(
                s => s.EnsureDefaultSubscriptionAsync("test-user-id"),
                Times.Once);

            _emailMock.Verify(
                e => e.SendEmailAsync(
                    dto.Email,
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        // ============================================================
        // LoginAsync
        // ============================================================

        [Fact]
        public async Task LoginAsync_ShouldThrow_WhenUserNotFound()
        {
            var dto = new LoginDto { Email = "notfound@example.com", Password = "Pass123!" };
            _userManagerMock.Setup(u => u.FindByEmailAsync(dto.Email)).ReturnsAsync((ApplicationUser?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.LoginAsync(dto));
        }

        [Fact]
        public async Task LoginAsync_ShouldThrow_WhenPasswordInvalid()
        {
            var dto = new LoginDto { Email = "test@example.com", Password = "WrongPass" };
            _userManagerMock.Setup(u => u.FindByEmailAsync(dto.Email)).ReturnsAsync(MakeUser());
            _userManagerMock.Setup(u => u.CheckPasswordAsync(It.IsAny<ApplicationUser>(), dto.Password))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<Exception>(() => _service.LoginAsync(dto));
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnToken_WhenCredentialsValid()
        {
           
            var dto = new LoginDto { Email = "test@example.com", Password = "Pass123!" };
            var user = MakeUser();

            _userManagerMock
                .Setup(u => u.FindByEmailAsync(dto.Email))
                .ReturnsAsync(user);

            _userManagerMock
                .Setup(u => u.CheckPasswordAsync(user, dto.Password))
                .ReturnsAsync(true);

            
            _userManagerMock
                .Setup(u => u.GetRolesAsync(user))
                .ReturnsAsync(new List<string>());

           
            var result = await _service.LoginAsync(dto);

           
            Assert.NotNull(result);
            Assert.NotEmpty(result.AccessToken);
            Assert.Equal(dto.Email, result.Email);
        }

        // ============================================================
        // LogoutAsync
        // ============================================================

        [Fact]
        public async Task LogoutAsync_ShouldReturnFalse_WhenTokenNotFound()
        {
            var result = await _service.LogoutAsync("invalid-token");

            Assert.False(result);
        }

        // ============================================================
        // ConfirmEmailAsync
        // ============================================================

        [Fact]
        public async Task ConfirmEmailAsync_ShouldThrow_WhenUserNotFound()
        {
            var dto = new ConfirmEmailDto { UserId = "fake-id", Token = "fake-token" };
            _userManagerMock.Setup(u => u.FindByIdAsync(dto.UserId)).ReturnsAsync((ApplicationUser?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.ConfirmEmailAsync(dto));
        }

        // ============================================================
        // ResetPasswordAsync
        // ============================================================

        [Fact]
        public async Task ResetPasswordAsync_ShouldThrow_WhenUserNotFound()
        {
            var dto = new ResetPasswordDto { Email = "fake@example.com", Token = "fake-token", NewPassword = "NewPass123!", ConfirmPassword = "NewPass123!" };
            _userManagerMock.Setup(u => u.FindByEmailAsync(dto.Email)).ReturnsAsync((ApplicationUser?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.ResetPasswordAsync(dto));
        }

        // ============================================================
        // GetCurrentUserAsync
        // ============================================================

        [Fact]
        public async Task GetCurrentUserAsync_ShouldThrow_WhenUserNotFound()
        {
            _userManagerMock.Setup(u => u.FindByIdAsync("fake-id")).ReturnsAsync((ApplicationUser?)null);

            await Assert.ThrowsAsync<Exception>(() => _service.GetCurrentUserAsync("fake-id"));
        }

        [Fact]
        public async Task GetCurrentUserAsync_ShouldReturnUser_WhenExists()
        {
            var user = MakeUser();
            _userManagerMock.Setup(u => u.FindByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await _service.GetCurrentUserAsync(user.Id);

            Assert.NotNull(result);
            Assert.Equal(user.Email, result.Email);
        }

        // ============================================================
        // RefreshTokenAsync
        // ============================================================

        [Fact]
        public async Task RefreshTokenAsync_ShouldThrow_WhenTokenInvalid()
        {
            await Assert.ThrowsAsync<Exception>(() => _service.RefreshTokenAsync("invalid-token"));
        }

        // ============================================================
        // SendForgotPasswordEmailAsync
        // ============================================================

        [Fact]
        public async Task SendForgotPasswordEmailAsync_ShouldNotThrow_WhenEmailNotFound()
        {
            var dto = new ForgotPasswordDto { Email = "notfound@example.com" };
            _userManagerMock.Setup(u => u.FindByEmailAsync(dto.Email)).ReturnsAsync((ApplicationUser?)null);

            // مش بيرمي exception لو الإيميل مش موجود (security by design)
            var ex = await Record.ExceptionAsync(() => _service.SendForgotPasswordEmailAsync(dto));
            Assert.Null(ex);
        }

        // ============================================================
        // OAuthLoginAsync
        // ============================================================

        [Fact]
        public async Task OAuthLoginAsync_ShouldCreateNewUser_WhenNotExists()
        {
         
            var email = "oauth@example.com";
            var fullName = "OAuth User";

            _userManagerMock
                .Setup(u => u.FindByEmailAsync(email))
                .ReturnsAsync((ApplicationUser?)null);

            _userManagerMock
                .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            
            _userManagerMock
                .Setup(u => u.GetRolesAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(new List<string>());

            _userManagerMock
                .Setup(u => u.GetLoginsAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(new List<UserLoginInfo>());

            _userManagerMock
                .Setup(u => u.AddLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<UserLoginInfo>()))
                .ReturnsAsync(IdentityResult.Success);

            var result = await _service.OAuthLoginAsync(email, fullName, "Google", "google-123");


            
            Assert.NotNull(result);
            Assert.Equal(email, result.Email);

            _userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>()), Times.Once);
            _userManagerMock.Verify(u => u.FindByEmailAsync(email), Times.Once);
        }
    }
}