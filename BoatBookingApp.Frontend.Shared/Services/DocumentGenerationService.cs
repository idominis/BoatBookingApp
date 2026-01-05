using Xceed.Words.NET;
using BoatBookingApp.Frontend.Shared.Models;
using BoatBookingApp.Frontend.Shared.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using Xceed.Document.NET;
using BoatBookingApp.Frontend.Shared.Utilities;

namespace BoatBookingApp.Frontend.Shared.Services
{
    public class DocumentGenerationService
    {
        private readonly IDbContextFactory<BoatBookingContext> _dbContextFactory;
        private readonly IFileProvider _fileProvider;

        public DocumentGenerationService(
            IDbContextFactory<BoatBookingContext> dbContextFactory,
            IFileProvider fileProvider)
        {
            _dbContextFactory = dbContextFactory;
            _fileProvider = fileProvider;
        }

        private string NormalizeLocationName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            name = Regex.Replace(name.Trim(), @"\s+", " ");
            return name;
        }

        public async Task GenerateBoatBookingDocumentAsync(
            BoatBooking booking, 
            string pickUpMapLink, 
            IEnumerable<Extra> selectedExtras, 
            string templateFileName,
            string outputPath, 
            bool isNotesTemplate = false)
        {
            try
            {
                using var templateStream = await _fileProvider.OpenAppPackageFileAsync(templateFileName);
                
                if (templateStream == null)
                {
                    throw new FileNotFoundException($"Template '{templateFileName}' not found in app package");
                }

                using var doc = DocX.Load(templateStream);

                string startDate = isNotesTemplate
                    ? (booking.StartDate?.ToString("d.M.", new CultureInfo("hr-HR")) ?? "N/A")
                    : (booking.StartDate?.ToString("dd-MM-yyyy") ?? "N/A");
                    
                string endDate = isNotesTemplate
                    ? (booking.EndDate?.ToString("d.M.", new CultureInfo("hr-HR")) ?? startDate)
                    : (booking.EndDate?.ToString("dd-MM-yyyy") ?? startDate);

                string pickupTime = booking.PickupTime?.ToString(@"hh\:mm") ?? "N/A";
                string returnTime = booking.ReturnTime?.ToString(@"hh\:mm") ?? "N/A";
                string skipperIncluded = booking.SkipperRequired ? "Yes" : "No";
                string fuelIncluded = booking.FuelIncluded ? "Yes" : "No";
                string extrasText = selectedExtras.Any() ? string.Join(", ", selectedExtras.Select(e => e.Name)) : "None";

                doc.ReplaceText("<<boat_name>>", booking.BoatName ?? "N/A");
                doc.ReplaceText("<<start_date>>", startDate);
                doc.ReplaceText("<<end_date>>", endDate);
                doc.ReplaceText("<<passenger_count>>", booking.PassengerCount.ToString());
                doc.ReplaceText("<<pickup_time>>", pickupTime);
                doc.ReplaceText("<<return_time>>", returnTime);
                doc.ReplaceText("<<skipper_included>>", skipperIncluded);
                doc.ReplaceText("<<fuel_included>>", fuelIncluded);
                doc.ReplaceText("<<total_price>>", $"{booking.TotalPrice:F2} EUR");
                doc.ReplaceText("<<deposit_paid>>", $"{booking.DepositPaid:F2} EUR");
                doc.ReplaceText("<<renter_name>>", booking.RenterName ?? "N/A");
                doc.ReplaceText("<<renter_email>>", booking.RenterEmail ?? "N/A");
                doc.ReplaceText("<<renter_phone>>", booking.RenterPhone ?? "N/A");
                doc.ReplaceText("<<extras>>", extrasText);

                if (!string.IsNullOrEmpty(pickUpMapLink))
                {
                    ReplaceTextWithHyperlink(doc, "<<pickup_location>>", pickUpMapLink, pickUpMapLink);
                }
                else
                {
                    doc.ReplaceText("<<pickup_location>>", booking.CustomDepartureLocationName ?? "Custom Location");
                }

                // Save document
                doc.SaveAs(outputPath);
                Console.WriteLine($"Document generated successfully: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating document: {ex.Message}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        // Existing sync method - keep for backward compatibility
        public void GenerateBoatBookingDocument(BoatBooking booking, string pickUpMapLink, IEnumerable<Extra> selectedExtras, string templatePath, string outputPath, bool isNotesTemplate = false)
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

                // Different date formatting based on template type
                string dateFormatted;
                if (isNotesTemplate)
                {
                    // Croatian format dd/mm/yyyy for Notes template
                    dateFormatted = booking.StartDate.HasValue 
                        ? booking.StartDate.Value.ToString("dd/MM/yyyy")
                        : "N/A";
                }
                else
                {
                    // English format with ordinal for other templates
                    dateFormatted = Utility.GetDateWithOrdinal(booking.StartDate) ?? "N/A";
                }
                
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Date}",
                    NewValue = dateFormatted
                });

                // Add DayOfTheWeak handling
                string dayOfWeek = booking.StartDate.HasValue 
                    ? booking.StartDate.Value.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
                    : "N/A";
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{DayOfTheWeak}",
                    NewValue = dayOfWeek
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

