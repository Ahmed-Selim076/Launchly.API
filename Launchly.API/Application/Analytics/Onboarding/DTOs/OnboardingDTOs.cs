namespace Launchly.API.Application.Analytics.Onboarding.DTOs;

public record OnboardingStepDto(
    string Key,
    string Label,
    bool IsComplete
);

public record OnboardingStatusDto(
    List<OnboardingStepDto> Steps,
    int CompletedCount,
    int TotalCount,
    bool IsFullyComplete
);