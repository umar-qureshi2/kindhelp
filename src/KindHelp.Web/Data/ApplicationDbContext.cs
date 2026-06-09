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

        b.Entity<Contribution>(e =>
        {
            e.Property(c => c.Amount).HasColumnType("numeric(18,2)");
            e.HasIndex(c => new { c.CaseId, c.ReceivedAtUtc });
            e.HasIndex(c => c.DonorUserId);

            e.HasOne(c => c.Case)
                .WithMany(c2 => c2.Contributions)
                .HasForeignKey(c => c.CaseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.Donor)
                .WithMany(u => u.Contributions)
                .HasForeignKey(c => c.DonorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
