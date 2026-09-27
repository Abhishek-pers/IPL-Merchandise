using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using IplStore.Api.Controllers;
using IplStore.Api.ErrorHandling;
using IplStore.Api.Health;
using IplStore.Api.Identity;
using IplStore.Application;
using IplStore.Application.Carts;
using IplStore.Application.Catalog;
using IplStore.Application.Common;
using IplStore.Application.Orders;
using IplStore.Application.Pricing;
using IplStore.Infrastructure;
using IplStore.Infrastructure.Options;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace IplStore.Api.Composition;

/// <summary>
/// Composition root: the ONE place where concrete implementations are chosen and every
/// configurable "knob" is bound to an options object. Read this file to understand how the
/// application is assembled.
/// </summary>
public static class CompositionRoot
{
    public static IServiceCollection AddIplStore(this IServiceCollection services, IConfiguration configuration)
    {
        // ---- 1. Options: every tunable parameter is an object, validated at start-up (fail fast).
        services.AddValidatedOptions<PagingOptions>(configuration, PagingOptions.SectionName)
            .Validate(o => o.DefaultPageSize <= o.MaxPageSize, "Paging:DefaultPageSize must be <= Paging:MaxPageSize.");
        services.AddValidatedOptions<CatalogOptions>(configuration, CatalogOptions.SectionName);
        services.AddValidatedOptions<CartOptions>(configuration, CartOptions.SectionName);
        services.AddValidatedOptions<CheckoutOptions>(configuration, CheckoutOptions.SectionName);
        services.AddValidatedOptions<PricingOptions>(configuration, PricingOptions.SectionName);
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        services.AddValidatedOptions<ResilienceOptions>(configuration, ResilienceOptions.SectionName);
        services.AddValidatedOptions<CorsSettings>(configuration, CorsSettings.SectionName);
        services.AddValidatedOptions<RateLimitingSettings>(configuration, RateLimitingSettings.SectionName);

        // ---- 2. Layers (inner -> outer).
        services.AddApplication();
        services.AddInfrastructure();
        services.AddApiLayer();

        return services;
    }

    public static WebApplication UseIplStorePipeline(this WebApplication app)
    {
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI(o => o.DocumentTitle = "IPL Store API");
            app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
        }

        var cors = app.Services.GetRequiredService<IOptions<CorsSettings>>().Value;
        app.UseCors(policy => policy
            .WithOrigins(cors.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Location", OrdersController.IdempotentReplayedHeader));

        app.UseRateLimiter();

        app.MapControllers();

        // Liveness: process is up (no dependencies). Readiness: can serve traffic (DB reachable).
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

        return app;
    }

    private static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class =>
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

    private static IServiceCollection AddApiLayer(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        // Swap for a JWT-claims accessor when real authentication is added.
        services.AddScoped<ICurrentCustomerAccessor, HeaderCurrentCustomerAccessor>();

        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IPL Franchise Store API",
                Version = "v1",
                Description = "Catalogue, search, cart, idempotent checkout and order history.",
            });
            o.OperationFilter<SwaggerCustomerHeaderFilter>();
            foreach (var xml in new[] { "IplStore.Api.xml", "IplStore.Application.xml" })
            {
                var path = Path.Combine(AppContext.BaseDirectory, xml);
                if (File.Exists(path))
                {
                    o.IncludeXmlComments(path);
                }
            }
        });

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

        services.AddCors();
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingSettings>>((options, settings) => ConfigureRateLimiter(options, settings.Value));

        return services;
    }

    private static void ConfigureRateLimiter(RateLimiterOptions options, RateLimitingSettings settings)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!settings.Enabled)
            {
                return RateLimitPartition.GetNoLimiter("disabled");
            }

            var customer = context.Request.Headers[HeaderCurrentCustomerAccessor.HeaderName].ToString();
            var partitionKey = !string.IsNullOrWhiteSpace(customer)
                ? $"customer:{customer}"
                : $"ip:{context.Connection.RemoteIpAddress}";

            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.PermitLimit,
                Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                QueueLimit = settings.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });
        });
    }
}
