using BoatBookingApp.Frontend.Shared;
using BoatBookingApp.Frontend.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Microsoft.Extensions.Configuration;

namespace BoatBookingApp.Frontend
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            // Load configuration from appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Get connection string from configuration
            string connectionString = configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();

            // Add BoatBookingContext to DI container
            builder.Services.AddDbContext<BoatBookingContext>(options =>
                  options.UseMySQL(connectionString));

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}