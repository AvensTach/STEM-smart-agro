using Microsoft.EntityFrameworkCore;
using SmartAgro.Backend.Data;
using SmartAgro.Backend.Hubs;
using SmartAgro.Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System;
using Serilog;

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

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    // 4. Ensure all logs are flushed to the file before the app fully shuts down
    Log.CloseAndFlush();
}