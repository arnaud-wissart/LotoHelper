using Loto.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loto.Infrastructure;

public class LotoDbContext : DbContext
{
    public LotoDbContext(DbContextOptions<LotoDbContext> options) : base(options)
    {
    }

    public DbSet<Draw> Draws => Set<Draw>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Draw>(entity =>
        {
            entity.ToTable("Draws");
            entity.HasKey(d => d.Id);
            entity.HasIndex(d => d.OfficialDrawId).IsUnique();
            entity.HasIndex(d => new
            {
                d.DrawDate,
                d.Number1,
                d.Number2,
                d.Number3,
                d.Number4,
                d.Number5,
                d.LuckyNumber
            }).IsUnique();

            entity.Property(d => d.DrawDate)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(d => d.OfficialDrawId)
                .HasMaxLength(100);

            entity.Property(d => d.DrawDayName)
                .HasMaxLength(20);

            entity.Property(d => d.Source)
                .HasMaxLength(50);

            entity.Property(d => d.Number1).IsRequired();
            entity.Property(d => d.Number2).IsRequired();
            entity.Property(d => d.Number3).IsRequired();
            entity.Property(d => d.Number4).IsRequired();
            entity.Property(d => d.Number5).IsRequired();

            entity.Property(d => d.LuckyNumber).IsRequired();

            entity.Property(d => d.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'")
                .IsRequired()
                .HasColumnType("timestamp with time zone");
        });
    }
}
