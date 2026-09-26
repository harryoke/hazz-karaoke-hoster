using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HazzKaraokeHoster.Core.Models;

public static class AlternativeMatch
{
    public static string[] Words(string value) => Regex.Matches(
        new string(value.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant(),
        @"[\p{L}\p{N}]+").Select(m => m.Value).Distinct().Order().ToArray();

    private static double Similarity(string a, string b)
    {
        var x = Words(a); var y = Words(b);
        if (x.Length == 0 || y.Length == 0) return 0;
        return 2.0 * x.Intersect(y).Count() / (x.Length + y.Length);
    }

    // Compare fields both ways; word order does not distinguish Elton John from John Elton.
    // Candidates are suggestions only: the operator always chooses the recording.
    public static double Score(string artist, string title, SongRecord candidate)
    {
        double Pair(string a, string t)
        {
            var ts = Similarity(title, t);
            if (string.IsNullOrWhiteSpace(artist)) return ts >= .75 ? ts : 0;
            var ars = Similarity(artist, a);
            return ts >= .70 && ars >= .65 ? .7 * ts + .3 * ars : 0;
        }
        return Math.Max(Pair(candidate.Artist, candidate.Title), Pair(candidate.Title, candidate.Artist));
    }
}