        public async Task GenerateTransferDocumentAsync(
            TransferBooking booking,
            string pickUpLocation,
            string dropOffLocation,
            string templateFileName,
            string outputPath,
            bool isReTour = false,
            bool isNotesTemplate = false)
        {
            try
            {
                using var templateStream = await _fileProvider.OpenAppPackageFileAsync(templateFileName);
                
                if (templateStream == null)
                {
                    throw new FileNotFoundException($"Template '{templateFileName}' not found in app package");
                }

                // Load DocX from stream
                using var doc = DocX.Load(templateStream);

                // Formatting dates
                string dateFormatted;
                if (isNotesTemplate)
                {
                    var date = isReTour ? booking.ReTourDate : booking.DepartureDate;
                    dateFormatted = date.HasValue 
                        ? date.Value.ToString("d.M.", new CultureInfo("hr-HR"))
                        : "N/A";
                }
                else
                {
                    var date = isReTour ? booking.ReTourDate : booking.DepartureDate;
                    dateFormatted = Utility.GetDateWithOrdinal(date) ?? "N/A";
                }

                // Formatting time
                var time = isReTour ? booking.ReTourTime : booking.DepartureTime;
                string timeFormatted = time.HasValue
                    ? $"{time.Value.Hours:D2}:{time.Value.Minutes:D2}"
                    : "N/A";

                // Day of week
                var dateValue = isReTour ? booking.ReTourDate : booking.DepartureDate;
                string dayOfWeek = dateValue.HasValue 
                    ? dateValue.Value.ToString("dddd", CultureInfo.GetCultureInfo("en-US"))
                    : "N/A";

                // Replace placeholders
                doc.ReplaceText("<<date>>", dateFormatted);
                doc.ReplaceText("<<day_of_week>>", dayOfWeek);
                doc.ReplaceText("<<time>>", timeFormatted);
                doc.ReplaceText("<<pickup_location>>", pickUpLocation);
                doc.ReplaceText("<<dropoff_location>>", dropOffLocation);
                doc.ReplaceText("<<passenger_count>>", booking.PassengerCount.ToString());
                doc.ReplaceText("<<luggage>>", booking.Luggage ? "Yes" : "No");
                doc.ReplaceText("<<total_price>>", $"{booking.TotalPrice:F2} EUR");
                doc.ReplaceText("<<deposit_paid>>", $"{booking.DepositPaid:F2} EUR");
                doc.ReplaceText("<<remaining_amount>>", $"{(booking.TotalPrice - booking.DepositPaid):F2} EUR");
                doc.ReplaceText("<<renter_name>>", booking.RenterName ?? "N/A");
                doc.ReplaceText("<<renter_email>>", booking.RenterEmail ?? "N/A");

                // Save document
                doc.SaveAs(outputPath);
                Console.WriteLine($"Transfer document generated successfully: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating transfer document: {ex.Message}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        // LEGACY SYNC METHOD - Keep for backward compatibility (Windows desktop app)
        public void GenerateDocument(
            TransferBooking booking,
            string pickUpLocation,
            string dropOffLocation,
            string templatePath,
            string outputPath,
            bool isReTour = false,
            bool isNotesTemplate = false)
        {
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Template file not found: {templatePath}");
            }

            Console.WriteLine($"Generiranje transfer dokumenta: {templatePath} -> {outputPath}");
            using (var doc = DocX.Load(templatePath))
            {
                // Contact info
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{ContactName}",
                    NewValue = booking.RenterName ?? "Guest"
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{ContactEmail}",
                    NewValue = booking.RenterEmail ?? "N/A"
                });

                // Date formatting
                string dateFormatted;
                if (isNotesTemplate)
                {
                    var date = isReTour ? booking.ReTourDate : booking.DepartureDate;
                    dateFormatted = date.HasValue 
                        ? date.Value.ToString("dd/MM/yyyy")
                        : "N/A";
                }
                else
                {
                    var date = isReTour ? booking.ReTourDate : booking.DepartureDate;
                    dateFormatted = Utility.GetDateWithOrdinal(date) ?? "N/A";
                }
                
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Date}",
                    NewValue = dateFormatted
                });

                // Day of the week
                var dateValue = isReTour ? booking.ReTourDate : booking.DepartureDate;
                string dayOfWeek = dateValue.HasValue 
                    ? dateValue.Value.ToString("dddd", CultureInfo.GetCultureInfo("en-US"))
                    : "N/A";
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{DayOfTheWeak}",
                    NewValue = dayOfWeek
                });

                // Time formatting
                var time = isReTour ? booking.ReTourTime : booking.DepartureTime;
                string timeFormatted = time.HasValue
                    ? $"{time.Value.Hours:D2}:{time.Value.Minutes:D2}"
                    : "N/A";
                Console.WriteLine($"Formatirano vrijeme za dokument: {timeFormatted}");
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Time}",
                    NewValue = timeFormatted
                });

                // Locations
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{PickUpLocation}",
                    NewValue = pickUpLocation
                });

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{DropOffLocation}",
                    NewValue = dropOffLocation
                });

                // Passenger count
                Console.WriteLine($"Zamjena {{PassengerCount}} s {booking.PassengerCount}");
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{PassengerCount}",
                    NewValue = booking.PassengerCount.ToString()
                });

                // Luggage
                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{Luggage}",
                    NewValue = booking.Luggage ? "Yes" : "No"
                });

                // Prices
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