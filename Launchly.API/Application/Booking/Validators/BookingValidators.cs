using FluentValidation;
using Launchly.API.Application.Booking.DTOs;

namespace Launchly.API.Application.Booking.Validators;

public class CreateServiceRequestValidator : AbstractValidator<CreateServiceRequest>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Service name is required.")
            .MaximumLength(150).WithMessage("Service name must be 150 characters or fewer.");

        RuleFor(x => x.DurationMins)
            .GreaterThan(0).WithMessage("Duration must be greater than 0 minutes.")
            .LessThanOrEqualTo(480).WithMessage("Duration cannot exceed 8 hours.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price cannot be negative.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be 1000 characters or fewer.")
            .When(x => x.Description is not null);
    }
}

public class UpdateServiceRequestValidator : AbstractValidator<UpdateServiceRequest>
{
    public UpdateServiceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Service name is required.")
            .MaximumLength(150).WithMessage("Service name must be 150 characters or fewer.");

        RuleFor(x => x.DurationMins)
            .GreaterThan(0).WithMessage("Duration must be greater than 0 minutes.")
            .LessThanOrEqualTo(480).WithMessage("Duration cannot exceed 8 hours.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price cannot be negative.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must be 1000 characters or fewer.")
            .When(x => x.Description is not null);
    }
}

public class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.ServiceId)
            .NotEmpty().WithMessage("Service is required.");

        RuleFor(x => x.StartTime)
            .GreaterThan(DateTime.UtcNow).WithMessage("Appointment must be in the future.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes must be 500 characters or fewer.")
            .When(x => x.Notes is not null);
    }
}

public class UpdateAppointmentStatusRequestValidator : AbstractValidator<UpdateAppointmentStatusRequest>
{
    private static readonly string[] AllowedStatuses = ["Pending", "Confirmed", "Cancelled", "Completed"];

    public UpdateAppointmentStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => AllowedStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
    }
}
