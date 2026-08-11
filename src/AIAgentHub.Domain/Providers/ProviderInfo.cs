namespace AIAgentHub.Domain.Providers;

public sealed class ModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ContextWindow { get; set; }
    public bool IsDefault { get; set; }
    public bool IsDisplayed { get; set; } = true;
}

public sealed class ProviderInfo
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public bool IsAuthenticated { get; set; }
    public ProviderStatus Status { get; set; } = ProviderStatus.NotInstalled;
    public string? Version { get; set; }
    public string? ExecutablePath { get; set; }
    public ProviderCapability Capabilities { get; set; } = ProviderCapability.None;
    public List<ModelInfo> SupportedModels { get; set; } = new();
    public string? InstallInstructions { get; set; }
    public string? InstallCommand { get; set; }
    public string? AuthCommand { get; set; }
    public string? DocumentationUrl { get; set; }
}
