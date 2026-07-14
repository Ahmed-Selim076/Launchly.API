namespace Launchly.API.Core.Enums;

public enum StoreType
{
    Ecommerce  = 0,
    Booking    = 1,
    Restaurant = 2
}

public enum UserRole
{
    SuperAdmin  = 0,
    TenantAdmin = 1,
    Customer    = 2
}

public enum PlanType
{
    Free = 0,
    Paid = 1
}

public enum OrderStatus
{
    Pending   = 0,
    Confirmed = 1,
    Shipped   = 2,
    Delivered = 3,
    Cancelled = 4
}

public enum FoodOrderStatus
{
    Received  = 0,
    Preparing = 1,
    Ready     = 2,
    Delivered = 3,
    Cancelled = 4
}

public enum OrderType
{
    Delivery = 0,
    Pickup   = 1
}

public enum AppointmentStatus
{
    Pending   = 0,
    Confirmed = 1,
    Completed = 2,
    Cancelled = 3
}

public enum AuditAction
{
    Created       = 0,
    Updated       = 1,
    Deleted       = 2,
    StatusChanged = 3,
    LoggedIn      = 4,
    LoggedOut     = 5,
    PasswordReset = 6,
    Suspended     = 7,
    Activated     = 8
}