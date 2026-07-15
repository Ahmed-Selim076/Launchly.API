using System.Text;
using AspNetCoreRateLimit;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SendGrid.Extensions.DependencyInjection;
using Serilog;
using Launchly.API.Application.Analytics;
using Launchly.API.Application.Auth;
using Launchly.API.Application.Categories;
using Launchly.API.Application.Orders;
using Launchly.API.Application.Products;
using Launchly.API.Application.Settings;
using Launchly.API.Core.Interfaces;
using Launchly.API.Infrastructure.Data;
using Launchly.API.Infrastructure.Data.Seeds;
using Launchly.API.Infrastructure.Middleware;
using Launchly.API.Infrastructure.Services;
using Launchly.API.Application.Analytics.Onboarding;
using Launchly.API.Application.Customers;
using Launchly.API.Application.AuditLog;
using Launchly.API.Application.Store;
using Launchly.API.Application.Booking;
using Launchly.API.Infrastructure.BackgroundServices;
using Launchly.API.Application.SuperAdmin;
using Launchly.API.Application.Restaurant;
using Launchly.API.Application.Tenants;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog ──────────────────────────────────────────────────────────────

    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/launchly-.log", rollingInterval: RollingInterval.Day);
    });

    // ─── Database ─────────────────────────────────────────────────────────────

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseNpgsql(builder.Configuration["DATABASE_URL"]);

        if (builder.Environment.IsDevelopment())
            options.EnableSensitiveDataLogging();
    });

    // ─── Memory Cache ─────────────────────────────────────────────────────────

    builder.Services.AddMemoryCache();

    // ─── Response Caching ─────────────────────────────────────────────────────
    // Used on public store endpoints (settings, product listing, product detail)
    // so repeated anonymous requests are served from memory without hitting the DB.

    builder.Services.AddResponseCaching();

    // ─── Scoped Context Services ──────────────────────────────────────────────

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<TenantContext>();
    builder.Services.AddScoped<ITenantContext>(sp =>
        sp.GetRequiredService<TenantContext>());
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    // ─── Infrastructure Services ──────────────────────────────────────────────

    builder.Services.AddScoped<TokenService>();
    builder.Services.AddScoped<AuditLogService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();

    // ─── SendGrid ─────────────────────────────────────────────────────────────

    builder.Services.AddSendGrid(options =>
    {
        var apiKey = builder.Configuration["SENDGRID_API_KEY"];

        // SendGridClient throws on construction if the key is null/empty.
        // Fall back to a placeholder so the app still starts in dev —
        // EmailService logs and swallows the resulting send failure.
        options.ApiKey = string.IsNullOrWhiteSpace(apiKey) ? "SG.not-configured" : apiKey;
    });

    // ─── Application Services ─────────────────────────────────────────────────

    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<ProductService>();
    builder.Services.AddScoped<CategoryService>();
    builder.Services.AddScoped<OrderService>();
    builder.Services.AddScoped<SettingsService>();
    builder.Services.AddScoped<DashboardService>();
    builder.Services.AddScoped<OnboardingService>();
    builder.Services.AddScoped<CustomerService>();
    builder.Services.AddScoped<AuditLogQueryService>();
    builder.Services.AddScoped<StoreService>();
    builder.Services.AddScoped<BookingService>();
    builder.Services.AddScoped<AnalyticsService>();
    builder.Services.AddHostedService<VisitorLogCleanupService>();
    builder.Services.AddScoped<SuperAdminService>();
    builder.Services.AddScoped<RestaurantService>();
    builder.Services.AddScoped<TemplateService>();

    
    // ─── JWT Authentication ───────────────────────────────────────────────────

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["JWT_ISSUER"],
                ValidAudience = builder.Configuration["JWT_AUDIENCE"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["JWT_SECRET"]!)),
                ClockSkew = TimeSpan.Zero
            };
        });

    // ─── Authorization Policies ───────────────────────────────────────────────

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("SuperAdmin", policy =>
            policy.RequireRole("SuperAdmin"));

        options.AddPolicy("TenantAdmin", policy =>
            policy.RequireRole("TenantAdmin")
                  .RequireClaim("tenantId"));

        options.AddPolicy("Customer", policy =>
            policy.RequireRole("Customer")
                  .RequireClaim("tenantId"));

        options.AddPolicy("TenantMember", policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole("TenantAdmin") ||
                ctx.User.IsInRole("Customer")));
    });

    // ─── Rate Limiting ────────────────────────────────────────────────────────

    builder.Services.Configure<IpRateLimitOptions>(options =>
    {
        options.EnableEndpointRateLimiting = true;
        options.StackBlockedRequests = false;
        options.GeneralRules =
        [
            new RateLimitRule
            {
                Endpoint = "POST:/api/v1/auth/login",
                Limit = 5,
                Period = "1m"
            },
            new RateLimitRule
            {
                Endpoint = "POST:/api/v1/auth/login-customer",
                Limit = 5,
                Period = "1m"
            },
            new RateLimitRule
            {
                Endpoint = "POST:/api/v1/auth/google",
                Limit = 10,
                Period = "1m"
            },
            new RateLimitRule
            {
                Endpoint = "POST:/api/v1/auth/register",
                Limit = 3,
                Period = "1m"
            },
            new RateLimitRule
            {
                Endpoint = "POST:/api/v1/auth/forgot-password",
                Limit = 3,
                Period = "5m"
            }
        ];
    });

    builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
    builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
    builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
    builder.Services.AddInMemoryRateLimiting();

    // ─── CORS ─────────────────────────────────────────────────────────────────

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("LaunchlyPolicy", policy =>
        {
            policy
                .SetIsOriginAllowed(origin =>
                {
                    var host = new Uri(origin).Host;
                    var platformDomain = builder.Configuration["PLATFORM_DOMAIN"]
                        ?? "launchly.app";

                    // Vercel gives every deployment (production + every preview/
                    // branch build) its own unique *.vercel.app hostname, e.g.
                    // launchly-frontend-pznrhmlfw-xxahmedwork-5030s-projects.vercel.app
                    // Those all belong to the same Vercel project as the production
                    // domain, so allow any host that starts with the same project
                    // prefix and ends in .vercel.app, in addition to the exact
                    // production domain configured in PLATFORM_DOMAIN.
                    var vercelProjectPrefix = builder.Configuration["VERCEL_PROJECT_PREFIX"]
                        ?? "launchly-frontend";

                    return host == platformDomain ||
                           host.EndsWith($".{platformDomain}") ||
                           (host.StartsWith(vercelProjectPrefix) && host.EndsWith(".vercel.app")) ||
                           host == "localhost" ||
                           host.EndsWith(".localhost");
                })
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    // ─── Controllers ──────────────────────────────────────────────────────────

    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            // Keep validation failures in the same ApiResponse envelope as
            // every other error in the API, instead of the framework default
            // ValidationProblemDetails shape.
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(kvp => kvp.Value!.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                return new Microsoft.AspNetCore.Mvc.UnprocessableEntityObjectResult(
                    Launchly.API.Common.ApiResponse<object>.Fail("Validation failed.", errors));
            };
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy =
                System.Text.Json.JsonNamingPolicy.CamelCase;
        });

    // ─── FluentValidation ─────────────────────────────────────────────────────
    // Must be registered after AddControllers() — see FluentValidation.AspNetCore docs.

    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddFluentValidationAutoValidation();

    // ─── Health Checks ────────────────────────────────────────────────────────

    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration["DATABASE_URL"]!);

    // ─── Swagger (dev only) ───────────────────────────────────────────────────

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            var jwtScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Enter: Bearer {your access token}",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            options.AddSecurityDefinition("Bearer", jwtScheme);
            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                { jwtScheme, Array.Empty<string>() }
            });
        });
    }

    var app = builder.Build();

    // ─── Migrate + Seed ───────────────────────────────────────────────────────

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        await db.Database.MigrateAsync();
        await SuperAdminSeed.RunAsync(db, config);
        await DemoDataSeed.RunAsync(db);
    }

    // ─── Middleware Pipeline ──────────────────────────────────────────────────

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseIpRateLimiting();
    app.UseSerilogRequestLogging();
    app.UseResponseCaching();
    app.UseCors("LaunchlyPolicy");
    app.UseAuthentication();
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Launchly API failed to start.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
