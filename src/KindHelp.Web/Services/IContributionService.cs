using KindHelp.Web.Models;

namespace KindHelp.Web.Services;

public record DonorCaseSummary(
    int CaseId,
    string Slug,
    string Title,
    CaseStatus Status,
    string? CoverPhotoPath,
    decimal MyTotal,
    int MyContributionCount,
    DateTime LastContributionUtc);

public record DonorContributionRow(
    int Id,
    int CaseId,
    string CaseSlug,
    string CaseTitle,
    decimal Amount,
    DateTime ReceivedAtUtc,
    string? Reference);

public record DonorUpdateRow(
    int CaseId,
    string CaseSlug,
    string CaseTitle,
    int UpdateId,
    string UpdateTitle,
    string UpdateBody,
    DateTime PostedAtUtc);

public interface IContributionService
{
    /// <summary>For donor dashboard — accepts a donorId (NOT an ApplicationUser id) and filters strictly.</summary>
    Task<IReadOnlyList<DonorCaseSummary>> GetMyCaseSummariesAsync(int donorId, CancellationToken ct = default);

    Task<IReadOnlyList<DonorContributionRow>> GetMyContributionsAsync(int donorId, CancellationToken ct = default);

    Task<PagedResult<DonorContributionRow>> GetMyContributionsPagedAsync(int donorId, int pageIndex, int pageSize, CancellationToken ct = default);

    /// <summary>Updates from cases the donor has supported. Strictly filtered by donorId.</summary>
    Task<IReadOnlyList<DonorUpdateRow>> GetMyCaseUpdatesAsync(int donorId, int max = 30, CancellationToken ct = default);

    /// <summary>Admin-only: full list of contributions for a case.</summary>
    Task<IReadOnlyList<Contribution>> GetForCaseAdminAsync(int caseId, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
