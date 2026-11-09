using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using SmartAgro.Frontend.Helpers;

namespace SmartAgro.Frontend;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        
        builder.Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl)
        });

      
        // builder.Services.AddTransient<LoginPage>();
        // builder.Services.AddScoped<AuthService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}