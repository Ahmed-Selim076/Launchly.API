using FluentValidation;
using Launchly.API.Application.Settings.DTOs;

namespace Launchly.API.Application.Settings.Validators;

public class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    private static readonly System.Text.RegularExpressions.Regex HexColorRegex =
        new(@"^#([A-Fa-f0-9]{6})$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public UpdateSettingsRequestValidator()
    {
        RuleFor(x => x.StoreName)
            .NotEmpty().WithMessage("Store name is required.")
            .MaximumLength(100).WithMessage("Store name must be 100 characters or fewer.");

        RuleFor(x => x.PrimaryColor)
            .NotEmpty().WithMessage("Primary color is required.")
            .Matches(HexColorRegex).WithMessage("Primary color must be a valid hex color (e.g. #C1522A).");

        RuleFor(x => x.SecondaryColor)
            .NotEmpty().WithMessage("Secondary color is required.")
            .Matches(HexColorRegex).WithMessage("Secondary color must be a valid hex color (e.g. #F2EDE6).");

        RuleFor(x => x.HeroText)
            .MaximumLength(200).WithMessage("Hero text must be 200 characters or fewer.")
            .When(x => x.HeroText is not null);

        RuleFor(x => x.AboutText)
            .MaximumLength(2000).WithMessage("About text must be 2000 characters or fewer.")
            .When(x => x.AboutText is not null);

        RuleFor(x => x.GoogleAnalyticsId)
            .MaximumLength(20).WithMessage("Google Analytics ID must be 20 characters or fewer.")
            .When(x => x.GoogleAnalyticsId is not null);

        RuleFor(x => x.ContactPhone)
            .MaximumLength(30).When(x => x.ContactPhone is not null);
        RuleFor(x => x.WhatsappNumber)
            .MaximumLength(30).When(x => x.WhatsappNumber is not null);
        RuleFor(x => x.ContactEmail)
            .EmailAddress().WithMessage("Contact email must be a valid email address.")
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.ContactAddress)
            .MaximumLength(300).When(x => x.ContactAddress is not null);
        RuleFor(x => x.FacebookUrl)
            .MaximumLength(300).When(x => x.FacebookUrl is not null);
        RuleFor(x => x.InstagramUrl)
            .MaximumLength(300).When(x => x.InstagramUrl is not null);
    }
}
