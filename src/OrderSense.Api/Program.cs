using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderSense.Api.Auth;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Demo;
using OrderSense.Api.Hubs;
using OrderSense.Api.Predictions;
using Hangfire;
using Hangfire.PostgreSql;

var isOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<AppDbContext>("ordersense",
    configureDbContextOptions: options => options.UseSnakeCaseNamingConvention());

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

var jwtOptions = builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations();

if (!isOpenApiGeneration)
{
    jwtOptions.ValidateOnStart();
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
    var secretKey = jwtOptions.Key.Trim();

    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        NameClaimType = "name",
        RoleClaimType = "role",
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/orders"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TokenService>();

builder.Services.AddHttpClient<PredictionClient>(client => client.BaseAddress = new Uri("https+http://prediction"));
builder.Services.AddScoped<OrderFeatureLoader>();
builder.Services.AddScoped<OrderPredictor>();

#pragma warning disable EXTEXP0001
builder.Services.AddHttpClient<BatchPredictor>(client => client.BaseAddress = new Uri("https+http://prediction"))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
    });
#pragma warning restore EXTEXP0001

var simulator = builder.Configuration.GetSection("Simulator").Get<SimulatorOptions>() ?? new SimulatorOptions();

if (simulator.Enabled && !isOpenApiGeneration)
{
    builder.Services.AddSingleton(simulator);
    builder.Services.AddHostedService<OrderSimulator>();
}

builder.Services.AddAuthorization();

builder.Services.AddProblemDetails();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddOpenApi();

builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();
builder.Services.AddSingleton<BatchProgressNotifier>();
builder.Services.AddScoped<WhatIfService>();
builder.Services.AddScoped<BatchJobProcessor>();
builder.Services.AddScoped<PredictMissingProcessor>();

bool poolerRemoved = false;
if (!isOpenApiGeneration)
{
    HangfireConnectionInfo hangfireInfo = HangfireConnection.Resolve(builder.Configuration);
    string hangfireConnection = hangfireInfo.ConnectionString;
    poolerRemoved = hangfireInfo.PoolerRemoved;

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(
            options => options.UseNpgsqlConnection(hangfireConnection),
            new PostgreSqlStorageOptions
            {
                SchemaName = "hangfire",
                QueuePollInterval = TimeSpan.FromSeconds(2),
            }));

    builder.Services.AddHangfireServer(options => options.WorkerCount = 2);
}

var app = builder.Build();

if (poolerRemoved)
{
    app.Logger.LogWarning("Hangfire needs a direct database endpoint: '-pooler' was removed from the host name");
}

if (!isOpenApiGeneration)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration, app.Logger);
    await DemoDataSeeder.SeedAsync(db, app.Environment, app.Configuration.GetValue("Seed:DemoOrders", 0), app.Logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapHub<OrdersHub>("/hubs/orders");

app.Run();