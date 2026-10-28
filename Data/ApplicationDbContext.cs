using CollabSpace.Models;
using Microsoft.EntityFrameworkCore;

namespace CollabSpace.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<RoomNote> RoomNotes => Set<RoomNote>();

    public DbSet<CanvasNote> CanvasNotes => Set<CanvasNote>();

    public DbSet<DrawingStroke> DrawingStrokes => Set<DrawingStroke>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(room => room.Id);

            entity.Property(room => room.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(room => room.AccessCode)
                .HasMaxLength(32)
                .IsRequired();

            entity.HasIndex(room => room.AccessCode)
                .IsUnique();

            entity.HasMany(room => room.DrawingStrokes)
                .WithOne(stroke => stroke.Room)
                .HasForeignKey(stroke => stroke.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(room => room.CanvasNotes)
                .WithOne(note => note.Room)
                .HasForeignKey(note => note.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(room => room.Note)
                .WithOne(note => note.Room)
                .HasForeignKey<RoomNote>(note => note.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoomNote>(entity =>
        {
            entity.HasKey(note => note.Id);

            entity.HasIndex(note => note.RoomId)
                .IsUnique();

            entity.Property(note => note.Content)
                .HasColumnType("longtext")
                .IsRequired();
        });

        modelBuilder.Entity<CanvasNote>(entity =>
        {
            entity.HasKey(note => note.Id);

            entity.Property(note => note.Content)
                .HasColumnType("longtext")
                .IsRequired();

            entity.Property(note => note.Color)
                .HasMaxLength(20)
                .IsRequired();

            entity.HasIndex(note => note.RoomId);

            entity.HasIndex(note => new { note.RoomId, note.Id });
        });

        modelBuilder.Entity<DrawingStroke>(entity =>
        {
            entity.HasKey(stroke => stroke.Id);

            entity.Property(stroke => stroke.StrokeData)
                .HasColumnType("longtext")
                .IsRequired();

            entity.HasIndex(stroke => stroke.RoomId);
        });
    }
}
