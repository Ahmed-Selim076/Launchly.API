namespace Launchly.API.Application.Auth.DTOs;

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string StoreName,
    string Subdomain,
    int StoreType,
    int TemplateId
);

public record LoginRequest(
    string Email,
    string Password
);

public record GoogleAuthRequest(
    string IdToken
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record ForgotPasswordRequest(
    string Email
);

public record ResetPasswordRequest(
    string Token,
    string NewPassword
);

public record VerifyEmailRequest(
    string Token
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    UserDto User
);

public record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    Guid? TenantId,
    string? AvatarUrl,
    string? TenantSubdomain
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record UpdateAvatarRequest(
    string? AvatarUrl
);