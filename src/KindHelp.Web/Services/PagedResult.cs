namespace KindHelp.Web.Services;

/// <summary>Simple paged-list helper for admin and donor list pages.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageIndex, int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;
    public bool HasPrevious => PageIndex > 1;
    public bool HasNext => PageIndex < TotalPages;
}
