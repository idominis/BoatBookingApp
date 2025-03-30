using Microsoft.EntityFrameworkCore;
using BoatBookingApp.Frontend.Shared.Models;

namespace BoatBookingApp.Frontend.Shared.Data
{
    public class BoatBookingContext : DbContext
    {
        public DbSet<Boat> Boats { get; set; }
        public DbSet<Booker> Bookers { get; set; }
        public DbSet<BoatBooking> BoatBookings { get; set; }
        public DbSet<TransferBooking> TransferBookings { get; set; }
        public DbSet<Extra> Extras { get; set; }
        public DbSet<BookingExtra> BookingExtras { get; set; }
        public DbSet<Location> Locations { get; set; } // Dodano

        public BoatBookingContext(DbContextOptions<BoatBookingContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BookingExtra>()
                .HasKey(be => new { be.BookingId, be.ExtraId });

            modelBuilder.Entity<TransferBooking>()
                .HasOne(tb => tb.DepartureLocation)
                .WithMany()
                .HasForeignKey(tb => tb.DepartureLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TransferBooking>()
                .HasOne(tb => tb.ArrivalLocation)
                .WithMany()
                .HasForeignKey(tb => tb.ArrivalLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}