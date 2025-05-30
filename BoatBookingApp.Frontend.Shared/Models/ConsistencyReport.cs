namespace BoatBookingApp.Frontend.Shared.Models
{
    public class ConsistencyReport
    {
        public int BookingId { get; set; }
        public DateTime Date { get; set; }
        public string Type { get; set; } // "Boat" ili "Transfer"
        public string BoatName { get; set; } // Za glisere
        public string Locations { get; set; } // Za transfere
        public string Details { get; set; } // Gliser ili lokacije za prikaz u tablici
        public string Sheet2025Status { get; set; }
        public string EvidencijaStatus { get; set; }
        public string DocumentStatus { get; set; }
        public string DatabaseStatus { get; set; }
        public string Note { get; set; }
    }
}