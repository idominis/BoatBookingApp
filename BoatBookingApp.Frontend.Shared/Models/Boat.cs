using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatBookingApp.Frontend.Shared.Models
{
    public class Boat
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? Capacity { get; set; } // Nullable kapacitet
        public decimal? BasicPrice { get; set; } // Nullable cijena
        public double? Length { get; set; } // Nullable duljina
        public int? HorsePower { get; set; } // Nullable snaga motora
    }
}
