using Serilog;
using SmartAgro.IoT;

var builder = Host.CreateApplicationBuilder(args);

// Settings Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/iot-worker-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Registration of Auth Handler and HttpClient
builder.Services.AddTransient<JwtAuthenticationHandler>();

builder.Services.AddHttpClient("IotClient", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["IotSettings:BaseUrl"] ?? "http://10.0.2.2:5297");
}).AddHttpMessageHandler<JwtAuthenticationHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();