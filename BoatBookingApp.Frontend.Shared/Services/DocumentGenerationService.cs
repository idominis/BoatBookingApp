using BoatBookingApp.Frontend.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xceed.Words.NET;
using BoatBookingApp.Frontend.Shared.Utilities;
using Xceed.Document.NET;

namespace BoatBookingApp.Frontend.Shared.Services
{
    public class DocumentGenerationService
    {
        public void GenerateDocument(TransferBooking booking, string pickUpLocation, string dropOffLocation, string pickUpMapLink, string dropOffMapLink, List<Location> locations, string templatePath, string outputPath)
        {
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException("Template file not found.", templatePath);
            }

            using (var doc = DocX.Load(templatePath))
            {
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
                Console.WriteLine($"Document saved to: {outputPath}");
            }
        }

        public void GenerateBoatBookingDocument(BoatBooking booking, string pickUpMapLink, IEnumerable<Extra> selectedExtras, string templatePath, string outputPath)
        {
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException("Template file not found.", templatePath);
            }

            using (var doc = DocX.Load(templatePath))
            {
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

                doc.ReplaceText(new StringReplaceTextOptions
                {
                    SearchValue = "{PassengerCount}",
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
                Console.WriteLine($"Document saved to: {outputPath}");
            }
        }

        private void ReplaceTextWithHyperlink(DocX doc, string placeholder, string displayText, string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                // Trim url to remove leading/trailing spaces
                url = url.Trim();
                displayText = displayText?.Trim() ?? url;
                bool replaced = false;

                foreach (var paragraph in doc.Paragraphs)
                {
                    // Normaliziraj tekst paragrafa za usporedbu
                    string paragraphText = paragraph.Text.Replace("\t", "").Trim();
                    if (paragraphText.Contains(placeholder))
                    {
                        try
                        {
                            Console.WriteLine($"Zamjena {placeholder} s hiperlinkom: {url}");
                            // Zamijeni placeholder praznim tekstom kako bismo izbjegli dupliciranje
                            paragraph.ReplaceText(placeholder, "");
                            // Dodaj hiperlink kao zaseban element
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
                    Console.WriteLine($"Placeholder {placeholder} nije pronađen u dokumentu!");
                    // Pokušaj zamijeniti običnim tekstom kao fallback
                    foreach (var paragraph in doc.Paragraphs)
                    {
                        if (paragraph.Text.Contains(placeholder))
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
                    if (paragraph.Text.Contains(placeholder))
                    {
                        Console.WriteLine($"Placeholder {placeholder} zamijenjen s praznim tekstom jer URL nije definiran.");
                        paragraph.ReplaceText(placeholder, "");
                    }
                }
            }
        }
    }
}