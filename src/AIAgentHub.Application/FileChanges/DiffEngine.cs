namespace AIAgentHub.Application.FileChanges;

public sealed class DiffEngine : IDiffEngine
{
    public DiffResult CalculateTextDiff(string relativePath, string? oldText, string? newText)
    {
        var oldLines = (oldText ?? "").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var newLines = (newText ?? "").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        if (oldText == null && newText != null)
        {
            var unified = new List<DiffLine>();
            var sideBySide = new List<SideBySideLine>();
            for (int i = 0; i < newLines.Length; i++)
            {
                unified.Add(new DiffLine(null, i + 1, newLines[i], DiffLineKind.Added));
                sideBySide.Add(new SideBySideLine(null, null, DiffLineKind.Unchanged, i + 1, newLines[i], DiffLineKind.Added));
            }
            return new DiffResult(relativePath, false, true, newLines.Length, 0, unified, sideBySide, oldText, newText);
        }

        if (oldText != null && newText == null)
        {
            var unified = new List<DiffLine>();
            var sideBySide = new List<SideBySideLine>();
            for (int i = 0; i < oldLines.Length; i++)
            {
                unified.Add(new DiffLine(i + 1, null, oldLines[i], DiffLineKind.Deleted));
                sideBySide.Add(new SideBySideLine(i + 1, oldLines[i], DiffLineKind.Deleted, null, null, DiffLineKind.Unchanged));
            }
            return new DiffResult(relativePath, false, true, 0, oldLines.Length, unified, sideBySide, oldText, newText);
        }

        // Standard dynamic programming LCS (Longest Common Subsequence)
        int n = oldLines.Length;
        int m = newLines.Length;
        int[,] dp = new int[n + 1, m + 1];

        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                if (string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal))
                {
                    dp[i, j] = 1 + dp[i + 1, j + 1];
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }
        }

        var unifiedList = new List<DiffLine>();
        var sbsList = new List<SideBySideLine>();
        int curOld = 0, curNew = 0;
        int additions = 0, deletions = 0;

        while (curOld < n && curNew < m)
        {
            if (string.Equals(oldLines[curOld], newLines[curNew], StringComparison.Ordinal))
            {
                unifiedList.Add(new DiffLine(curOld + 1, curNew + 1, oldLines[curOld], DiffLineKind.Unchanged));
                sbsList.Add(new SideBySideLine(curOld + 1, oldLines[curOld], DiffLineKind.Unchanged, curNew + 1, newLines[curNew], DiffLineKind.Unchanged));
                curOld++;
                curNew++;
            }
            else if (dp[curOld + 1, curNew] >= dp[curOld, curNew + 1])
            {
                deletions++;
                unifiedList.Add(new DiffLine(curOld + 1, null, oldLines[curOld], DiffLineKind.Deleted));
                sbsList.Add(new SideBySideLine(curOld + 1, oldLines[curOld], DiffLineKind.Deleted, null, null, DiffLineKind.Unchanged));
                curOld++;
            }
            else
            {
                additions++;
                unifiedList.Add(new DiffLine(null, curNew + 1, newLines[curNew], DiffLineKind.Added));
                sbsList.Add(new SideBySideLine(null, null, DiffLineKind.Unchanged, curNew + 1, newLines[curNew], DiffLineKind.Added));
                curNew++;
            }
        }

        while (curOld < n)
        {
            deletions++;
            unifiedList.Add(new DiffLine(curOld + 1, null, oldLines[curOld], DiffLineKind.Deleted));
            sbsList.Add(new SideBySideLine(curOld + 1, oldLines[curOld], DiffLineKind.Deleted, null, null, DiffLineKind.Unchanged));
            curOld++;
        }

        while (curNew < m)
        {
            additions++;
            unifiedList.Add(new DiffLine(null, curNew + 1, newLines[curNew], DiffLineKind.Added));
            sbsList.Add(new SideBySideLine(null, null, DiffLineKind.Unchanged, curNew + 1, newLines[curNew], DiffLineKind.Added));
            curNew++;
        }

        bool hasChanges = additions > 0 || deletions > 0;
        return new DiffResult(relativePath, false, hasChanges, additions, deletions, unifiedList, sbsList, oldText, newText);
    }

    public DiffResult CalculateImageDiff(string relativePath, string? oldDataUri, string? newDataUri)
    {
        bool hasChanges = !string.Equals(oldDataUri, newDataUri, StringComparison.Ordinal);
        return new DiffResult(
            relativePath,
            true,
            hasChanges,
            hasChanges ? 1 : 0,
            hasChanges ? 1 : 0,
            Array.Empty<DiffLine>(),
            Array.Empty<SideBySideLine>(),
            oldDataUri,
            newDataUri
        );
    }
}
