using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HRDB.Models;

namespace HRDB.Data;

/// <summary>
/// Контекст кадровой базы Sklad_v1_HR: ASP.NET Identity + существующая
/// таблица сотрудников.
/// </summary>
public class HRContext : IdentityDbContext<ApplicationUser>
{
    public HRContext(DbContextOptions<HRContext> options) : base(options)
    {
    }

    public DbSet<Сотрудник> Сотрудники => Set<Сотрудник>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseCollation("Cyrillic_General_CI_AS");

        builder.Entity<Сотрудник>(entity =>
        {
            // Таблица уже создана скриптами db/sklad_v1_hr, EF её не создаёт.
            entity.ToTable("Сотрудник", table => table.ExcludeFromMigrations());
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Guid).HasDefaultValueSql("NEWID()");
            entity.HasIndex(e => e.Guid).IsUnique();
            entity.Property(e => e.Имя).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Адрес).HasMaxLength(100);
            entity.Property(e => e.Телефон).HasMaxLength(18);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.Фио).HasMaxLength(200);
            entity.Property(e => e.ДатаСоздания)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            // Учётка без сотрудника невозможна; один сотрудник — одна учётка.
            entity.HasOne(e => e.Сотрудник)
                .WithMany(s => s.ApplicationUsers)
                .HasForeignKey(e => e.СотрудникId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.СотрудникId).IsUnique();
        });
    }
}
