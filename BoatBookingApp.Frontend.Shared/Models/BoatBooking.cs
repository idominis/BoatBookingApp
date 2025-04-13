using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatBookingApp.Frontend.Shared.Models
{
    public class BoatBooking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string BoatName { get; set; } = string.Empty;
        public bool IsMultiDay { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PassengerCount { get; set; }
        public TimeSpan? PickupTime { get; set; } = new TimeSpan(9, 30, 0);
        public TimeSpan? ReturnTime { get; set; } = new TimeSpan(18, 30, 0);
        public bool SkipperRequired { get; set; }
        public bool FuelIncluded { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal DepositPaid { get; set; }     
        public int? BookerId { get; set; }
        public Booker Booker { get; set; }
        public ICollection<BookingExtra> BookingExtras { get; set; } = new List<BookingExtra>();
        public string RenterName { get; set; } = string.Empty;
        public string RenterEmail { get; set; } = string.Empty;
        public string RenterPhone { get; set; } = string.Empty;

        // Nova polja za Departure Location
        public int? DepartureLocationId { get; set; }
        public string? CustomDepartureLocation { get; set; }
        public string? CustomDepartureLocationName { get; set; }
    }
}
