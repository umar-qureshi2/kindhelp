using KindHelp.Web.Models;

namespace KindHelp.Web.Services;

public record CaseListItem(
    int Id,
    string Slug,
    string Title,
    string Summary,
    CaseStatus Status,
    CaseCategory Category,
    string? CoverPhotoPath,
    decimal? GoalAmount,
    decimal RaisedAmount,
    int DonorCount);

public record CaseDetailView(
    Case Case,
    IReadOnlyList<CasePhoto> Photos,
    IReadOnlyList<CaseUpdate> Updates,
    decimal RaisedAmount,
    int DonorCount);

public interface ICaseService
{
    Task<IReadOnlyList<CaseListItem>> ListPublicAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CaseListItem>> ListAllForAdminAsync(CancellationToken ct = default);
    Task<CaseDetailView?> GetPublicBySlugAsync(string slug, CancellationToken ct = default);
    Task<Case?> GetForAdminAsync(int id, CancellationToken ct = default);
}
