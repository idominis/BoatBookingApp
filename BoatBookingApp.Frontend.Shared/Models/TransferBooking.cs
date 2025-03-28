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
        public string DepartureLocation { get; set; }  // Polazna destinacija
        public string ArrivalLocation { get; set; }  // Dolazna destinacija
        public DateTime DepartureDateTime { get; set; }  // Datum i vrijeme polaska
        public TimeSpan PickupTime { get; set; }
        public int PassengerCount { get; set; }  // Broj putnika
        public bool Luggage { get; set; } = true;
        public decimal TotalPrice { get; set; }  // Ukupna cijena
        public decimal DepositPaid { get; set; }  // Plaćeni depozit
    }
}
