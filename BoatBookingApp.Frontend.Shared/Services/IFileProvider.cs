namespace BoatBookingApp.Frontend.Shared.Services
{
    public interface IFileProvider
    {
        Task<Stream?> OpenAppPackageFileAsync(string fileName);
    }
}