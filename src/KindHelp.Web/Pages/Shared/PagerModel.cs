namespace KindHelp.Web.Pages.Shared;

/// <summary>
/// View-model for the shared <c>_Pager.cshtml</c> partial. <paramref name="ExtraQuery"/> lets
/// callers preserve filter/search query strings across page navigation.
/// </summary>
public record PagerModel(
    string Page,
    int PageIndex,
    int TotalPages,
    IDictionary<string, string?>? ExtraQuery = null);
