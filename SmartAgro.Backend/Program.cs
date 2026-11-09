using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SmartAgro.Backend.Data;
using SmartAgro.Backend.Hubs;
using SmartAgro.Backend.Services;
using SmartAgro.Shared;
using System;
using System.Text;

// 1. Initial Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/log-.txt",
        rollingInterval: RollingInterval.Day, // Creates daily files like log-20261109.txt
        fileSizeLimitBytes: 10485760, // 10 MB limit to prevent disk bloat
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 30) // Keeps exactly 30 days of history
    .CreateLogger();

try
{
    Log.Information("Starting SmartAgro Backend Application");

    var builder = WebApplication.CreateBuilder(args);

    // 2. Override default ASP.NET Core loggers with Serilog
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddHostedService<DataArchiverService>();
    builder.Services.AddHostedService<DataCleanupService>();
    builder.Services.AddSignalR();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=smartagro.db"));

    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "FallbackSecretKeyForDevelopmentOnly123!");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

    builder.Services.AddAuthorization();

    var app = builder.Build();

    // 3. Enable Serilog HTTP request logging (logs incoming endpoints and response times)
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<MicroclimateHub>("/hubs/microclimate");


    // Securely seed the IoT Worker service account
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.Migrate();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var workerUsername = config["IotSettings:Username"];
        var workerPassword = config["IotSettings:Password"];

        if (string.IsNullOrEmpty(workerUsername) || string.IsNullOrEmpty(workerPassword))
        {
            Log.Warning("Seeder skipped: IotSettings Username or Password is missing in Backend appsettings.json.");
        }
        else if (!context.Users.Any(u => u.Username == workerUsername))
        {
            context.Users.Add(new User
            {
                Username = workerUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(workerPassword),
                Role = "Admin" // Only the seeded account gets Admin
            });
            context.SaveChanges();
            Log.Information("Seeded IoT Worker Admin account.");
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw; // Re-throw the exception so the process exits with a failure status
}
finally
{
    Log.CloseAndFlush();
}