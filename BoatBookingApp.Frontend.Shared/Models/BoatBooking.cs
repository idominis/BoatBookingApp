using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatBookingApp.Frontend.Shared.Models
{
    public class BoatBooking
    {
        public string BoatName { get; set; } = string.Empty;
        public bool IsMultiDay { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PassengerCount { get; set; }
        public TimeSpan? PickupTime { get; set; } = new TimeSpan(9, 30, 0);
        public TimeSpan? ReturnTime { get; set; } = new TimeSpan(18, 30, 0);
        public bool SkipperRequired { get; set; }
        public bool FuelIncluded { get; set; }
        public List<string> Extras { get; set; } = new List<string>();
        public decimal TotalPrice { get; set; }
        public decimal DepositPaid { get; set; }
    }
}
