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
    ContributionMethod Method,
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
    /// <summary>For donor dashboard — strictly filtered to the given user id.</summary>
    Task<IReadOnlyList<DonorCaseSummary>> GetMyCaseSummariesAsync(string userId, CancellationToken ct = default);

    /// <summary>For donor dashboard — strictly filtered to the given user id.</summary>
    Task<IReadOnlyList<DonorContributionRow>> GetMyContributionsAsync(string userId, CancellationToken ct = default);

    /// <summary>Updates from cases the donor has supported. Strictly filtered.</summary>
    Task<IReadOnlyList<DonorUpdateRow>> GetMyCaseUpdatesAsync(string userId, int max = 30, CancellationToken ct = default);

    /// <summary>Admin-only: full list of contributions for a case.</summary>
    Task<IReadOnlyList<Contribution>> GetForCaseAdminAsync(int caseId, CancellationToken ct = default);

    Task<Contribution> RecordAsync(Contribution input, string recordedByUserId, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
