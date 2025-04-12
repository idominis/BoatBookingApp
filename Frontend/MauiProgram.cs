using BoatBookingApp.Frontend.Shared;
using BoatBookingApp.Frontend.Shared.Data;
using BoatBookingApp.Frontend.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using System.Reflection;

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
            .SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

            builder.Services.AddSingleton<IConfiguration>(configuration);

            string connectionString = configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();

            builder.Services.AddDbContextFactory<BoatBookingContext>(options =>
            options.UseMySQL(connectionString));

            builder.Services.AddSingleton<BookerStateService>();
            builder.Services.AddScoped<GoogleSheetsService>();
            builder.Services.AddScoped<DocumentGenerationService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}