using System.Linq.Expressions;

namespace KindHelp.Web.Models;

/// <summary>
/// Single source of truth for the "best label for this donor" fallback chain.
/// Used both for in-memory rendering (<see cref="LabelFor"/>) and for EF query
/// projection (<see cref="LabelExpression"/>) so the SQL and the entity never drift.
/// </summary>
public static class DonorExtensions
{
    /// <summary>Display label for a hydrated <see cref="Donor"/> entity.</summary>
    public static string LabelFor(Donor d) =>
        !string.IsNullOrWhiteSpace(d.DisplayName) ? d.DisplayName!
        : !string.IsNullOrWhiteSpace(d.Email)     ? d.Email!
        : !string.IsNullOrWhiteSpace(d.PhoneNumber) ? d.PhoneNumber!
        : $"Donor #{d.Id}";

    /// <summary>EF-translatable label expression — use inside <c>.Select(d => new {{ Label = DonorExtensions.LabelExpression.Compile()(d) }})</c>... no — just inline the equivalent COALESCE.</summary>
    public static readonly Expression<Func<Donor, string>> LabelExpression =
        d => d.DisplayName ?? d.Email ?? d.PhoneNumber ?? ("Donor #" + d.Id);
}
