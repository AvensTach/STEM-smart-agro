using Serilog;
using SmartAgro.IoT;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

// Configuring Serilog for Log Rotation
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/iot-worker-.log",
        rollingInterval: RollingInterval.Day, // Creates a new file every day: iot - worker - 20261102.log
        retainedFileCountLimit: 7,            // Keep logs for the last 7 days
        fileSizeLimitBytes: 10 * 1024 * 1024) // Max 10 MB per file
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();