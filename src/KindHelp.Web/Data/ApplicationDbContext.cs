using KindHelp.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KindHelp.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseUpdate> CaseUpdates => Set<CaseUpdate>();
    public DbSet<CasePhoto> CasePhotos => Set<CasePhoto>();
    public DbSet<Donor> Donors => Set<Donor>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Contribution> Contributions => Set<Contribution>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Case>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasIndex(c => c.Status);
            e.Property(c => c.GoalAmount).HasColumnType("numeric(18,2)");
        });

        b.Entity<CaseUpdate>(e =>
        {
            e.HasIndex(u => new { u.CaseId, u.PostedAtUtc });
            e.HasOne(u => u.Case)
                .WithMany(c => c.Updates)
                .HasForeignKey(u => u.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CasePhoto>(e =>
        {
            e.HasIndex(p => new { p.CaseId, p.SortOrder });
            e.HasOne(p => p.Case)
                .WithMany(c => c.Photos)
                .HasForeignKey(p => p.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Donor>(e =>
        {
            // Email and PhoneNumber are optional but uniquely identify a donor when present.
            // Partial indexes keep multiple NULLs valid.
            e.HasIndex(d => d.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            e.HasIndex(d => d.PhoneNumber).IsUnique().HasFilter("\"PhoneNumber\" IS NOT NULL");
            e.HasIndex(d => d.ApplicationUserId).IsUnique().HasFilter("\"ApplicationUserId\" IS NOT NULL");
            e.Property(d => d.WalletBalance).HasColumnType("numeric(18,2)");

            // Use PostgreSQL's built-in xmin system column as an optimistic concurrency token.
            // Any UPDATE to a Donor row increments xmin server-side; we declare it as a
            // shadow property mapped to the system column with IsRowVersion(), which is the
            // EF Core 8 idiomatic way (the old UseXminAsConcurrencyToken() was deprecated
            // in favour of this). If two transactions both read balance=100 and try to debit,
            // only the first commits — the second sees DbUpdateConcurrencyException and
            // WalletService retries with a fresh read.
            e.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            // Belt-and-braces format check: any stored phone number must contain only digits
            // (optionally led by '+'). Service-level NormalizePhone enforces this on writes
            // through the API; the check catches accidental direct writes.
            e.ToTable(t => t.HasCheckConstraint(
                "CK_Donors_PhoneNormalized",
                "\"PhoneNumber\" IS NULL OR \"PhoneNumber\" ~ '^\\+?[0-9]+$'"));

            e.HasOne(d => d.ApplicationUser)
                .WithOne(u => u.Donor!)
                .HasForeignKey<Donor>(d => d.ApplicationUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WalletTransaction>(e =>
        {
            e.Property(t => t.Amount).HasColumnType("numeric(18,2)");
            e.Property(t => t.BalanceAfter).HasColumnType("numeric(18,2)");
            e.HasIndex(t => new { t.DonorId, t.OccurredAtUtc });
            e.HasIndex(t => new { t.CaseId, t.OccurredAtUtc });
            e.HasIndex(t => t.CorrectsWalletTransactionId);

            e.HasOne(t => t.Donor)
                .WithMany(d => d.WalletTransactions)
                .HasForeignKey(t => t.DonorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(t => t.Case)
                .WithMany()
                .HasForeignKey(t => t.CaseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(t => t.Contribution)
                .WithOne(c => c.WalletTransaction!)
                .HasForeignKey<WalletTransaction>(t => t.ContributionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Self-referencing FK: corrections point at the original transaction they adjust.
            e.HasOne(t => t.CorrectsWalletTransaction)
                .WithMany()
                .HasForeignKey(t => t.CorrectsWalletTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Contribution>(e =>
        {
            e.Property(c => c.Amount).HasColumnType("numeric(18,2)");
            e.HasIndex(c => new { c.CaseId, c.ReceivedAtUtc });
            e.HasIndex(c => c.DonorId);

            e.HasOne(c => c.Case)
                .WithMany(c2 => c2.Contributions)
                .HasForeignKey(c => c.CaseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.Donor)
                .WithMany(d => d.Contributions)
                .HasForeignKey(c => c.DonorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
