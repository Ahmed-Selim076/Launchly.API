using FluentValidation;
using Launchly.API.Application.Auth.Validators;
using Launchly.API.Application.Store.DTOs;

namespace Launchly.API.Application.Store.Validators;

// ─── Customer Registration ────────────────────────────────────────────────────

public class RegisterCustomerRequestValidator : AbstractValidator<RegisterCustomerRequest>
{
    public RegisterCustomerRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(50).WithMessage("First name must be 50 characters or fewer.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(50).WithMessage("Last name must be 50 characters or fewer.");

        // Reuses the same password/email strength rules enforced for
        // tenant-admin registration — a storefront customer account
        // deserves the same baseline protection.
        RuleFor(x => x.Email).ApplyEmailRules();
        RuleFor(x => x.Password).ApplyPasswordRules();
    }
}

// ─── Place Order ──────────────────────────────────────────────────────────────

public class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Order must have at least one item.");

        // Exactly one of ProductId/MenuItemId per line — this is what makes
        // a line "Ecommerce" vs "Restaurant"; having both or neither is
        // always a client bug, not a valid order.
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x)
                .Must(line => line.ProductId.HasValue ^ line.MenuItemId.HasValue)
                .WithMessage("Each line must have exactly one of ProductId or MenuItemId.");

            item.RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than 0.");
        });

        // A request must be entirely Ecommerce lines or entirely Restaurant
        // lines — mixing them in one order isn't a real scenario (a tenant
        // is one StoreType), and would make TotalAmount/stock-decrement
        // logic ambiguous about which table to touch.
        RuleFor(x => x.Items)
            .Must(items =>
                items.All(i => i.ProductId.HasValue) ||
                items.All(i => i.MenuItemId.HasValue))
            .WithMessage("All lines in one order must be the same type (all products or all menu items).")
            .When(x => x.Items.Count > 0);

        // OrderType (Delivery/Pickup) only makes sense for Restaurant orders
        // (MenuItemId lines) — Ecommerce orders leave it null and this rule
        // doesn't apply to them.
        RuleFor(x => x.OrderType)
            .Must(t => t is 0 or 1)
            .WithMessage("OrderType must be 0 (Delivery) or 1 (Pickup).")
            .When(x => x.Items.Any(i => i.MenuItemId.HasValue));

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes must be 1000 characters or fewer.")
            .When(x => x.Notes is not null);
    }
}
