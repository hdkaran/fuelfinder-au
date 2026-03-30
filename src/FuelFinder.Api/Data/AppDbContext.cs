using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FuelFinder.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportFuelType> ReportFuelTypes => Set<ReportFuelType>();
    public DbSet<PushRegistration> PushRegistrations => Set<PushRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Station>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.Brand).HasMaxLength(100).IsRequired();
            e.Property(s => s.Address).HasMaxLength(500).IsRequired();
            e.Property(s => s.Suburb).HasMaxLength(100).IsRequired();
            e.Property(s => s.State).HasMaxLength(3).IsRequired();
            e.HasIndex(s => new { s.Latitude, s.Longitude }); // bounding-box queries
        });

        modelBuilder.Entity<Report>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Status).HasMaxLength(20).IsRequired();
            e.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne(r => r.Station)
             .WithMany(s => s.Reports)
             .HasForeignKey(r => r.StationId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.StationId);
            e.HasIndex(r => r.CreatedAt);
        });

        modelBuilder.Entity<ReportFuelType>(e =>
        {
            e.HasKey(rf => rf.Id);
            e.Property(rf => rf.FuelType).HasMaxLength(20).IsRequired();
            e.HasOne(rf => rf.Report)
             .WithMany(r => r.FuelTypes)
             .HasForeignKey(rf => rf.ReportId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(rf => rf.ReportId);
        });

        modelBuilder.Entity<PushRegistration>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Endpoint).HasMaxLength(2048).IsRequired();
            e.Property(p => p.P256dh).HasMaxLength(200).IsRequired();
            e.Property(p => p.Auth).HasMaxLength(100).IsRequired();
            e.HasIndex(p => p.Endpoint).IsUnique(); // prevent duplicate subscriptions
            e.HasIndex(p => new { p.Latitude, p.Longitude }); // bounding-box queries
        });
    }
}
