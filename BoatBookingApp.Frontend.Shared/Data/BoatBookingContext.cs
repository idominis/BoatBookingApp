using Microsoft.EntityFrameworkCore;
using BoatBookingApp.Frontend.Shared.Models;

namespace BoatBookingApp.Frontend.Shared.Data
{
    public class BoatBookingContext : DbContext
    {
        public DbSet<Boat> Boats { get; set; }
        public DbSet<Booker> Bookers { get; set; }
        public DbSet<BoatBooking> Bookings { get; set; }
        public DbSet<Extra> Extras { get; set; }
        public DbSet<BookingExtra> BookingExtras { get; set; }

        public BoatBookingContext(DbContextOptions<BoatBookingContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BookingExtra>()
                .HasKey(be => new { be.BookingId, be.ExtraId });

            base.OnModelCreating(modelBuilder);
        }
    }
}

