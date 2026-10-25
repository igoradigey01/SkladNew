using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkladApi.Models;

namespace SkladApi.Data;

/// <summary>
/// Контекст основной базы Sklad_v4.1.1. Аудит включается только для
/// транзакционных таблиц и основных справочников (Группы 1 и 2).
/// </summary>
public class SkladContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public SkladContext(
        DbContextOptions<SkladContext> options,
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    public DbSet<Приход> Приходы => Set<Приход>();
    public DbSet<Расход> Расходы => Set<Расход>();
    public DbSet<Деньги> Деньги => Set<Деньги>();
    public DbSet<НакладнаяРасход> НакладныеРасходы => Set<НакладнаяРасход>();
    public DbSet<Сделка> Сделки => Set<Сделка>();
    public DbSet<Расхождение> Расхождения => Set<Расхождение>();
    public DbSet<Ревизия> Ревизии => Set<Ревизия>();
    public DbSet<Номенклатура> Номенклатуры => Set<Номенклатура>();
    public DbSet<Клиент> Клиенты => Set<Клиент>();
    public DbSet<Поставщик> Поставщики => Set<Поставщик>();
    public DbSet<Склад> Склады => Set<Склад>();

    public override int SaveChanges()
    {
        return SaveChanges(true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(true, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.UseCollation("Cyrillic_General_CI_AS");

        modelBuilder.Entity<Приход>(entity =>
        {
            entity.ToTable("Приход");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Sum).HasPrecision(18, 2);
            entity.Property(e => e.Comment).HasMaxLength(1000);
            ConfigureAudit(entity);
        });

        modelBuilder.Entity<Расход>(entity =>
        {
            entity.ToTable("Расход");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Sum).HasPrecision(18, 2);
            entity.Property(e => e.Comment).HasMaxLength(1000);
            ConfigureAudit(entity);
        });

        modelBuilder.Entity<Деньги>(entity =>
        {
            entity.ToTable("Деньги");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Sum).HasPrecision(18, 2);
            entity.Property(e => e.CounterpartyType).HasDefaultValue(0);
            entity.Property(e => e.PaymentMethod).HasDefaultValue(1);
            entity.Property(e => e.НомерДокумента).HasMaxLength(60);
            entity.Property(e => e.Comment).HasMaxLength(1000);
            ConfigureAudit(entity);
        });

        modelBuilder.Entity<НакладнаяРасход>(entity =>
        {
            entity.ToTable("НакладнаяРасход");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Номер).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Дата).HasColumnType("datetime");
            entity.Property(e => e.Комментарий).HasMaxLength(1000);
            ConfigureAudit(entity);
        });

        modelBuilder.Entity<Сделка>(entity =>
        {
            entity.ToTable("Сделка");
            entity.HasKey(e => e.КодСделки);
            entity.Property(e => e.КодСделки).ValueGeneratedNever();
            entity.Property(e => e.НомерСделки).HasMaxLength(40).ValueGeneratedOnAdd();
            entity.Property(e => e.Дата).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.Комментарий).HasMaxLength(1000);
            entity.Property(e => e.Активна).HasDefaultValue(true);
            entity.HasIndex(e => e.НомерСделки).IsUnique();
            ConfigureAudit(entity);
        });

        modelBuilder.Entity<Расхождение>(entity =>
        {
            entity.ToTable("Расхождение");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.КодРевизии).HasMaxLength(60).IsRequired();
            entity.Property(e => e.AccountingValue).HasPrecision(18, 3);
            entity.Property(e => e.ActualValue).HasPrecision(18, 3);
            entity.Property(e => e.Delta).HasPrecision(19, 3);
            entity.Property(e => e.Comment).HasMaxLength(1000);
            ConfigureAudit(entity);
        });

        modelBuilder.Entity<Ревизия>(entity =>
        {
            entity.ToTable("Ревизия");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(60).ValueGeneratedNever();
            entity.Property(e => e.DateStart).HasColumnType("date");
            entity.Property(e => e.DateEnd).HasColumnType("date");
            entity.Property(e => e.DateCreated).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            ConfigureAudit(entity);
        });

        modelBuilder.Entity<Номенклатура>(entity =>
        {
            entity.ToTable("Номенклатура");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Guid).HasDefaultValueSql("NEWID()");
            entity.HasIndex(e => e.Guid).IsUnique();
            entity.Property(e => e.Код).HasMaxLength(40).IsRequired();
            entity.HasIndex(e => e.Код).IsUnique();
            entity.Property(e => e.Артикул).HasMaxLength(100);
            entity.Property(e => e.Название).HasMaxLength(400).IsRequired();
            entity.Property(e => e.КраткоеНазвание).HasMaxLength(100);
            entity.Property(e => e.ЦенаЗакупочная).HasPrecision(18, 2);
            entity.Property(e => e.ЦенаПродажная).HasPrecision(18, 2);
            entity.Property(e => e.ЦенаОптовая).HasPrecision(18, 2);
            entity.Property(e => e.МинимальныйОстаток).HasPrecision(18, 3);
            entity.Property(e => e.МаксимальныйОстаток).HasPrecision(18, 3);
            entity.Property(e => e.СтранаПроисхождения).HasMaxLength(100);
            entity.Property(e => e.Описание).HasMaxLength(2000);
            entity.Property(e => e.Активен).HasDefaultValue(true);
            ConfigureAudit(entity);
        });

        modelBuilder.Entity<Клиент>(entity =>
        {
            entity.ToTable("Клиент");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Псевдоним).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Фио).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Телефон).HasMaxLength(36).IsRequired();
            entity.Property(e => e.Город).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ПределКредита).HasPrecision(18, 2).HasDefaultValue(10000m);
            entity.Property(e => e.Активен).HasDefaultValue(true);
            ConfigureAudit(entity);
        });

        modelBuilder.Entity<Поставщик>(entity =>
        {
            entity.ToTable("Поставщик");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Название).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Город).HasMaxLength(100);
            entity.Property(e => e.Телефон).HasMaxLength(36);
            ConfigureAudit(entity);
        });

        modelBuilder.Entity<Склад>(entity =>
        {
            entity.ToTable("Склад");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Название).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Псевдоним).HasMaxLength(20).IsRequired();
            ConfigureAudit(entity);
        });
    }

    private static void ConfigureAudit<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : AuditableEntity
    {
        entity.Property(e => e.СоздалId).HasMaxLength(450);
        entity.Property(e => e.ИзменилId).HasMaxLength(450);
        entity.Property(e => e.ДатаСоздания)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(e => e.ДатаИзменения).HasColumnType("datetime2");
    }

    private void ApplyAudit()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userId = user is null ? null : _userManager.GetUserId(user);
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.СоздалId = userId;
                    entry.Entity.ДатаСоздания = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.ИзменилId = userId;
                    entry.Entity.ДатаИзменения = now;
                    break;
            }
        }
    }
}
