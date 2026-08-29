namespace AIAgentHub.Domain.Configuration;

public sealed class ProviderSwitchOptions
{
    public const string SectionName = "ProviderSwitchSettings";

    /// <summary>
    /// Recent message count thresholds (e.g., [10, 20, 50]).
    /// </summary>
    public List<int> RecentMessageCounts { get; set; } = [10, 20, 50];

    /// <summary>
    /// Single string property for configuration convenience (e.g. "10, 20, 50").
    /// </summary>
    public string? Counts
    {
        get => string.Join(", ", RecentMessageCounts);
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var parsed = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var n) ? n : -1)
                    .Where(n => n > 0)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
                if (parsed.Count > 0)
                {
                    RecentMessageCounts = parsed;
                }
            }
        }
    }
}
