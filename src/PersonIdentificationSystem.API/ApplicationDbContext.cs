using Microsoft.EntityFrameworkCore;
using PersonIdentificationSystem.API.Models.Entities;

namespace PersonIdentificationSystem.API;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Person> Persons => Set<Person>();
    public DbSet<PersonPhoto> PersonPhotos => Set<PersonPhoto>();
    public DbSet<RTSPStream> RTSPStreams => Set<RTSPStream>();
    public DbSet<Detection> Detections => Set<Detection>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Person ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Person>(e =>
        {
            e.ToTable("persons");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(255);
            e.Property(x => x.RiskLevel).HasMaxLength(20).HasDefaultValue("Medium");
            e.Property(x => x.DateAdded).HasDefaultValueSql("NOW()");
            e.Property(x => x.DateUpdated).HasDefaultValueSql("NOW()");
        });

        // ── PersonPhoto ─────────────────────────────────────────────────────
        modelBuilder.Entity<PersonPhoto>(e =>
        {
            e.ToTable("person_photos");
            e.HasKey(x => x.Id);
            e.Property(x => x.PhotoUrl).IsRequired().HasMaxLength(500);
            e.Property(x => x.QualityScore).HasPrecision(4, 3);
            e.HasOne(x => x.Person)
             .WithMany(x => x.Photos)
             .HasForeignKey(x => x.PersonId)
             .OnDelete(DeleteBehavior.Cascade);
            // Partial unique index for primary photo handled in DB
        });

        // ── RTSPStream ──────────────────────────────────────────────────────
        modelBuilder.Entity<RTSPStream>(e =>
        {
            e.ToTable("rtsp_streams");
            e.HasKey(x => x.Id);
            e.Property(x => x.CameraName).IsRequired().HasMaxLength(255);
            e.Property(x => x.RtspUrl).IsRequired().HasMaxLength(1000);
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Unknown");
        });

        // ── Detection ───────────────────────────────────────────────────────
        modelBuilder.Entity<Detection>(e =>
        {
            e.ToTable("detections");
            e.HasKey(x => x.Id);
            e.Property(x => x.ConfidenceScore).HasPrecision(5, 4);
            e.Property(x => x.RawMatchData).HasColumnType("jsonb");
            e.HasOne(x => x.Stream)
             .WithMany(x => x.Detections)
             .HasForeignKey(x => x.StreamId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Person)
             .WithMany(x => x.Detections)
             .HasForeignKey(x => x.PersonId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── NotificationLog ─────────────────────────────────────────────────
        modelBuilder.Entity<NotificationLog>(e =>
        {
            e.ToTable("notification_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.RecipientEmail).IsRequired().HasMaxLength(255);
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Pending");
            e.HasOne(x => x.Detection)
             .WithMany(x => x.NotificationLogs)
             .HasForeignKey(x => x.DetectionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── NotificationSettings ────────────────────────────────────────────
        modelBuilder.Entity<NotificationSettings>(e =>
        {
            e.ToTable("notification_settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.MinimumConfidence).HasPrecision(4, 3);
            e.Property(x => x.RecipientEmails).HasColumnType("text[]");
            e.Property(x => x.NotifyOnRiskLevels).HasColumnType("text[]");
        });
    }
}
