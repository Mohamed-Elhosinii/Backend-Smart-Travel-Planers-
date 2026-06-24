using FluentValidation;
using SmartTravelPlaners.BLL.DTOs.Auth;

namespace SmartTravelPlaners.BLL.Validation.Auth
{
    public class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
    {
        public ResetPasswordDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Must be a valid email format.");

            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("Token is required.");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("New Password is required.")
                .MinimumLength(6).WithMessage("New Password must be at least 6 characters.")
                .Matches(@"[A-Z]").WithMessage("New Password must contain at least one uppercase letter.")
                .Matches(@"[a-z]").WithMessage("New Password must contain at least one lowercase letter.")
                .Matches(@"[0-9]").WithMessage("New Password must contain at least one digit.")
                .Matches(@"[^a-zA-Z0-9]").WithMessage("New Password must contain at least one special character.");

            RuleFor(x => x.ConfirmPassword)
                .NotEmpty().WithMessage("Confirm Password is required.")
                .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
        }
    }
}
