using BoatBookingApp.Frontend.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xceed.Words.NET;
using BoatBookingApp.Frontend.Shared.Utilities;
using Xceed.Document.NET;
using Microsoft.EntityFrameworkCore;
using BoatBookingApp.Frontend.Shared.Data;

namespace BoatBookingApp.Frontend.Shared.Services
{
    public class DocumentGenerationService
    {
        private readonly IDbContextFactory<BoatBookingContext> _dbContextFactory;

        public DocumentGenerationService(IDbContextFactory<BoatBookingContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        private string NormalizeLocationName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            name = Regex.Replace(name.Trim(), @"\s+", " ");
            return name;
        }

        public void GenerateDocument(TransferBooking booking, string pickUpLocation, string dropOffLocation, string pickUpMapLink, string dropOffMapLink, List<Location> locations, string templatePath, string outputPath)
        {
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Template file not found: {templatePath}");
            }

            Console.WriteLine($"Generiranje transfer dokumenta: {templatePath} -> {outputPath}");
            using (var doc = DocX.Load(templatePath))
            {
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{ContactName}",
                    NewValue = booking.RenterName ?? "N/A"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{ContactPhone}",
                    NewValue = booking.RenterPhone ?? "N/A"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{PickUpLocation}",
                    NewValue = pickUpLocation
                });

