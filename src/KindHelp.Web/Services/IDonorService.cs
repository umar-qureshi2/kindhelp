using KindHelp.Web.Models;

namespace KindHelp.Web.Services;

public record DonorListItem(
    int Id,
    string Label,
    string? Email,
    string? PhoneNumber,
    bool IsRegistered,
    decimal WalletBalance,
    int ContributionCount,
    DateTime CreatedAtUtc);

public interface IDonorService
{
    Task<IReadOnlyList<DonorListItem>> ListAsync(string? search = null, CancellationToken ct = default);

    Task<PagedResult<DonorListItem>> ListPagedAsync(string? search, int pageIndex, int pageSize, CancellationToken ct = default);

    Task<Donor?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Find an existing donor by email OR phone (whichever is non-empty). Case-insensitive on email.</summary>
    Task<Donor?> FindByContactAsync(string? email, string? phone, CancellationToken ct = default);

    /// <summary>Ensure a Donor row exists for the given registered ApplicationUser; create if not.</summary>
    Task<Donor> EnsureForUserAsync(ApplicationUser user, CancellationToken ct = default);

    /// <summary>Create an admin-managed donor (no login). At least one of email/phone must be provided.</summary>
    Task<Donor> CreateAdminManagedAsync(string? displayName, string? email, string? phone, string? notes, string createdByUserId, CancellationToken ct = default);

    Task UpdateAsync(int id, string? displayName, string? email, string? phone, string? notes, CancellationToken ct = default);
}
