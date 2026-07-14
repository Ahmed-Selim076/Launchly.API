using FluentValidation;
using Launchly.API.Application.Restaurant.DTOs;

namespace Launchly.API.Application.Restaurant.Validators;

public class CreateMenuCategoryRequestValidator : AbstractValidator<CreateMenuCategoryRequest>
{
    public CreateMenuCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must be 80 characters or fewer.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order cannot be negative.");
    }
}

public class UpdateMenuCategoryRequestValidator : AbstractValidator<UpdateMenuCategoryRequest>
{
    public UpdateMenuCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must be 80 characters or fewer.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order cannot be negative.");
    }
}

public class CreateMenuItemRequestValidator : AbstractValidator<CreateMenuItemRequest>
{
    public CreateMenuItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(120).WithMessage("Name must be 120 characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must be 500 characters or fewer.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.");
    }
}

public class UpdateMenuItemRequestValidator : AbstractValidator<UpdateMenuItemRequest>
{
    public UpdateMenuItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(120).WithMessage("Name must be 120 characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must be 500 characters or fewer.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.");
    }
}

public class CreateFoodOrderRequestValidator : AbstractValidator<CreateFoodOrderRequest>
{
    public CreateFoodOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Order must have at least one item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.MenuItemId)
                .NotEmpty().WithMessage("MenuItemId is required.");

            item.RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be at least 1.");
        });
    }
}

public class UpdateFoodOrderStatusRequestValidator : AbstractValidator<UpdateFoodOrderStatusRequest>
{
    public UpdateFoodOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .InclusiveBetween(0, 4).WithMessage("Status must be between 0 and 4.");
    }
}