                ReplaceTextWithHyperlink(doc, "{PickUpMapLink}", pickUpMapLink, pickUpMapLink);

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{DropOffLocation}",
                    NewValue = dropOffLocation
                });

                ReplaceTextWithHyperlink(doc, "{DropOffMapLink}", dropOffMapLink, dropOffMapLink);

                string dateWithOrdinal = Utility.GetDateWithOrdinal(booking.DepartureDate);
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Date}",
                    NewValue = dateWithOrdinal ?? "N/A"
                });

                string timeFormatted = booking.DepartureTime.HasValue
                    ? $"{booking.DepartureTime.Value.Hours:D2}:{booking.DepartureTime.Value.Minutes:D2}"
                    : "N/A";
                Console.WriteLine($"Formatirano vrijeme za dokument: {timeFormatted}");
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Time}",
                    NewValue = timeFormatted
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{PassengerCount}",
                    NewValue = booking.PassengerCount.ToString()
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{SkipperStatus}",
                    NewValue = "Included in the price"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{LuggageStatus}",
                    NewValue = booking.Luggage ? "Included in the price" : "Not included"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{FuelStatus}",
                    NewValue = booking.FuelIncluded ? "Included in the price" : "Not included"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{TotalPrice}",
                    NewValue = $"{booking.TotalPrice}€"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Deposit}",
                    NewValue = $"{booking.DepositPaid}€"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{RemainingAmount}",
                    NewValue = $"{booking.TotalPrice - booking.DepositPaid}€"
                });

                doc.SaveAs(outputPath);
                Console.WriteLine($"Transfer dokument spremljen na: {outputPath}");
            }
        }

        public void GenerateBoatBookingDocument(BoatBooking booking, string pickUpMapLink, IEnumerable<Extra> selectedExtras, string templatePath, string outputPath)
        {
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Template file not found: {templatePath}");
            }

            Console.WriteLine($"Generiranje boat dokumenta: {templatePath} -> {outputPath}");
            using (var doc = DocX.Load(templatePath))
            {
                // Provjera prisutnosti placeholdera
                if (!doc.Text.Contains("{PassengerCount}"))
                {
                    Console.WriteLine("Placeholder {PassengerCount} nije pronađen u templateu!");
                }

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{ContactName}",
                    NewValue = booking.RenterName ?? "Guest"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{ContactPhone}",
                    NewValue = booking.RenterPhone ?? "N/A"
                });

                string dateWithOrdinal = Utility.GetDateWithOrdinal(booking.StartDate);
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Date}",
                    NewValue = dateWithOrdinal ?? "N/A"
                });

                string pickupTimeFormatted = booking.PickupTime.HasValue
                    ? $"{booking.PickupTime.Value.Hours:D2}:{booking.PickupTime.Value.Minutes:D2}"
                    : "N/A";
                Console.WriteLine($"Formatirano vrijeme za dokument (Pickup): {pickupTimeFormatted}");
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Time}",
                    NewValue = pickupTimeFormatted
                });

                string returnTimeFormatted = booking.ReturnTime.HasValue
                    ? $"{booking.ReturnTime.Value.Hours:D2}:{booking.ReturnTime.Value.Minutes:D2}"
                    : "N/A";
                Console.WriteLine($"Formatirano vrijeme za dokument (Return): {returnTimeFormatted}");
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{ReturnTime}",
                    NewValue = returnTimeFormatted
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{BoatName}",
                    NewValue = booking.BoatName ?? "N/A"
                });

                Console.WriteLine($"Zamjena {{PassengerCount}} s {booking.PassengerCount}");
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{PassengerCount}",
                    NewValue = booking.PassengerCount.ToString()
                });

                // Fallback za pogrešan placeholder
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{PassengerCount",
                    NewValue = booking.PassengerCount.ToString()
                });

                string extrasFormatted = selectedExtras.Any()
                    ? string.Join(", ", selectedExtras.Select(e => e.Name))
                    : "None";
                Console.WriteLine($"Formatirani extras za dokument: {extrasFormatted}");
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Extras}",
                    NewValue = extrasFormatted
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{SkipperStatus}",
                    NewValue = booking.SkipperRequired ? "Included" : "Not included"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{FuelStatus}",
                    NewValue = booking.FuelIncluded ? "Included" : "Not included"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{TotalPrice}",
                    NewValue = $"{booking.TotalPrice}€"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Deposit}",
                    NewValue = $"{booking.DepositPaid}€"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{RemainingAmount}",
                    NewValue = $"{booking.TotalPrice - booking.DepositPaid}€"
                });

                ReplaceTextWithHyperlink(doc, "{PickUpMapLink}", pickUpMapLink, pickUpMapLink);

                doc.SaveAs(outputPath);
                Console.WriteLine($"Boat dokument spremljen na: {outputPath}");
            }
        }

        private void ReplaceTextWithHyperlink(DocX doc, string placeholder, string displayText, string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                url = url.Trim();
                displayText = displayText?.Trim() ?? url;
                bool replaced = false;

                foreach (var paragraph in doc.Paragraphs)
                {
                    var textElements = paragraph.FindAll(placeholder);
                    if (textElements.Any())
                    {
                        try
                        {
                            Console.WriteLine($"Zamjena {placeholder} s hiperlinkom: {url} u paragrafu: {paragraph.Text}");
                            paragraph.ReplaceText(placeholder, "");
                            var hyperlink = doc.AddHyperlink(displayText, new Uri(url));
                            paragraph.AppendHyperlink(hyperlink);
                            replaced = true;
                        }
                        catch (UriFormatException ex)
                        {
                            Console.WriteLine($"Invalid URL format for {placeholder}: {url}, error: {ex.Message}");
                            paragraph.ReplaceText(placeholder, displayText);
                        }
                    }
                }

                if (!replaced)
                {
                    Console.WriteLine($"Placeholder {placeholder} nije pronađen u dokumentu! Tekst paragrafa: {string.Join(" | ", doc.Paragraphs.Select(p => p.Text))}");
                    foreach (var paragraph in doc.Paragraphs)
                    {
                        var textElements = paragraph.FindAll(placeholder);
                        if (textElements.Any())
                        {
                            paragraph.ReplaceText(placeholder, displayText);
                        }
                    }
                }
            }
            else
            {
                foreach (var paragraph in doc.Paragraphs)
                {
                    var textElements = paragraph.FindAll(placeholder);
                    if (textElements.Any())
                    {
                        Console.WriteLine($"Placeholder {placeholder} zamijenjen s praznim tekstom jer URL nije definiran.");
                        paragraph.ReplaceText(placeholder, "");
                    }
                }
            }
        }

        public async Task<List<ConsistencyReport>> GetDocumentsForDate(DateTime date)
        {
            var result = new List<ConsistencyReport>();
            string dateStr = date.ToString("dd-MM-yyyy");
            Console.WriteLine($"Skeniranje dokumenata za datum: {dateStr}");

            try
            {
                // Putanja za glisere
                string boatBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "BumbarRent", "Booking_confrmations", "2025", "0_Boats_2025");
                if (Directory.Exists(boatBasePath))
                {
                    var boatFolders = Directory.GetDirectories(boatBasePath)
                        .Where(d => d.Contains(dateStr) && d.Contains("_Boat_"))
                        .ToList();

                    foreach (var folder in boatFolders)
                    {
                        var folderName = Path.GetFileName(folder);
                        var parts = folderName.Split('_');
                        if (parts.Length >= 4 && int.TryParse(parts[0], out var bookingId))
                        {
                            string boatName = parts[2];
                            if (Directory.GetFiles(folder, "*.docx").Any())
                            {
                                result.Add(new ConsistencyReport
                                {
                                    BookingId = bookingId,
                                    Type = "Boat",
                                    BoatName = boatName,
                                    Details = NormalizeLocationName(boatName)
                                });
                                Console.WriteLine($"Pronađen dokument glisera: BookingId={bookingId}, BoatName={boatName}, Normalized={NormalizeLocationName(boatName)}");
                            }
                        }
                    }
                    Console.WriteLine($"Pronađeno {boatFolders.Count} mapa glisera za {dateStr}.");
                }
                else
                {
                    Console.WriteLine($"Putanja za glisere ne postoji: {boatBasePath}");
                }

                // Putanja za transfere
                string transferBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "BumbarRent", "Booking_confrmations", "2025");
                if (Directory.Exists(transferBasePath))
                {
                    // Dohvati sve mape transfera
                    var transferFolders = Directory.GetDirectories(transferBasePath)
                        .Where(d => d.Contains("_Transfer_"))
                        .ToList();

                    foreach (var folder in transferFolders)
                    {
                        var folderName = Path.GetFileName(folder);
                        var parts = folderName.Split('_');
                        if (parts.Length >= 4 && int.TryParse(parts[0], out var bookingId))
                        {
                            string locationsPart = parts[2];
                            string normalizedDetails = NormalizeLocationName(locationsPart);

                            // Provjeri dokumente u mapi
                            var files = Directory.GetFiles(folder, "*.docx");
                            foreach (var file in files)
                            {
                                string fileName = Path.GetFileName(file);
                                bool isRetour = fileName.Contains("_ReTour");
                                string details = isRetour
                                    ? $"{parts[2].Split('-')[1]}-{parts[2].Split('-')[0]}" // Obrnuti redoslijed za retour
                                    : normalizedDetails;

                                using var dbContext = _dbContextFactory.CreateDbContext();
                                var transfer = await dbContext.TransferBookings
                                    .FirstOrDefaultAsync(t => t.Id == bookingId &&
                                                              ((t.DepartureDate.HasValue && t.DepartureDate.Value.Date == date.Date && !isRetour) ||
                                                               (t.WithReTour && t.ReTourDate.HasValue && t.ReTourDate.Value.Date == date.Date && isRetour)));

                                if (transfer != null)
                                {
                                    result.Add(new ConsistencyReport
                                    {
                                        BookingId = bookingId,
                                        Type = "Transfer",
                                        Locations = NormalizeLocationName(details),
                                        Details = NormalizeLocationName(details)
                                    });
                                    Console.WriteLine($"Pronađen dokument transfera: BookingId={bookingId}, Details={details}, File={fileName}");
                                }
                            }
                        }
                    }
                    Console.WriteLine($"Pronađeno {transferFolders.Count} mapa transfera.");
                }
                else
                {
                    Console.WriteLine($"Putanja za transfere ne postoji: {transferBasePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u GetDocumentsForDate: {ex.Message}, StackTrace: {ex.StackTrace}");
            }

            return await Task.FromResult(result);
        }
    }
}