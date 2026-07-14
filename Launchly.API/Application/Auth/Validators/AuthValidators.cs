using FluentValidation;
using Launchly.API.Application.Auth.DTOs;
using Launchly.API.Core.Enums;

namespace Launchly.API.Application.Auth.Validators;

// ─── Shared Rules ──────────────────────────────────────────────────────────────
// Kept as static helpers (not a base validator) since FluentValidation rule
// chains can't easily be inherited across different DTO types.

internal static class AuthValidationRules
{
    // Reserved subdomains that must never be claimed by a tenant — they'd
    // collide with platform-level routes or look like official Launchly pages.
    public static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "app", "mail", "ftp", "smtp", "ns1", "ns2",
        "support", "help", "status", "blog", "docs", "static", "cdn",
        "dashboard", "login", "signup", "register", "billing", "assets"
    };

    public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(100).WithMessage("Password must be 100 characters or fewer.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

    public static IRuleBuilderOptions<T, string> ApplyEmailRules<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256).WithMessage("Email must be 256 characters or fewer.")
            .EmailAddress().WithMessage("A valid email address is required.");

    public static IRuleBuilderOptions<T, string> ApplySubdomainRules<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("Subdomain is required.")
            .MinimumLength(3).WithMessage("Subdomain must be at least 3 characters long.")
            .MaximumLength(63).WithMessage("Subdomain must be 63 characters or fewer.")
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
                .WithMessage("Subdomain may only contain lowercase letters, digits, and hyphens, " +
                             "and cannot start or end with a hyphen.")
            .Must(s => !ReservedSubdomains.Contains(s))
                .WithMessage("This subdomain is reserved. Please choose another.");
}

// ─── Register ───────────────────────────────────────────────────────────────────

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(50).WithMessage("First name must be 50 characters or fewer.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(50).WithMessage("Last name must be 50 characters or fewer.");

        RuleFor(x => x.Email).ApplyEmailRules();

        RuleFor(x => x.Password).ApplyPasswordRules();

        RuleFor(x => x.StoreName)
            .NotEmpty().WithMessage("Store name is required.")
            .MaximumLength(100).WithMessage("Store name must be 100 characters or fewer.");

        // Note: normalization (trim/lowercase) happens in AuthService before
        // persistence, not here — a validator's job is to check the value,
        // not rewrite it. ApplySubdomainRules already requires lowercase
        // letters/digits/hyphens only, so a value that passes this rule is
        // already in the shape the service will persist.
        RuleFor(x => x.Subdomain)
            .ApplySubdomainRules();

        // StoreType is an int (not the StoreType enum) on this DTO, so
        // .IsInEnum() does NOT work here — FluentValidation's EnumValidator
        // checks typeof(TProperty).IsEnum first and returns false
        // immediately if TProperty isn't actually an enum type, regardless
        // of the value. On an `int` property that means every request
        // would fail this rule, valid values included. Enum.IsDefined
        // against the underlying type is the correct check for an int
        // standing in for an enum at the API boundary.
        RuleFor(x => x.StoreType)
            .Must(s => Enum.IsDefined(typeof(StoreType), s))
            .WithMessage("Store type must be a valid store type (Ecommerce, Booking, or Restaurant).");

        // Range only — which TemplateId values are valid *for the chosen
        // StoreType* is a cross-field rule checked in AuthService
        // (TemplateService.IsValidTemplateId), since every StoreType
        // currently happens to allow 1-3 but that's not guaranteed to stay
        // true, and FluentValidation rules here only see one field at a time.
        RuleFor(x => x.TemplateId)
            .InclusiveBetween(1, 3)
            .WithMessage("Template must be between 1 and 3.");
    }
}

// ─── Login ────────────────────────────────────────────────────────────────────

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256).WithMessage("Email must be 256 characters or fewer.");
        // Intentionally no .EmailAddress()/format check here: login is an
        // authentication attempt, not data entry. Rejecting malformed input
        // before it reaches AuthService would leak "this isn't even a valid
        // email shape" as a distinct response path from "wrong password" —
        // a minor but needless oracle. AuthService's lookup simply won't
        // match and returns the same generic "Invalid email or password."

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(100).WithMessage("Password must be 100 characters or fewer.");
    }
}

// ─── Google OAuth ─────────────────────────────────────────────────────────────

public class GoogleAuthRequestValidator : AbstractValidator<GoogleAuthRequest>
{
    public GoogleAuthRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("Google ID token is required.");
    }
}

// ─── Refresh / Logout ─────────────────────────────────────────────────────────

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}

// ─── Forgot Password ───────────────────────────────────────────────────────────

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).ApplyEmailRules();
    }
}

// ─── Reset Password ────────────────────────────────────────────────────────────

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword).ApplyPasswordRules();
    }
}

// ─── Verify Email ──────────────────────────────────────────────────────────────

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Verification token is required.");
    }
}
