using System;
using System.Globalization;

namespace BoatBookingApp.Frontend.Shared.Utilities
{
    public static class Utility
    {
        public static string GetDateWithOrdinal(DateTime? date)
        {
            if (!date.HasValue) return null;

            int day = date.Value.Day;
            string suffix;
            switch (day % 10)
            {
                case 1 when day != 11:
                    suffix = "st";
                    break;
                case 2 when day != 12:
                    suffix = "nd";
                    break;
                case 3 when day != 13:
                    suffix = "rd";
                    break;
                default:
                    suffix = "th";
                    break;
            }

            return date.Value.ToString($"MMMM d'{suffix}', yyyy", CultureInfo.GetCultureInfo("en-US"));
        }
    }
}