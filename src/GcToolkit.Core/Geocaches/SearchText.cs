using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Geocaches;

/// <summary>Folds text for searching and sorting, so "certova" finds "Čertova skála" and Č sorts with C.</summary>
public static class SearchText
{
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
