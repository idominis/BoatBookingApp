using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BoatBookingApp.Frontend.Shared.Models;
using BoatBookingApp.Frontend.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace BoatBookingApp.Frontend.Shared.Services
{
    public class GoogleSheetsService
    {
        private readonly SheetsService? sheetsService;
        private readonly string? spreadsheetId;
        private readonly IDbContextFactory<BoatBookingContext> dbContextFactory;
        private const int EVIDENCIJA_SHEET_ID = 1240119258;
        private const int SHEET_2025_ID = 300482270;
        private readonly bool _isInitialized;

        public GoogleSheetsService(
            IConfiguration configuration, 
            IDbContextFactory<BoatBookingContext> dbContextFactory,
            IFileProvider fileProvider) // DODANO
        {
            this.dbContextFactory = dbContextFactory;
            
            try
            {
                spreadsheetId = configuration["GoogleSheets:SpreadsheetId"];
                
                // Koristi IFileProvider umjesto direktnog FileSystem pristupa
                string jsonFileName = "speedboatbookingapp-e48f775027f5.json";
                
                var stream = fileProvider.OpenAppPackageFileAsync(jsonFileName).Result;
                
                if (stream == null)
                {
                    Console.WriteLine($"GoogleSheetsService: JSON key file '{jsonFileName}' not found. Service will run in offline mode.");
                    _isInitialized = false;
                    return;
                }

                using (stream)
                {
                    var credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(new[] { SheetsService.Scope.Spreadsheets });

                    sheetsService = new SheetsService(new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "Boat Booking App"
                    });
                
                    _isInitialized = true;
                    Console.WriteLine("GoogleSheetsService initialized successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GoogleSheetsService initialization error: {ex.Message}");
                _isInitialized = false;
            }
        }

        private string NormalizeLocationName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            name = Regex.Replace(name.Trim(), @"\s+", " ");
            return name;
        }

        private string NormalizeTransferDetails(string details)
        {
            if (string.IsNullOrEmpty(details))
                return "";
            var match = Regex.Match(details, @"^T\d: (.*?)(?:,|$)");
            string normalized = match.Success ? match.Groups[1].Value.Trim() : details;
            var parts = normalized.Split('-').Select(NormalizeLocationName).ToArray();
            return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : normalized;
        }

        public async Task<bool> CheckExistingTransfer(DateTime date, string pickUpLocation, string dropOffLocation, int passengerCount, TimeSpan? time)
        {
            if (!_isInitialized || sheetsService == null)
            {
                Console.WriteLine("GoogleSheetsService not initialized, skipping CheckExistingTransfer");
                return false;
            }

            try
            {
                string dateStr = date.ToString("d.M.", CultureInfo.InvariantCulture);
                string checkRange = $"2025!A2:W";
                var checkRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, checkRange);
                var checkResponse = await checkRequest.ExecuteAsync();

                if (checkResponse.Values != null)
                {
                    for (int i = 0; i < checkResponse.Values.Count; i++)
                    {
                        if (checkResponse.Values[i].Count > 0 && checkResponse.Values[i][0].ToString() == dateStr)
                        {
                            if (checkResponse.Values[i].Count > 22 && checkResponse.Values[i][22] != null)
                            {
                                string note = checkResponse.Values[i][22].ToString();
                                string expectedNote = $"T1: {NormalizeLocationName(pickUpLocation)}-{NormalizeLocationName(dropOffLocation)}, {passengerCount} osobe, polazak u {time?.ToString("hh\\:mm")}";
                                string expectedNote2 = $"T2: {NormalizeLocationName(pickUpLocation)}-{NormalizeLocationName(dropOffLocation)}, {passengerCount} osobe, polazak u {time?.ToString("hh\\:mm")}";
                                string expectedNote3 = $"T3: {NormalizeLocationName(pickUpLocation)}-{NormalizeLocationName(dropOffLocation)}, {passengerCount} osobe, polazak u {time?.ToString("hh\\:mm")}";

                                if (note.Contains(expectedNote) || note.Contains(expectedNote2) || note.Contains(expectedNote3))
                                {
                                    Console.WriteLine($"Zapis već postoji u Sheetu za {pickUpLocation}-{dropOffLocation} na datum {dateStr}");
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u CheckExistingTransfer: {ex.Message}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task UpdateGoogleSheet(DateTime date, string? pickUpLocation, string? dropOffLocation, int passengerCount, TimeSpan? time, string shortName, string? boatName = null, bool skipperRequired = false)
        {
            if (!_isInitialized || sheetsService == null)
            {
                Console.WriteLine("GoogleSheetsService not initialized, skipping UpdateGoogleSheet");
                return;
            }

            try
            {
                string dateStr = date.ToString("d.M.", CultureInfo.InvariantCulture);
                Console.WriteLine($"Pokušaj upisa za datum: {dateStr}");

                string range = "2025!A2:A";
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
                            if (DateTime.TryParseExact(sheetDate,
                                new[] { "d.M.", "d.M.yyyy", "dd.MM.yyyy", "dd-MM-yyyy", "dd/MM/yyyy" },
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out DateTime parsedSheetDate))
                            {
                                if (parsedSheetDate.Date == date.Date)
                                {
                                    rowIndex = i + 2;
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

                int columnIndex;
                if (boatName != null)
                {
                    // Update header range for boats
                    string headerRange = "2025!C1:U1"; // was C1:T1
                    var headerRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, headerRange);
                    var headerResponse = await headerRequest.ExecuteAsync();
                    columnIndex = -1;

                    if (headerResponse.Values != null && headerResponse.Values.Count > 0)
                    {
                        for (int i = 0; i < headerResponse.Values[0].Count; i++)
                        {
                            if (headerResponse.Values[0][i].ToString().Equals(boatName, StringComparison.OrdinalIgnoreCase))
                            {
                                columnIndex = i + 3; // Stupac C je indeks 3
                                Console.WriteLine($"Pronađen stupac za gliser: {boatName}, columnIndex: {columnIndex}");
                                break;
                            }
                        }
                    }

                    if (columnIndex == -1)
                    {
                        throw new InvalidOperationException($"Gliser {boatName} nije pronađen u stupcima C-T!");
                    }
                }
                else
                {
                    // Update transfer columns
                    string checkRange = $"2025!V{rowIndex}:X{rowIndex}"; // was U:W
                    var checkRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, checkRange);
                    var checkResponse = await checkRequest.ExecuteAsync();

                    columnIndex = -1;
                    if (checkResponse.Values == null || checkResponse.Values.Count == 0 || checkResponse.Values[0].Count < 3)
                    {
                        columnIndex = checkResponse.Values == null || checkResponse.Values[0].Count == 0 ? 21 : checkResponse.Values[0].Count + 21; // Početak od U (indeks 21)
                    }
                    else if (checkResponse.Values[0].Count == 3)
                    {
                        throw new InvalidOperationException("Tri transfera već bukirana za ovaj datum!");
                    }
                }

                string columnLetter = GetColumnLetter(columnIndex);
                string shortNameRange = $"2025!{columnLetter}{rowIndex}";
                var shortNameValue = new ValueRange { Values = new List<IList<object>> { new List<object> { shortName } } };
                var shortNameRequest = sheetsService.Spreadsheets.Values.Update(shortNameValue, spreadsheetId, shortNameRange);
                shortNameRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await shortNameRequest.ExecuteAsync();
                Console.WriteLine($"ShortName '{shortName}' upisan u {shortNameRange}");

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
                                    SheetId = SHEET_2025_ID,
                                    StartRowIndex = rowIndex - 1,
                                    EndRowIndex = rowIndex,
                                    StartColumnIndex = columnIndex - 1,
                                    EndColumnIndex = columnIndex
                                },
                                Cell = new CellData
                                {
                                    UserEnteredFormat = new CellFormat
                                    {
                                        BackgroundColor = boatName != null && skipperRequired
                                            ? new Color { Red = 0, Green = 0, Blue = 1 }
                                            : boatName != null
                                                ? new Color { Red = 1, Green = 0, Blue = 0 }
                                                : new Color { Red = 0, Green = 0, Blue = 1 },
                                        TextFormat = new TextFormat { ForegroundColor = new Color { Red = 1, Green = 1, Blue = 1 } }
                                    }
                                },
                                Fields = "userEnteredFormat.backgroundColor,userEnteredFormat.textFormat"
                            }
                        }
                    }
                };
                await sheetsService.Spreadsheets.BatchUpdate(formatRequest, spreadsheetId).ExecuteAsync();
                Console.WriteLine($"Ćelija {shortNameRange} obojana {(boatName != null && skipperRequired ? "modro" : boatName != null ? "crveno" : "modro")} s bijelim tekstom");

                if (boatName == null)
                {
                    // Update notes column
                    string noteRange = $"2025!Y{rowIndex}"; // was X
                    var noteGetRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, noteRange);
                    var noteGetResponse = await noteGetRequest.ExecuteAsync();
                    string existingNote = noteGetResponse.Values != null && noteGetResponse.Values.Count > 0 && noteGetResponse.Values[0].Count > 0
                        ? noteGetResponse.Values[0][0].ToString()
                        : "";
                    Console.WriteLine($"Postojeća napomena u Y: {existingNote}");

                    string transferPrefix = (columnIndex - 22) switch
                    {
                        0 => "T1",
                        1 => "T2",
                        2 => "T3",
                        _ => "T?"
                    };
                    string timeFormatted = time.HasValue ? $"{time.Value.Hours:D2}:{time.Value.Minutes:D2}" : "N/A";
                    string newNote = $"{transferPrefix}: {NormalizeLocationName(pickUpLocation)}-{NormalizeLocationName(dropOffLocation)}, {passengerCount} osobe, polazak u {timeFormatted}";
                    Console.WriteLine($"Nova napomena: {newNote}");

                    string combinedNote = string.IsNullOrEmpty(existingNote) ? newNote : $"{existingNote} / {newNote}";
                    var noteValue = new ValueRange { Values = new List<IList<object>> { new List<object> { combinedNote } } };
                    var noteRequest = sheetsService.Spreadsheets.Values.Update(noteValue, spreadsheetId, noteRange);
                    noteRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                    await noteRequest.ExecuteAsync();
                    Console.WriteLine($"Napomena upisana u {noteRange}: {combinedNote}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u UpdateGoogleSheet: {ex.Message}, InnerException: {ex.InnerException?.Message}");
                throw;
            }
        }

        private string GetColumnLetter(int columnIndex)
        {
            string columnLetter = "";
            while (columnIndex > 0)
            {
                int modulo = (columnIndex - 1) % 26;
                columnLetter = (char)('A' + modulo) + columnLetter;
                columnIndex = (columnIndex - 1) / 26;
            }
            return columnLetter;
        }

        public async Task UpdateEvidenceSheet(TransferBooking booking, string pickUpLocation, string dropOffLocation, string shortName, bool isReTour)
        {
            try
            {
                Console.WriteLine($"UpdateEvidenceSheet: isReTour={isReTour}, DepartureTime={booking.DepartureTime}, ReTourTime={booking.ReTourTime}");

                string range = "Evidencija!A2:A";
                var getRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
                var getResponse = await getRequest.ExecuteAsync();
                int rowIndex = getResponse.Values != null ? getResponse.Values.Count + 2 : 2;
                Console.WriteLine($"Pronađen slobodan red u Evidencija: {rowIndex}");

                string dateStr = isReTour ? booking.ReTourDate?.ToString("d.M.", CultureInfo.InvariantCulture) ?? "N/A" : booking.DepartureDate?.ToString("d.M.", CultureInfo.InvariantCulture) ?? "N/A";
                string timeFormatted = isReTour ? (booking.ReTourTime.HasValue ? $"{booking.ReTourTime.Value.Hours:D2}:{booking.ReTourTime.Value.Minutes:D2}" : "N/A") : (booking.DepartureTime.HasValue ? $"{booking.DepartureTime.Value.Hours:D2}:{booking.DepartureTime.Value.Minutes:D2}" : "N/A");
                string locationStr = isReTour ? $"{NormalizeLocationName(dropOffLocation)}-{NormalizeLocationName(pickUpLocation)}" : $"{NormalizeLocationName(pickUpLocation)}-{NormalizeLocationName(dropOffLocation)}";
                decimal brutto = isReTour ? 0 : booking.TotalPrice;

                var values = new List<object>
                {
                    "Nedovršeno",
                    dateStr,
                    booking.RenterName ?? "N/A",
                    locationStr,
                    booking.PassengerCount,
                    timeFormatted,
                    "",
                    "Da",
                    "Da",
                    "PayPal",
                    brutto,
                    "",
                    70,
                    "",
                    "g+s+PP",
                    "",
                    isReTour ? "" : booking.DepositPaid,
                    "",
                    "",
                    "",
                    isReTour ? "" : (booking.TotalPrice - booking.DepositPaid),
                    DateTime.Now.ToString("d.M.", CultureInfo.InvariantCulture),
                    booking.RenterEmail ?? "N/A",
                    booking.RenterPhone ?? "N/A"
                };

                string updateRange = $"Evidencija!A{rowIndex}:X{rowIndex}";
                var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
                var updateRequest = sheetsService.Spreadsheets.Values.Update(valueRange, spreadsheetId, updateRange);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await updateRequest.ExecuteAsync();
                Console.WriteLine($"Podaci upisani u Evidencija, red {rowIndex}: {string.Join(", ", values)}");

                try
                {
                    var dataValidationRequest = new BatchUpdateSpreadsheetRequest
                    {
                        Requests = new List<Request>
                        {
                            new Request
                            {
                                SetDataValidation = new SetDataValidationRequest
                                {
                                    Range = new GridRange
                                    {
                                        SheetId = EVIDENCIJA_SHEET_ID,
                                        StartRowIndex = 1,
                                        EndRowIndex = null,
                                        StartColumnIndex = 0,
                                        EndColumnIndex = 1
                                    },
                                    Rule = new DataValidationRule
                                    {
                                        Condition = new BooleanCondition
                                        {
                                            Type = "ONE_OF_LIST",
                                            Values = new List<ConditionValue>
                                            {
                                                new ConditionValue { UserEnteredValue = "Nedovršeno" },
                                                new ConditionValue { UserEnteredValue = "OK" },
                                                new ConditionValue { UserEnteredValue = "Conf. poslan" },
                                                new ConditionValue { UserEnteredValue = "Otkazano" }
                                            }
                                        },
                                        Strict = true,
                                        ShowCustomUi = true
                                    }
                                }
                            }
                        }
                    };

                    await sheetsService.Spreadsheets.BatchUpdate(dataValidationRequest, spreadsheetId).ExecuteAsync();
                    Console.WriteLine("Dropdown postavljen za stupac Status u Evidencija.");

                    var conditionalFormatRequest = new BatchUpdateSpreadsheetRequest
                    {
                        Requests = new List<Request>
                        {
                            new Request
                            {
                                AddConditionalFormatRule = new AddConditionalFormatRuleRequest
                                {
                                    Rule = new ConditionalFormatRule
                                    {
                                        Ranges = new List<GridRange>
                                        {
                                            new GridRange
                                            {
                                                SheetId = EVIDENCIJA_SHEET_ID,
                                                StartRowIndex = 1,
                                                StartColumnIndex = 0,
                                                EndColumnIndex = 1
                                            }
                                        },
                                        BooleanRule = new BooleanRule
                                        {
                                            Condition = new BooleanCondition
                                            {
                                                Type = "TEXT_EQ",
                                                Values = new List<ConditionValue> { new ConditionValue { UserEnteredValue = "OK" } }
                                            },
                                            Format = new CellFormat
                                            {
                                                BackgroundColor = new Color { Red = 0f, Green = 1f, Blue = 0f }
                                            }
                                        }
                                    },
                                    Index = 0
                                }
                            },
                            new Request
                            {
                                AddConditionalFormatRule = new AddConditionalFormatRuleRequest
                                {
                                    Rule = new ConditionalFormatRule
                                    {
                                        Ranges = new List<GridRange>
                                        {
                                            new GridRange
                                            {
                                                SheetId = EVIDENCIJA_SHEET_ID,
                                                StartRowIndex = 1,
                                                StartColumnIndex = 0,
                                                EndColumnIndex = 1
                                            }
                                        },
                                        BooleanRule = new BooleanRule
                                        {
                                            Condition = new BooleanCondition
                                            {
                                                Type = "TEXT_EQ",
                                                Values = new List<ConditionValue> { new ConditionValue { UserEnteredValue = "Otkazano" } }
                                            },
                                            Format = new CellFormat
                                            {
                                                BackgroundColor = new Color { Red = 1f, Green = 0f, Blue = 0f }
                                            }
                                        }
                                    },
                                    Index = 0
                                }
                            },
                            new Request
                            {
                                AddConditionalFormatRule = new AddConditionalFormatRuleRequest
                                {
                                    Rule = new ConditionalFormatRule
                                    {
                                        Ranges = new List<GridRange>
                                        {
                                            new GridRange
                                            {
                                                SheetId = EVIDENCIJA_SHEET_ID,
                                                StartRowIndex = 1,
                                                StartColumnIndex = 0,
                                                EndColumnIndex = 1
                                            }
                                        },
                                        BooleanRule = new BooleanRule
                                        {
                                            Condition = new BooleanCondition
                                            {
                                                Type = "TEXT_EQ",
                                                Values = new List<ConditionValue> { new ConditionValue { UserEnteredValue = "Nedovršeno" } }
                                            },
                                            Format = new CellFormat
                                            {
                                                BackgroundColor = new Color { Red = 1f, Green = 0.7529f, Blue = 0.7961f }
                                            }
                                        }
                                    },
                                    Index = 0
                                }
                            },
                            new Request
                            {
                                AddConditionalFormatRule = new AddConditionalFormatRuleRequest
                                {
                                    Rule = new ConditionalFormatRule
                                    {
                                        Ranges = new List<GridRange>
                                        {
                                            new GridRange
                                            {
                                                SheetId = EVIDENCIJA_SHEET_ID,
                                                StartRowIndex = 1,
                                                StartColumnIndex = 0,
                                                EndColumnIndex = 1
                                            }
                                        },
                                        BooleanRule = new BooleanRule
                                        {
                                            Condition = new BooleanCondition
                                            {
                                                Type = "TEXT_EQ",
                                                Values = new List<ConditionValue> { new ConditionValue { UserEnteredValue = "Conf. poslan" } }
                                            },
                                            Format = new CellFormat
                                            {
                                                BackgroundColor = new Color { Red = 0.5451f, Green = 0.2706f, Blue = 0.0745f }
                                            }
                                        }
                                    },
                                    Index = 0
                                }
                            }
                        }
                    };

                    await sheetsService.Spreadsheets.BatchUpdate(conditionalFormatRequest, spreadsheetId).ExecuteAsync();
                    Console.WriteLine("Uvjetno formatiranje postavljeno za stupac Status u Evidencija.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Upozorenje: Neuspješno postavljanje dropdowna/formatiranja u Evidenciji: {ex.Message}, InnerException: {ex.InnerException?.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u UpdateEvidenceSheet: {ex.Message}, StackTrace: {ex.StackTrace}, InnerException: {ex.InnerException?.Message}");
                throw;
            }
        }

        public async Task UpdateEvidenceSheet(BoatBooking booking, string pickUpLocation, string dropOffLocation, string shortName, DateTime date, IEnumerable<Extra> selectedExtras, bool isFirstDay)
        {
            try
            {
                string range = "Evidencija!A2:A";
                var getRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
                var getResponse = await getRequest.ExecuteAsync();
                int rowIndex = getResponse.Values != null ? getResponse.Values.Count + 2 : 2;
                Console.WriteLine($"Pronađen slobodan red u Evidencija: {rowIndex}");

                string dateStr = date.ToString("d.M.", CultureInfo.InvariantCulture);
                string timeFormatted = booking.PickupTime.HasValue ? $"{booking.PickupTime.Value.Hours:D2}:{booking.PickupTime.Value.Minutes:D2}" : "N/A";
                string extrasNote = selectedExtras.Any() ? string.Join(", ", selectedExtras.Select(e => e.Name)) : "";
                string opisTroskova = booking.FuelIncluded && booking.SkipperRequired ? "g+s"
                    : booking.FuelIncluded ? "g"
                    : booking.SkipperRequired ? "s"
                    : "";

                var values = new List<object>
                {
                    "Nedovršeno",
                    dateStr,
                    booking.RenterName ?? "N/A",
                    booking.BoatName ?? "N/A",
                    booking.PassengerCount,
                    timeFormatted,
                    extrasNote,
                    booking.FuelIncluded ? "Da" : "Ne",
                    booking.SkipperRequired ? "Da" : "Ne",
                    "",
                    isFirstDay ? booking.TotalPrice : 0,
                    "",
                    booking.SkipperRequired ? 70 : "",
                    "",
                    opisTroskova,
                    "",
                    isFirstDay ? booking.DepositPaid : "",
                    "",
                    "",
                    "",
                    isFirstDay ? (booking.TotalPrice - booking.DepositPaid) : "",
                    DateTime.Now.ToString("d.M.", CultureInfo.InvariantCulture),
                    booking.RenterEmail ?? "N/A",
                    booking.RenterPhone ?? "N/A"
                };

                string updateRange = $"Evidencija!A{rowIndex}:X{rowIndex}";
                var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
                var updateRequest = sheetsService.Spreadsheets.Values.Update(valueRange, spreadsheetId, updateRange);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await updateRequest.ExecuteAsync();

                Console.WriteLine($"Podaci upisani u Evidencija, red {rowIndex}: {string.Join(", ", values)}");

                try
                {
                    var dataValidationRequest = new BatchUpdateSpreadsheetRequest
                    {
                        Requests = new List<Request>
                        {
                            new Request
                            {
                                SetDataValidation = new SetDataValidationRequest
                                {
                                    Range = new GridRange
                                    {
                                        SheetId = EVIDENCIJA_SHEET_ID,
                                        StartRowIndex = 1,
                                        EndRowIndex = null,
                                        StartColumnIndex = 0,
                                        EndColumnIndex = 1
                                    },
                                    Rule = new DataValidationRule
                                    {
                                        Condition = new BooleanCondition
                                        {
                                            Type = "ONE_OF_LIST",
                                            Values = new List<ConditionValue>
                                            {
                                                new ConditionValue { UserEnteredValue = "Nedovršeno" },
                                                new ConditionValue { UserEnteredValue = "OK" },
                                                new ConditionValue { UserEnteredValue = "Conf. poslan" },
                                                new ConditionValue { UserEnteredValue = "Otkazano" }
                                            }
                                        },
                                        Strict = true,
                                        ShowCustomUi = true
                                    }
                                }
                            },
                            new Request
                            {
                                SetDataValidation = new SetDataValidationRequest
                                {
                                    Range = new GridRange
                                    {
                                        SheetId = EVIDENCIJA_SHEET_ID,
                                        StartRowIndex = 1,
                                        EndRowIndex = null,
                                        StartColumnIndex = 14,
                                        EndColumnIndex = 15
                                    },
                                    Rule = new DataValidationRule
                                    {
                                        Condition = new BooleanCondition
                                        {
                                            Type = "ONE_OF_LIST",
                                            Values = new List<ConditionValue>
                                            {
                                                new ConditionValue { UserEnteredValue = "g" },
                                                new ConditionValue { UserEnteredValue = "g+s" },
                                                new ConditionValue { UserEnteredValue = "s" },
                                                new ConditionValue { UserEnteredValue = "" }
                                            }
                                        },
                                        Strict = true,
                                        ShowCustomUi = true
                                    }
                                }
                            }
                        }
                    };

                    await sheetsService.Spreadsheets.BatchUpdate(dataValidationRequest, spreadsheetId).ExecuteAsync();
                    Console.WriteLine("Dropdown postavljen za stupce Status i Opis_Troškova u Evidencija.");

                    var conditionalFormatRequest = new BatchUpdateSpreadsheetRequest
                    {
                        Requests = new List<Request>
                        {
                            new Request
                            {
                                AddConditionalFormatRule = new AddConditionalFormatRuleRequest
                                {
                                    Rule = new ConditionalFormatRule
                                    {
                                        Ranges = new List<GridRange>
                                        {
                                            new GridRange
                                            {
                                                SheetId = EVIDENCIJA_SHEET_ID,
                                                StartRowIndex = 1,
                                                StartColumnIndex = 0,
                                                EndColumnIndex = 1
                                            }
                                        },
                                        BooleanRule = new BooleanRule
                                        {
                                            Condition = new BooleanCondition
                                            {
                                                Type = "TEXT_EQ",
                                                Values = new List<ConditionValue> { new ConditionValue { UserEnteredValue = "OK" } }
                                            },
                                            Format = new CellFormat
                                            {
                                                BackgroundColor = new Color { Red = 0f, Green = 1f, Blue = 0f }
                                            }
                                        }
                                    },
                                    Index = 0
                                }
                            },
                            new Request
                            {
                                AddConditionalFormatRule = new AddConditionalFormatRuleRequest
                                {
                                    Rule = new ConditionalFormatRule
                                    {
                                        Ranges = new List<GridRange>
                                        {
                                            new GridRange
                                            {
                                                SheetId = EVIDENCIJA_SHEET_ID,
                                                StartRowIndex = 1,
                                                StartColumnIndex = 0,
                                                EndColumnIndex = 1
                                            }
                                        },
                                        BooleanRule = new BooleanRule
                                        {
                                            Condition = new BooleanCondition
                                            {
                                                Type = "TEXT_EQ",
                                                Values = new List<ConditionValue> { new ConditionValue { UserEnteredValue = "Otkazano" } }
                                            },
                                            Format = new CellFormat
                                            {
                                                BackgroundColor = new Color { Red = 1f, Green = 0f, Blue = 0f }
                                            }
                                        }
                                    },
                                    Index = 0
                                }
                            },
                            new Request
                            {
                                AddConditionalFormatRule = new AddConditionalFormatRuleRequest
                                {
                                    Rule = new ConditionalFormatRule
                                    {
                                        Ranges = new List<GridRange>
                                        {
                                            new GridRange
                                            {
                                                SheetId = EVIDENCIJA_SHEET_ID,
                                                StartRowIndex = 1,
                                                StartColumnIndex = 0,
                                                EndColumnIndex = 1
                                            }
                                        },
                                        BooleanRule = new BooleanRule
                                        {
                                            Condition = new BooleanCondition
                                            {
                                                Type = "TEXT_EQ",
                                                Values = new List<ConditionValue> { new ConditionValue { UserEnteredValue = "Nedovršeno" } }
                                            },
                                            Format = new CellFormat
                                            {
                                                BackgroundColor = new Color { Red = 1f, Green = 0.7529f, Blue = 0.7961f }
                                            }
                                        }
                                    },
                                    Index = 0
                                }
                            },
                            new Request
                            {
                                AddConditionalFormatRule = new AddConditionalFormatRuleRequest
                                {
                                    Rule = new ConditionalFormatRule
                                    {
                                        Ranges = new List<GridRange>
                                        {
                                            new GridRange
                                            {
                                                SheetId = EVIDENCIJA_SHEET_ID,
                                                StartRowIndex = 1,
                                                StartColumnIndex = 0,
                                                EndColumnIndex = 1
                                            }
                                        },
                                        BooleanRule = new BooleanRule
                                        {
                                            Condition = new BooleanCondition
                                            {
                                                Type = "TEXT_EQ",
                                                Values = new List<ConditionValue> { new ConditionValue { UserEnteredValue = "Conf. poslan" } }
                                            },
                                            Format = new CellFormat
                                            {
                                                BackgroundColor = new Color { Red = 0.5451f, Green = 0.2706f, Blue = 0.0745f }
                                            }
                                        }
                                    },
                                    Index = 0
                                }
                            }
                        }
                    };

                    await sheetsService.Spreadsheets.BatchUpdate(conditionalFormatRequest, spreadsheetId).ExecuteAsync();
                    Console.WriteLine("Uvjetno formatiranje postavljeno za stupac Status u Evidencija.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Upozorenje: Neuspješno postavljanje dropdowna/formatiranja u Evidenciji: {ex.Message}, InnerException: {ex.InnerException?.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u UpdateEvidenceSheet: {ex.Message}, StackTrace: {ex.StackTrace}, InnerException: {ex.InnerException?.Message}");
                throw;
            }
        }

        public async Task<List<ConsistencyReport>> GetBookingsFromSheet2025(DateTime date)
        {
            try
            {
                var result = new List<ConsistencyReport>();
                string dateStr = date.ToString("d.M.", CultureInfo.InvariantCulture);
                Console.WriteLine($"Dohvaćanje bukiranja iz Sheet 2025 za datum: {dateStr}");

                using var dbContext = dbContextFactory.CreateDbContext();
                var transferBookings = await dbContext.TransferBookings
                    .Where(t =>
                        (t.DepartureDate.HasValue &&
                         t.DepartureDate.Value.Year == date.Year &&
                         t.DepartureDate.Value.Month == date.Month &&
                         t.DepartureDate.Value.Day == date.Day)
                        ||
                        (t.WithReTour && t.ReTourDate.HasValue &&
                         t.ReTourDate.Value.Year == date.Year &&
                         t.ReTourDate.Value.Month == date.Month &&
                         t.ReTourDate.Value.Day == date.Day))
                    .Select(t => new
                    {
                        t.Id,
                        t.DepartureLocationId,
                        t.CustomDepartureLocationName,
                        t.ArrivalLocationId,
                        t.CustomArrivalLocationName,
                        t.DepartureDate,
                        t.ReTourDate,
                        t.WithReTour
                    })
                    .ToListAsync();

                var boatBookings = await dbContext.BoatBookings
                    .Where(b => b.StartDate.HasValue &&
                                b.StartDate.Value.Year == date.Year &&
                                b.StartDate.Value.Month == date.Month &&
                                b.StartDate.Value.Day == date.Day)
                    .Select(b => new
                    {
                        b.Id,
                        b.BoatName
                    })
                    .ToListAsync();

                var locationList = await dbContext.Locations.AsNoTracking().ToListAsync();

                string range = "2025!A2:X"; // Ažurirano na X zbog pomaka
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
                            if (DateTime.TryParseExact(sheetDate,
                                new[] { "d.M.", "d.M.yyyy", "dd.MM.yyyy", "dd-MM-yyyy", "dd/MM/yyyy" },
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out DateTime parsedSheetDate))
                            {
                                if (parsedSheetDate.Date == date.Date)
                                {
                                    rowIndex = i + 2;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (rowIndex == -1)
                {
                    Console.WriteLine($"Nema zapisa za datum {dateStr} u Sheet 2025.");
                    return result;
                }

                string dataRange = $"2025!A{rowIndex}:X{rowIndex}"; // Ažurirano na X
                var dataRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, dataRange);
                var dataResponse = await dataRequest.ExecuteAsync();

                if (dataResponse.Values != null && dataResponse.Values.Count > 0)
                {
                    var row = dataResponse.Values[0];
                    Console.WriteLine($"Redak {rowIndex} ima {row.Count} stupaca.");

                    string headerRange = "2025!C1:T1"; // Ažurirano na C1:T1
                    var headerRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, headerRange);
                    var headerResponse = await headerRequest.ExecuteAsync();
                    if (headerResponse.Values != null && headerResponse.Values.Count > 0)
                    {
                        var headers = headerResponse.Values[0];
                        for (int i = 0; i < headers.Count; i++)
                        {
                            int columnIndex = i + 2;
                            if (columnIndex < row.Count && row[columnIndex] != null && row[columnIndex].ToString().Trim() == "ID")
                            {
                                string boatName = headers[i].ToString();
                                int bookingId = boatBookings.FirstOrDefault(b => NormalizeLocationName(b.BoatName).Equals(NormalizeLocationName(boatName), StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
                                result.Add(new ConsistencyReport
                                {
                                    BookingId = bookingId,
                                    Type = "Boat",
                                    BoatName = boatName,
                                    Details = NormalizeLocationName(boatName)
                                });
                                Console.WriteLine($"Dodan gliser: BookingId={bookingId}, BoatName={boatName}, Normalized={NormalizeLocationName(boatName)}");
                            }
                        }
                    }

                    for (int i = 20; i <= 22; i++) // Ažurirano na 20-22 (U-W)
                    {
                        if (i < row.Count && row[i] != null && row[i].ToString().Trim() == "ID")
                        {
                            string locations = (row.Count > 23 && row[23] != null) ? row[23].ToString() : "Unknown"; // Ažurirano na 23 (X)
                            string normalizedDetails = NormalizeTransferDetails(locations);

                            int bookingId = 0;
                            foreach (var booking in transferBookings)
                            {
                                string departureName = booking.DepartureLocationId.HasValue
                                    ? NormalizeLocationName(locationList.FirstOrDefault(l => l.Id == booking.DepartureLocationId)?.Name ?? $"Unknown_Location_{booking.DepartureLocationId}")
                                    : NormalizeLocationName(booking.CustomDepartureLocationName ?? "Unknown_Custom");
                                string arrivalName = booking.ArrivalLocationId.HasValue
                                    ? NormalizeLocationName(locationList.FirstOrDefault(l => l.Id == booking.ArrivalLocationId)?.Name ?? $"Unknown_Location_{booking.ArrivalLocationId}")
                                    : NormalizeLocationName(booking.CustomArrivalLocationName ?? "Unknown_Custom");

                                string tourDetails = $"{departureName}-{arrivalName}";
                                string retourDetails = $"{arrivalName}-{departureName}";

                                bool isRetour = booking.WithReTour && booking.ReTourDate.HasValue && booking.ReTourDate.Value.Date == date.Date;
                                string expectedDetails = isRetour ? retourDetails : tourDetails;

                                Console.WriteLine($"Usporedba transfera: SheetDetails={normalizedDetails}, ExpectedDetails={expectedDetails}, TourDetails={tourDetails}, RetourDetails={retourDetails}, IsRetour={isRetour}");

                                if (normalizedDetails == expectedDetails)
                                {
                                    bookingId = booking.Id;
                                    break;
                                }
                            }

                            result.Add(new ConsistencyReport
                            {
                                BookingId = bookingId,
                                Type = "Transfer",
                                Locations = normalizedDetails,
                                Details = normalizedDetails
                            });
                            Console.WriteLine($"Dodan transfer: BookingId={bookingId}, Locations={normalizedDetails}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Nema podataka za redak {rowIndex} u Sheet 2025.");
                }

                Console.WriteLine($"Pronađeno {result.Count} bukiranja u Sheet 2025 za {dateStr}.");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u GetBookingsFromSheet2025: {ex.Message}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<List<ConsistencyReport>> GetBookingsFromEvidencija(DateTime date)
        {
            try
            {
                var result = new List<ConsistencyReport>();
                string dateStr = date.ToString("d.M.", CultureInfo.InvariantCulture);
                Console.WriteLine($"Dohvaćanje bukiranja iz Evidencija za datum: {dateStr}");

                using var dbContext = dbContextFactory.CreateDbContext();
                var transferBookings = await dbContext.TransferBookings
                    .Where(t =>
                        (t.DepartureDate.HasValue &&
                         t.DepartureDate.Value.Year == date.Year &&
                         t.DepartureDate.Value.Month == date.Month &&
                         t.DepartureDate.Value.Day == date.Day)
                        ||
                        (t.WithReTour && t.ReTourDate.HasValue &&
                         t.ReTourDate.Value.Year == date.Year &&
                         t.ReTourDate.Value.Month == date.Month &&
                         t.ReTourDate.Value.Day == date.Day))
                    .Select(t => new
                    {
                        t.Id,
                        t.DepartureLocationId,
                        t.CustomDepartureLocationName,
                        t.ArrivalLocationId,
                        t.CustomArrivalLocationName,
                        t.DepartureDate,
                        t.ReTourDate,
                        t.WithReTour
                    })
                    .ToListAsync();

                var boatBookings = await dbContext.BoatBookings
                    .Where(b => b.StartDate.HasValue &&
                                b.StartDate.Value.Year == date.Year &&
                                b.StartDate.Value.Month == date.Month &&
                                b.StartDate.Value.Day == date.Day)
                    .Select(b => new
                    {
                        b.Id,
                        b.BoatName
                    })
                    .ToListAsync();

                var locationList = await dbContext.Locations.AsNoTracking().ToListAsync();

                string range = "Evidencija!A2:D";
                var getRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
                var getResponse = await getRequest.ExecuteAsync();

                if (getResponse.Values != null)
                {
                    foreach (var row in getResponse.Values)
                    {
                        if (row.Count >= 4)
                        {
                            string sheetDate = row[1].ToString();
                            if (DateTime.TryParseExact(sheetDate,
                                new[] { "d.M.", "d.M.yyyy", "dd.MM.yyyy", "dd-MM-yyyy", "dd/MM/yyyy" },
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out DateTime parsedSheetDate))
                            {
                                if (parsedSheetDate.Date == date.Date)
                                {
                                    string status = row[0].ToString();
                                    if (status != "Conf. poslan" && status != "OK")
                                    {
                                        Console.WriteLine($"Preskočen zapis u Evidencija jer status nije 'Conf. poslan' ili 'OK': {status}");
                                        continue;
                                    }

                                    string type = row[3].ToString().Contains("-") ? "Transfer" : "Boat";
                                    string details = NormalizeLocationName(row[3].ToString());

                                    if (type == "Boat")
                                    {
                                        int bookingId = boatBookings.FirstOrDefault(b => NormalizeLocationName(b.BoatName).Equals(details, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
                                        result.Add(new ConsistencyReport
                                        {
                                            BookingId = bookingId,
                                            Type = type,
                                            BoatName = details,
                                            Details = details
                                        });
                                        Console.WriteLine($"Dodan zapis iz Evidencija: BookingId={bookingId}, Type={type}, Details={details}");
                                    }
                                    else
                                    {
                                        int bookingId = 0;
                                        foreach (var booking in transferBookings)
                                        {
                                            string departureName = booking.DepartureLocationId.HasValue
                                                ? NormalizeLocationName(locationList.FirstOrDefault(l => l.Id == booking.DepartureLocationId)?.Name ?? $"Unknown_Location_{booking.DepartureLocationId}")
                                                : NormalizeLocationName(booking.CustomDepartureLocationName ?? "Unknown_Custom");
                                            string arrivalName = booking.ArrivalLocationId.HasValue
                                                ? NormalizeLocationName(locationList.FirstOrDefault(l => l.Id == booking.ArrivalLocationId)?.Name ?? $"Unknown_Location_{booking.ArrivalLocationId}")
                                                : NormalizeLocationName(booking.CustomArrivalLocationName ?? "Unknown_Custom");

                                            string tourDetails = $"{departureName}-{arrivalName}";
                                            string retourDetails = $"{arrivalName}-{departureName}";

                                            bool isRetour = booking.WithReTour && booking.ReTourDate.HasValue && booking.ReTourDate.Value.Date == date.Date;
                                            string expectedDetails = isRetour ? retourDetails : tourDetails;

                                            Console.WriteLine($"Usporedba Evidencija transfera: SheetDetails={details}, ExpectedDetails={expectedDetails}, TourDetails={tourDetails}, RetourDetails={retourDetails}, IsRetour={isRetour}");

                                            if (details == expectedDetails)
                                            {
                                                bookingId = booking.Id;
                                                break;
                                            }
                                        }

                                        result.Add(new ConsistencyReport
                                        {
                                            BookingId = bookingId,
                                            Type = type,
                                            Locations = details,
                                            Details = details
                                        });
                                        Console.WriteLine($"Dodan zapis iz Evidencija: BookingId={bookingId}, Type={type}, Details={details}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Preskočen redak u Evidencija jer ima samo {row.Count} stupaca.");
                        }
                    }
                }

                Console.WriteLine($"Pronađeno {result.Count} bukiranja u Evidencija za {dateStr}.");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška u GetBookingsFromEvidencija: {ex.Message}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }
    }
}