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
using BoatBookingApp.Frontend.Shared.Models;

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

		public async Task UpdateEvidenceSheet(TransferBooking booking, string pickUpLocation, string dropOffLocation, string shortName, bool isReTour)
		{
			try
			{
				// Pronalaženje prvog slobodnog reda u listu "Evidencija" (preskačemo naslov u redu 1)
				string range = "Evidencija!A2:A";
				var getRequest = sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
				var getResponse = await getRequest.ExecuteAsync();
				int rowIndex = getResponse.Values != null ? getResponse.Values.Count + 2 : 2;
				Console.WriteLine($"Pronađen slobodan red u Evidencija: {rowIndex}");

				// Priprema podataka za upis (24 stupaca: A-X)
				string dateStr = isReTour ? booking.ReTourDate?.ToString("d.M.", CultureInfo.InvariantCulture) ?? "N/A" : booking.DepartureDate?.ToString("d.M.", CultureInfo.InvariantCulture) ?? "N/A";
				string timeFormatted = isReTour ? (booking.ReTourTime.HasValue ? $"{booking.ReTourTime.Value.Hours:D2}:{booking.ReTourTime.Value.Minutes:D2}" : "N/A") : (booking.DepartureTime.HasValue ? $"{booking.DepartureTime.Value.Hours:D2}:{booking.DepartureTime.Value.Minutes:D2}" : "N/A");
				string locationStr = isReTour ? $"{dropOffLocation}-{pickUpLocation}" : $"{pickUpLocation}-{dropOffLocation}";
				decimal brutto = isReTour ? 0 : booking.TotalPrice;

				var values = new List<object>
{
"Nedovršeno", // Status
dateStr, // Datum
booking.RenterName ?? "N/A", // Ime_Gosta
locationStr, // Gliser_ili_Transfer
booking.PassengerCount, // Broj_Putnika
timeFormatted, // Vrijeme_Polaska
"", // Napomena
"Da", // Gorivo
"Da", // Skiper
"PayPal", // Kanal_Prodaje
brutto, // Bruto(€)
"", // Trošak_Goriva(€)
70, // Trošak_Skipera(€)
"", // Utrošak_Goriva(Lit)
"g+s+PP", // Opis_Troškova
"", // Neto(€)
isReTour ? "" : booking.DepositPaid, // PayPal_Uplata(€)
"", // Paypal_Fee(€)
"", // IBAN(€)
"", // Cash(€)
isReTour ? "" : (booking.TotalPrice - booking.DepositPaid), // Preostalo_za_Naplatu(€)
DateTime.Now.ToString("d.M.", CultureInfo.InvariantCulture), // Datum_Bukinga
booking.RenterEmail ?? "N/A", // Email
booking.RenterPhone ?? "N/A" // Mobitel
};

				// Upis podataka u raspon A-X za slobodan red
				string updateRange = $"Evidencija!A{rowIndex}:X{rowIndex}";
				var valueRange = new ValueRange { Values = new List<IList<object>> { values } };
				var updateRequest = sheetsService.Spreadsheets.Values.Update(valueRange, spreadsheetId, updateRange);
				updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
				await updateRequest.ExecuteAsync();

				Console.WriteLine($"Podaci upisani u Evidencija, red {rowIndex}: {string.Join(", ", values)}");

				// Postavljanje dropdowna za stupac Status (A) od reda 2 nadalje
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
SheetId = 1240119258, // gid za list "Evidencija"
StartRowIndex = 1, // Počinjemo od reda 2 (indeks 1 jer je 0-based)
EndRowIndex = null, // Cijeli stupac
StartColumnIndex = 0, // Stupac A
EndColumnIndex = 1 // Do stupca A (uključivo)
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
Strict = true, // Samo dopuštene vrijednosti
ShowCustomUi = true // Prikaz dropdowna u sučelju
}
}
}
}
				};

				await sheetsService.Spreadsheets.BatchUpdate(dataValidationRequest, spreadsheetId).ExecuteAsync();
				Console.WriteLine("Dropdown postavljen za stupac Status u Evidencija.");

				// Postavljanje uvjetnog formatiranja za stupac Status (A) od reda 2 nadalje
				var conditionalFormatRequest = new BatchUpdateSpreadsheetRequest
				{
					Requests = new List<Request>
{
// Pravilo za OK (zeleno)
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
SheetId = 1240119258,
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
BackgroundColor = new Color { Red = 0f, Green = 1f, Blue = 0f } // Zeleno
}
}
},
Index = 0
}
},
// Pravilo za Otkazano (crveno)
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
SheetId = 1240119258,
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
BackgroundColor = new Color { Red = 1f, Green = 0f, Blue = 0f } // Crveno
}
}
},
Index = 0
}
},
// Pravilo za Nedovršeno (rozo)
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
SheetId = 1240119258,
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
BackgroundColor = new Color { Red = 1f, Green = 0.7529f, Blue = 0.7961f } // Rozo (RGB: 255, 192, 203)
}
}
},
Index = 0
}
},
// Pravilo za Conf. poslan (smeđe)
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
SheetId = 1240119258,
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
BackgroundColor = new Color { Red = 0.5451f, Green = 0.2706f, Blue = 0.0745f } // Smeđe (RGB: 139, 69, 19)
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
				Console.WriteLine($"Greška u UpdateEvidenceSheet: {ex.Message}, InnerException: {ex.InnerException?.Message}");
				throw;
			}
		}
	}
}
