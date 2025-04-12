using BoatBookingApp.Frontend.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
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

        private void ReplaceTextWithHyperlink(DocX doc, string placeholder, string displayText, string url)
        {
            foreach (var paragraph in doc.Paragraphs)
            {
                if (paragraph.Text.Contains(placeholder))
                {
                    var hyperlink = doc.AddHyperlink(displayText, new Uri(url));
                    paragraph.ReplaceText(placeholder, "");
                    paragraph.AppendHyperlink(hyperlink);
                }
            }
        }
    }
}