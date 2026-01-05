using BoatBookingApp.Frontend.Shared;
using BoatBookingApp.Frontend.Shared.Data;
using BoatBookingApp.Frontend.Shared.Services;
using BoatBookingApp.Frontend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

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

            IConfiguration configuration;
            using (var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").Result)
            using (var reader = new StreamReader(stream))
            {
                var json = reader.ReadToEnd();
                var memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                configuration = new ConfigurationBuilder()
                    .AddJsonStream(memoryStream)
                    .Build();
            }

            builder.Services.AddSingleton<IConfiguration>(configuration);

            string connectionString = configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddMauiBlazorWebView();
            
            builder.Services.AddMudServices();

            builder.Services.AddDbContextFactory<BoatBookingContext>(options =>
                options.UseMySQL(connectionString));

            builder.Services.AddSingleton<BoatBookingApp.Frontend.Shared.Services.IFileProvider, MauiFileProvider>();
            
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