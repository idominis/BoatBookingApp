using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatBookingApp.Frontend.Shared.Models
{
    public class Booker
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; } // za GSheets
    }
}
