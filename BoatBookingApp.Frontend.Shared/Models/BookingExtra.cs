using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoatBookingApp.Frontend.Shared.Models
{
    public class BookingExtra
    {
        [Key, Column(Order = 0)]
        public int BookingId { get; set; }
        [Key, Column(Order = 1)]
        public int ExtraId { get; set; }

        // Navigacijske osobine (opcionalno za lakše dohvaćanje povezanih podataka)
        public BoatBooking Booking { get; set; }
        public Extra Extra { get; set; }
    }
}

