using JXServer.Domain.Accounts;
using JXServer.Domain.Characters;
using JXServer.Primitives.Accounts;
using JXServer.Primitives.Characters;
using Microsoft.EntityFrameworkCore;

namespace JXServer.Infrastructure.EntityFrameworkCore;

/// <summary>
/// EF Core 10 database context for <see cref="Account"/> and <see cref="Character"/> aggregates.
/// Supports both SQL Server and SQLite providers without schema divergence.
/// </summary>
public sealed class JXServerDbContext : DbContext
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Character> Characters => Set<Character>();

    public JXServerDbContext(DbContextOptions<JXServerDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(builder =>
        {
            builder.ToTable("Accounts");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserName)
                .IsRequired()
                .HasMaxLength(AccountConsts.MaxUserNameByteLength);

            builder.Property(x => x.NormalizedUserName)
                .IsRequired()
                .HasMaxLength(AccountConsts.MaxUserNameByteLength);

            builder.HasIndex(x => x.NormalizedUserName)
                .IsUnique();

            builder.Property(x => x.PasswordHashUpperMd5)
                .IsRequired()
                .HasMaxLength(AccountConsts.PasswordMd5HexLength);

            builder.Property(x => x.SecondaryPasswordHashUpperMd5)
                .HasMaxLength(AccountConsts.PasswordMd5HexLength);

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.RemainingTimeSeconds)
                .IsRequired();

            builder.Property(x => x.CurrentLeaseEpoch)
                .IsRequired();

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.Property(x => x.ConcurrencyStamp)
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<Character>(builder =>
        {
            builder.ToTable("Characters");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.AccountId)
                .IsRequired();

            builder.Property(x => x.SlotIndex)
                .IsRequired();

            builder.HasIndex(x => new { x.AccountId, x.SlotIndex })
                .IsUnique();

            builder.Property(x => x.RoleName)
                .IsRequired()
                .HasMaxLength(CharacterConsts.FixedRoleNameBufferSize);

            builder.Property(x => x.NormalizedRoleName)
                .IsRequired()
                .HasMaxLength(CharacterConsts.FixedRoleNameBufferSize);

            builder.HasIndex(x => x.NormalizedRoleName)
                .IsUnique();

            builder.Property(x => x.ConcurrencyStamp)
                .IsConcurrencyToken();

            builder.OwnsMany(x => x.Skills, skillBuilder =>
            {
                skillBuilder.ToTable("CharacterSkills");
                skillBuilder.WithOwner().HasForeignKey("CharacterId");
                skillBuilder.HasKey("CharacterId", nameof(CharacterSkillValue.SkillId));
                skillBuilder.Property(s => s.SkillId).ValueGeneratedNever();
                skillBuilder.Property(s => s.BaseLevel).IsRequired();
                skillBuilder.Property(s => s.BonusLevel).IsRequired();
                skillBuilder.Property(s => s.ExpPercent).IsRequired();
            });

            builder.Navigation(x => x.Skills)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
