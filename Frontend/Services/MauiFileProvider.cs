using BoatBookingApp.Frontend.Shared.Services;

namespace BoatBookingApp.Frontend.Services
{
    public class MauiFileProvider : IFileProvider
    {
        public async Task<Stream?> OpenAppPackageFileAsync(string fileName)
        {
            try
            {
                return await FileSystem.OpenAppPackageFileAsync(fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening file '{fileName}': {ex.Message}");
                return null;
            }
        }
    }
}