using BoatBookingApp.Frontend.Shared;
using BoatBookingApp.Frontend.Shared.Data;
using Microsoft.EntityFrameworkCore;
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

            // Definirajte connection string ovdje
            //var connectionString = "Server=185.62.73.24;Database=dominish_boat_booking_db;User=dominish_boat_user;Password=uB1vUbKQ7406#Xsj;";
            //var connectionString = "mysql://dominish_boat_user:uB1vUbKQ7406#Xsj@cp014.mydataknox.com:3306/dominish_boat_booking_db";
            //var connectionString = "Server=cp014.mydataknox.com;Database=dominish_boat_booking_db;User=dominish_boat_user;Password=uB1vUbKQ7406#Xsj;Port=3306;";
            string connectionString = "Server=sql7.freesqldatabase.com;Database=sql7770324;User Id=sql7770324;Password=5uE8lQpVS4;Port=3306;";

            //string connectionString = "Server=sql101.infinityfree.com;Port=3306;Database=if0_38634110_boat_booking_db;User Id=if0_38634110;Password=NJvT3NdlkS857QR;";

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();

            // Dodajte BoatBookingContext u DI container
            //builder.Services.AddDbContext<BoatBookingContext>(options =>
            //    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
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