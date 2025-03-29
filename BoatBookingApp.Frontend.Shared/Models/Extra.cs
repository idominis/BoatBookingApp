using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatBookingApp.Frontend.Shared.Models
{
    public class Extra
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Navigacijska osobina
        public ICollection<BookingExtra> BookingExtras { get; set; } = new List<BookingExtra>();
    }
}