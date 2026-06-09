using System.Text;
using System.Text.RegularExpressions;

namespace KindHelp.Web.Services;

public static class Slugger
{
    public static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Guid.NewGuid().ToString("N")[..10];

        var s = input.Trim().ToLowerInvariant();
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '-' or '_' or '/') sb.Append('-');
        }
        var result = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        if (string.IsNullOrEmpty(result)) result = Guid.NewGuid().ToString("N")[..10];
        return result.Length > 80 ? result[..80] : result;
    }
}
