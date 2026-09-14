namespace Wtile.Launcher;

/// <summary>
/// dmenu/rofi-style fuzzy matching: the query must appear as a (case-insensitive) subsequence of
/// the candidate, in order but not necessarily contiguous -- "ffx" matches "FireFoX.exe". Scoring
/// rewards matches that start earlier in the candidate and run together, so "ff" ranks
/// "firefox.exe" (prefix, contiguous) above "coreflow.exe" (scattered) for the same query.
/// </summary>
public static class FuzzyMatcher
{
    /// <summary>Higher is a better match; null means the query isn't a subsequence of candidate at
    /// all. An empty query matches everything with a score of 0 (unfiltered/unranked).</summary>
    public static int? Score(string query, string candidate)
    {
        if (query.Length == 0)
            return 0;
        if (candidate.Length < query.Length)
            return null;

        int score = 0;
        int candidateIndex = 0;
        int previousMatchIndex = -1;
        for (int queryIndex = 0; queryIndex < query.Length; queryIndex++)
        {
            char want = char.ToUpperInvariant(query[queryIndex]);
            int found = -1;
            for (int i = candidateIndex; i < candidate.Length; i++)
            {
                if (char.ToUpperInvariant(candidate[i]) == want)
                {
                    found = i;
                    break;
                }
            }
            if (found < 0)
                return null;

            if (found == 0)
                score += 10; // matches at the very start of the candidate
            if (previousMatchIndex >= 0 && found == previousMatchIndex + 1)
                score += 5; // contiguous run with the previous matched character
            score += Math.Max(0, 10 - found); // earlier matches score higher than later ones

            previousMatchIndex = found;
            candidateIndex = found + 1;
        }

        return score;
    }
}
