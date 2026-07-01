using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartTravelPlaners.BLL.Helpers
{
    public static class FuzzyMatcher
    {
        public static (string NormalizedQuery, string ResolvedName, string DestId)? FindClosest(
            string query, 
            List<(string NormalizedQuery, string ResolvedName, string DestId)> candidates, 
            int maxDistance)
        {
            if (string.IsNullOrWhiteSpace(query) || candidates == null || !candidates.Any())
                return null;

            int bestDistance = int.MaxValue;
            (string, string, string)? bestMatch = null;

            foreach (var candidate in candidates)
            {
                int distance = LevenshteinDistance(query, candidate.NormalizedQuery);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = candidate;
                }
            }

            if (bestDistance <= maxDistance && bestMatch != null)
                return bestMatch;

            return null;
        }

        private static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return input.Trim().ToLowerInvariant();
        }
    }
}
