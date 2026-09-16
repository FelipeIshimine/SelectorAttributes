using System.Collections.Generic;

internal static class DropdownSearch
{
    public static void CollectScoredLeaves(DropdownNode node, string query, List<(DropdownNode node, int score)> results)
    {
        foreach (var child in node.Children)
        {
            if (child.IsLeaf)
            {
                var score = FuzzyScore(child.FullPath ?? child.Label, query);
                if (score > 0) results.Add((child, score));
            }
            else
            {
                CollectScoredLeaves(child, query, results);
            }
        }
    }

    public static int FuzzyScore(string text, string query)
    {
        var lText = text.ToLowerInvariant();

        if (lText.Contains(query))
            return 10000 - text.Length;

        int score       = 0;
        int textIdx     = 0;
        int consecutive = 0;

        foreach (var qc in query)
        {
            bool matched = false;
            for (var i = textIdx; i < lText.Length; i++)
            {
                if (lText[i] != qc) { consecutive = 0; continue; }

                bool wordStart = i == 0 || lText[i - 1] == '/' || lText[i - 1] == ' ' || lText[i - 1] == '_';
                score += 1 + consecutive + (wordStart ? 8 : 0);
                consecutive++;
                textIdx = i + 1;
                matched = true;
                break;
            }

            if (!matched) return 0;
        }

        return score;
    }
}
