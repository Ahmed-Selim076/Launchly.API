using FluentValidation;
using Launchly.API.Application.Products.DTOs;

namespace Launchly.API.Application.Products.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must be 150 characters or fewer.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0.");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative.");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description must be 5000 characters or fewer.")
            .When(x => x.Description is not null);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(2000).WithMessage("Image URL is too long.")
            .When(x => x.ImageUrl is not null);
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must be 150 characters or fewer.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0.");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative.");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description must be 5000 characters or fewer.")
            .When(x => x.Description is not null);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(2000).WithMessage("Image URL is too long.")
            .When(x => x.ImageUrl is not null);
    }
}
