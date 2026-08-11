using AIAgentHub.Domain.Common;

namespace AIAgentHub.Domain.Security;

public enum NetworkMode
{
    Localhost = 0,
    Lan = 1,
    SelectedInterfaces = 2
}

public sealed class ServerSettings : AggregateRoot
{
    public bool IsSetupCompleted { get; set; }
    public NetworkMode NetworkMode { get; set; } = NetworkMode.Localhost;
    public int ListeningPortHttps { get; set; } = 5432;
    public int ListeningPortHttp { get; set; } = 5433;
    public List<string> SelectedInterfaces { get; set; } = new();
    public string Theme { get; set; } = "dark";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
