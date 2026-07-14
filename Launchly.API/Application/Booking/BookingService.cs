using Microsoft.EntityFrameworkCore;
using Launchly.API.Application.Booking.DTOs;
using Launchly.API.Common;
using Launchly.API.Core.Entities;
using Launchly.API.Core.Enums;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Services;

namespace Launchly.API.Application.Booking;

public class BookingService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly AuditLogService _auditLog;

    // Workday window (UTC). Move to TenantSettings if per-tenant hours are needed later.
    private static readonly TimeSpan WorkdayStart = TimeSpan.FromHours(9);
    private static readonly TimeSpan WorkdayEnd   = TimeSpan.FromHours(18);

    public BookingService(
        AppDbContext db,
        ITenantContext tenantContext,
        AuditLogService auditLog)
    {
        _db           = db;
        _tenantContext = tenantContext;
        _auditLog      = auditLog;
    }

    // ─── Admin: Service CRUD ──────────────────────────────────────────────────

    public async Task<Result<ServiceDto>> CreateServiceAsync(CreateServiceRequest request)
    {
        if (_tenantContext.TenantId is null)
            return Result<ServiceDto>.Failure("Store context is required.");

        var service = new Service
        {
            TenantId    = _tenantContext.TenantId.Value,
            Name        = request.Name.Trim(),
            Description = request.Description?.Trim(),
            DurationMins = request.DurationMins,
            Price       = request.Price,
            ImageUrl    = request.ImageUrl,
            IsActive    = true
        };

        _db.Services.Add(service);
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Created, "Service", service.Id,
            $"Created service \"{service.Name}\".");

        return Result<ServiceDto>.Created(MapServiceToDto(service));
    }

    public async Task<Result<IReadOnlyList<ServiceDto>>> GetServicesAsync()
    {
        var services = await _db.Services
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => MapServiceToDto(s))
            .ToListAsync();

        return Result<IReadOnlyList<ServiceDto>>.Success(services);
    }

    public async Task<Result<ServiceDto>> GetServiceByIdAsync(Guid id)
    {
        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service is null)
            return Result<ServiceDto>.NotFound("Service not found.");

        return Result<ServiceDto>.Success(MapServiceToDto(service));
    }

    public async Task<Result<ServiceDto>> UpdateServiceAsync(Guid id, UpdateServiceRequest request)
    {
        var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == id);

        if (service is null)
            return Result<ServiceDto>.NotFound("Service not found.");

        service.Name        = request.Name.Trim();
        service.Description = request.Description?.Trim();
        service.DurationMins = request.DurationMins;
        service.Price       = request.Price;
        service.IsActive    = request.IsActive;
        service.ImageUrl    = request.ImageUrl;
        service.UpdatedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Updated, "Service", service.Id,
            $"Updated service \"{service.Name}\".");

        return Result<ServiceDto>.Success(MapServiceToDto(service));
    }

    public async Task<Result<bool>> DeleteServiceAsync(Guid id)
    {
        var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == id);

        if (service is null)
            return Result<bool>.NotFound("Service not found.");

        // Guard: prevent deleting a service that has upcoming confirmed appointments
        var hasUpcoming = await _db.Appointments
            .AnyAsync(a =>
                a.ServiceId == id &&
                a.StartTime > DateTime.UtcNow &&
                a.Status == AppointmentStatus.Confirmed);

        if (hasUpcoming)
            return Result<bool>.Failure(
                "Cannot delete a service with upcoming confirmed appointments. " +
                "Cancel or complete all appointments first.");

        service.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.Deleted, "Service", service.Id,
            $"Soft-deleted service \"{service.Name}\".");

        return Result<bool>.Success(true);
    }

    // ─── Public: Available Slots ──────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<AvailableSlotDto>>> GetAvailableSlotsAsync(
        Guid serviceId,
        DateTime date)
    {
        // date is expected as a calendar date; strip time component
        var targetDate = date.Date;

        // Reject past dates immediately — no point querying
        if (targetDate < DateTime.UtcNow.Date)
            return Result<IReadOnlyList<AvailableSlotDto>>.Failure(
                "Cannot query availability for a past date.");

        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive);

        if (service is null)
            return Result<IReadOnlyList<AvailableSlotDto>>.NotFound("Service not found.");

        var dayStart = targetDate.Add(WorkdayStart);
        var dayEnd   = targetDate.Add(WorkdayEnd);

        // Load only booked slots for the requested day — single DB round-trip
        var bookedSlots = await _db.Appointments
            .AsNoTracking()
            .Where(a =>
                a.ServiceId == serviceId &&
                a.StartTime >= dayStart &&
                a.StartTime <  dayEnd &&
                a.Status    != AppointmentStatus.Cancelled)
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        var duration = TimeSpan.FromMinutes(service.DurationMins);
        var now      = DateTime.UtcNow;
        var slots    = new List<AvailableSlotDto>();
        var cursor   = dayStart;

        while (cursor.Add(duration) <= dayEnd)
        {
            var slotEnd = cursor.Add(duration);

            // Skip slots that have already started
            if (cursor > now)
            {
                var isBooked = bookedSlots.Any(b =>
                    cursor < b.EndTime && slotEnd > b.StartTime);

                if (!isBooked)
                    slots.Add(new AvailableSlotDto(cursor, slotEnd));
            }

            cursor = cursor.Add(duration);
        }

        return Result<IReadOnlyList<AvailableSlotDto>>.Success(slots);
    }

    // ─── Customer: Book Appointment ───────────────────────────────────────────

    public async Task<Result<AppointmentDto>> BookAppointmentAsync(
        BookAppointmentRequest request,
        Guid customerId)
    {
        if (_tenantContext.TenantId is null)
            return Result<AppointmentDto>.Failure("Store context is required.");

        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.IsActive);

        if (service is null)
            return Result<AppointmentDto>.NotFound("Service not found or no longer available.");

        // Validate StartTime falls within working hours
        var timeOfDay = request.StartTime.TimeOfDay;
        if (timeOfDay < WorkdayStart || request.StartTime.Add(TimeSpan.FromMinutes(service.DurationMins)).TimeOfDay > WorkdayEnd)
            return Result<AppointmentDto>.Failure(
                $"Appointments must be within working hours ({WorkdayStart:hh\\:mm}–{WorkdayEnd:hh\\:mm} UTC).");

        var endTime = request.StartTime.Add(TimeSpan.FromMinutes(service.DurationMins));

        // Conflict check — a second check beyond the DB unique index, catches race conditions cleanly
        var hasConflict = await _db.Appointments
            .AnyAsync(a =>
                a.ServiceId == request.ServiceId &&
                a.Status    != AppointmentStatus.Cancelled &&
                a.StartTime <  endTime &&
                a.EndTime   >  request.StartTime);

        if (hasConflict)
            return Result<AppointmentDto>.Failure(
                "This time slot is no longer available. Please choose another.");

        var customer = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u =>
                u.Id == customerId &&
                u.TenantId == _tenantContext.TenantId);

        if (customer is null)
            return Result<AppointmentDto>.NotFound("Customer not found.");

        var appointment = new Appointment
        {
            TenantId   = _tenantContext.TenantId.Value,
            ServiceId  = request.ServiceId,
            CustomerId = customerId,
            StartTime  = request.StartTime,
            EndTime    = endTime,
            Status     = AppointmentStatus.Pending,
            Notes      = request.Notes?.Trim()
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        return Result<AppointmentDto>.Created(MapAppointmentToDto(appointment, service, customer));
    }

    // ─── Customer: Own Appointments ───────────────────────────────────────────

    public async Task<Result<IReadOnlyList<CustomerAppointmentDto>>> GetCustomerAppointmentsAsync(
        Guid customerId)
    {
        var appointments = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Service)
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.StartTime)
            .Select(a => new CustomerAppointmentDto(
                a.Id,
                a.Service.Name,
                a.Service.DurationMins,
                a.Service.Price,
                a.StartTime,
                a.EndTime,
                a.Status.ToString(),
                a.Notes,
                a.CreatedAt
            ))
            .ToListAsync();

        return Result<IReadOnlyList<CustomerAppointmentDto>>.Success(appointments);
    }

    public async Task<Result<bool>> CancelAppointmentAsync(Guid id, Guid customerId)
    {
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId);

        if (appointment is null)
            return Result<bool>.NotFound("Appointment not found.");

        if (appointment.Status == AppointmentStatus.Cancelled)
            return Result<bool>.Failure("Appointment is already cancelled.");

        if (appointment.Status == AppointmentStatus.Completed)
            return Result<bool>.Failure("Cannot cancel a completed appointment.");

        if (appointment.StartTime <= DateTime.UtcNow)
            return Result<bool>.Failure("Cannot cancel an appointment that has already started.");

        appointment.Status = AppointmentStatus.Cancelled;
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.StatusChanged, "Appointment", appointment.Id,
            "Customer cancelled appointment.");

        return Result<bool>.Success(true);
    }

    // ─── Admin: Appointment Management ───────────────────────────────────────

    public async Task<Result<AppointmentListDto>> GetAppointmentsAsync(
        int page     = 1,
        int pageSize = 20)
    {
        var query = _db.Appointments
            .AsNoTracking()
            .Include(a => a.Service)
            .Include(a => a.Customer)
            .OrderByDescending(a => a.StartTime);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AppointmentDto(
                a.Id,
                a.ServiceId,
                a.Service.Name,
                a.Customer.FullName,
                a.Customer.Email,
                a.StartTime,
                a.EndTime,
                a.Status.ToString(),
                a.Notes,
                a.CreatedAt
            ))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<AppointmentListDto>.Success(new AppointmentListDto(
            items, totalCount, page, pageSize, totalPages));
    }

    public async Task<Result<AppointmentDto>> UpdateAppointmentStatusAsync(
        Guid id,
        UpdateAppointmentStatusRequest request)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Service)
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null)
            return Result<AppointmentDto>.NotFound("Appointment not found.");

        if (!Enum.TryParse<AppointmentStatus>(request.Status, ignoreCase: true, out var newStatus))
            return Result<AppointmentDto>.Failure("Invalid status value.");

        // Guard: Completed appointments are immutable
        if (appointment.Status == AppointmentStatus.Completed)
            return Result<AppointmentDto>.Failure("Cannot change status of a completed appointment.");

        var previousStatus = appointment.Status;
        appointment.Status = newStatus;
        await _db.SaveChangesAsync();

        _auditLog.Log(AuditAction.StatusChanged, "Appointment", appointment.Id,
            $"Status changed from {previousStatus} to {newStatus}.");

        return Result<AppointmentDto>.Success(
            MapAppointmentToDto(appointment, appointment.Service, appointment.Customer));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ServiceDto MapServiceToDto(Service s) => new(
        s.Id,
        s.Name,
        s.Description,
        s.DurationMins,
        s.Price,
        s.IsActive,
        s.CreatedAt,
        s.ImageUrl
    );

    private static AppointmentDto MapAppointmentToDto(
        Appointment a,
        Service service,
        Core.Entities.User customer) => new(
        a.Id,
        a.ServiceId,
        service.Name,
        customer.FullName,
        customer.Email,
        a.StartTime,
        a.EndTime,
        a.Status.ToString(),
        a.Notes,
        a.CreatedAt
    );
}
