using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace BoatBookingApp.Frontend.Shared.Services
{
    public class GoogleSheetsService
    {
        private readonly SheetsService sheetsService;
        private readonly string spreadsheetId;

        public GoogleSheetsService(IConfiguration configuration)
        {
            var credentialsPath = configuration["GoogleSheets:CredentialsPath"];
            spreadsheetId = configuration["GoogleSheets:SpreadsheetId"];

            if (string.IsNullOrEmpty(credentialsPath))
            {
                throw new ArgumentNullException(nameof(credentialsPath), "GoogleSheets:CredentialsPath nije definiran u appsettings.json.");
            }
            if (string.IsNullOrEmpty(spreadsheetId))
            {
                throw new ArgumentNullException(nameof(spreadsheetId), "GoogleSheets:SpreadsheetId nije definiran u appsettings.json.");
            }

            if (!File.Exists(credentialsPath))
            {
                throw new FileNotFoundException($"JSON ključ nije pronađen na putanji: {credentialsPath}");
            }

            GoogleCredential credential;
            using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(SheetsService.Scope.Spreadsheets);
            }

            sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "BumbarRentSheets"
            });
        }

        public async Task UpdateGoogleSheet(DateTime date, string pickUpLocation, string dropOffLocation, int passengerCount, TimeSpan? time, string shortName)
        {
            try
            {
                // Formatiranje datuma za Google Sheet (usklađeno s formatom 15.4.)
                string dateStr = date.ToString("d.M.", CultureInfo.InvariantCulture);
                Console.WriteLine($"Pokušaj upisa za datum: {dateStr}");

                // Pronalaženje reda za datum, preskačemo prvi redak (naslov)
                string range = "2025!A2:A"; // Počinjemo od A2 kako bismo preskočili naslov
                var getRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
                var getResponse = await getRequest.ExecuteAsync();
                int rowIndex = -1;

                if (getResponse.Values != null)
                {
                    for (int i = 0; i < getResponse.Values.Count; i++)
                    {
                        if (getResponse.Values[i].Count > 0)
                        {
                            string sheetDate = getResponse.Values[i][0].ToString();
                            Console.WriteLine($"Pronađen datum u Sheetu: {sheetDate}");
                            // Pokušaj parsiranja s više formata, fokus na "d.M."
                            if (DateTime.TryParseExact(sheetDate,
                            new[] { "d.M.", "d.M.yyyy", "dd.MM.yyyy", "dd-MM-yyyy", "dd/MM/yyyy" },
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out DateTime parsedSheetDate))
                            {
                                if (parsedSheetDate.Date == date.Date)
                                {
                                    rowIndex = i + 2; // +2 jer počinjemo od A2 (red 2 u Sheetu)
                                    Console.WriteLine($"Podudaranje pronađeno, red: {rowIndex}");
                                    break;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Neuspješno parsiranje datuma: {sheetDate}");
                            }
                        }
                    }
                }

                // Ako datum nije pronađen, dodaj novi red
                if (rowIndex == -1)
                {
                    rowIndex = getResponse.Values != null ? getResponse.Values.Count + 2 : 2;
                    var appendRange = $"2025!A{rowIndex}";
                    var appendValue = new ValueRange { Values = new List<IList<object>> { new List<object> { dateStr } } };
                    var appendRequest = sheetsService.Spreadsheets.Values.Update(appendValue, spreadsheetId, appendRange);
                    appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                    await appendRequest.ExecuteAsync();
                    Console.WriteLine($"Dodan novi red za datum {dateStr} na poziciji {rowIndex}");
                }

                // Provjera slobodnog polja u stupcima U, V, W
                string checkRange = $"2025!U{rowIndex}:W{rowIndex}";
                var checkRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, checkRange);
                var checkResponse = await checkRequest.ExecuteAsync();

                int columnIndex = -1;
                if (checkResponse.Values == null || checkResponse.Values.Count == 0 || checkResponse.Values[0].Count < 3)
                {
                    columnIndex = checkResponse.Values == null || checkResponse.Values[0].Count == 0 ? 20 : checkResponse.Values[0].Count + 20;
                }
                else if (checkResponse.Values[0].Count == 3)
                {
                    throw new InvalidOperationException("Tri transfera već bukirana za ovaj datum!");
                }

                // Upis ShortName u slobodno polje
                char columnLetter = (char)('U' + (columnIndex - 20));
                string shortNameRange = $"2025!{columnLetter}{rowIndex}";
                var shortNameValue = new ValueRange { Values = new List<IList<object>> { new List<object> { shortName } } };
                var shortNameRequest = sheetsService.Spreadsheets.Values.Update(shortNameValue, spreadsheetId, shortNameRange);
                shortNameRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await shortNameRequest.ExecuteAsync();
                Console.WriteLine($"ShortName '{shortName}' upisan u {shortNameRange}");

                // Bojanje ćelije modrom bojom s bijelim tekstom
                var formatRequest = new BatchUpdateSpreadsheetRequest
                {
                    Requests = new List<Request>
{
new Request
{
RepeatCell = new RepeatCellRequest
{
Range = new GridRange
{
SheetId = 300482270, // Zamijeni s točnim SheetId za sheet "2025"
StartRowIndex = rowIndex - 1,
EndRowIndex = rowIndex,
StartColumnIndex = columnIndex,
EndColumnIndex = columnIndex + 1
},
Cell = new CellData
{
UserEnteredFormat = new CellFormat
{
BackgroundColor = new Color { Red = 0, Green = 0, Blue = 1 },
TextFormat = new TextFormat { ForegroundColor = new Color { Red = 1, Green = 1, Blue = 1 } }
}
},
Fields = "userEnteredFormat.backgroundColor,userEnteredFormat.textFormat"
}
}
}
                };
                await sheetsService.Spreadsheets.BatchUpdate(formatRequest, spreadsheetId).ExecuteAsync();
                Console.WriteLine($"Ćelija {shortNameRange} obojana modro s bijelim tekstom");

                // Dohvaćanje postojeće napomene iz stupca X
                string noteRange = $"2025!X{rowIndex}";
                var noteGetRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, noteRange);
                var noteGetResponse = await noteGetRequest.ExecuteAsync();
                string existingNote = noteGetResponse.Values != null && noteGetResponse.Values.Count > 0 && noteGetResponse.Values[0].Count > 0
                ? noteGetResponse.Values[0][0].ToString()
                : "";
                Console.WriteLine($"Postojeća napomena: {existingNote}");

                // Kreiranje nove napomene s prefiksom T1, T2, T3
                string transferPrefix = columnIndex switch
                {
                    20 => "T1",
                    21 => "T2",
                    22 => "T3",
                    _ => "T?"
                };
                string timeFormatted = time.HasValue ? $"{time.Value.Hours:D2}:{time.Value.Minutes:D2}" : "N/A";
                string newNote = $"{transferPrefix}: {pickUpLocation ?? "N/A"}-{dropOffLocation ?? "N/A"}, {passengerCount} osobe, polazak u {timeFormatted}";
                Console.WriteLine($"Nova napomena: {newNote}");

                // Kombiniranje postojeće i nove napomene
                string combinedNote = string.IsNullOrEmpty(existingNote) ? newNote : $"{existingNote} / {newNote}";

                // Upis napomene u stupac X
                var noteValue = new ValueRange { Values = new List<IList<object>> { new List<object> { combinedNote } } };
                var noteRequest = sheetsService.Spreadsheets.Values.Update(noteValue, spreadsheetId, noteRange);
                noteRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await noteRequest.ExecuteAsync();
                Console.WriteLine($"Napomena upisana u {noteRange}: {combinedNote}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u UpdateGoogleSheet: {ex.Message}, InnerException: {ex.InnerException?.Message}");
                throw;
            }
        }
    }
}