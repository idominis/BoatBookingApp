using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoatBookingApp.Frontend.Shared.Models
{
    public class TransferBooking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int? DepartureLocationId { get; set; }
        public string CustomDepartureLocation { get; set; } = string.Empty;
        public string CustomDepartureLocationName { get; set; } = string.Empty;
        public int? ArrivalLocationId { get; set; }
        public string CustomArrivalLocation { get; set; } = string.Empty;
        public string CustomArrivalLocationName { get; set; } = string.Empty;
        public DateTime? DepartureDate { get; set; }
        public TimeSpan? DepartureTime { get; set; }
        public DateTime? ReTourDate { get; set; }
        public TimeSpan? ReTourTime { get; set; }
        public int PassengerCount { get; set; }
        public bool Luggage { get; set; }
        public bool WithReTour { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal DepositPaid { get; set; }
        public bool FuelIncluded { get; set; }
        public int? BookerId { get; set; }
        public Booker Booker { get; set; }
        public string RenterName { get; set; } = string.Empty;
        public string RenterEmail { get; set; } = string.Empty;
        public string RenterPhone { get; set; } = string.Empty;
        public Location DepartureLocation { get; set; }
        public Location ArrivalLocation { get; set; }
    }
}