using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatBookingApp.Frontend.Shared.Models
{
    public class TransferBooking
    {
        public int Id { get; set; }
        public string DepartureLocation { get; set; } = string.Empty;
        public string ArrivalLocation { get; set; } = string.Empty;
        public DateTime? DepartureDate { get; set; }  // Datum i vrijeme polaska
        public TimeSpan? DepartureTime { get; set; }
        public DateTime? ReTourDate { get; set; }  // Datum i vrijeme polaska
        public TimeSpan? ReTourTime { get; set; }
        public int PassengerCount { get; set; }  // Broj putnika
        public bool Luggage { get; set; } = true;
        public bool WithReTour { get; set; } = true;
        public decimal TotalPrice { get; set; }  // Ukupna cijena
        public decimal DepositPaid { get; set; }  // Plaćeni depozit
        public bool FuelIncluded { get; set; }
    }
}
