namespace AIAgentHub.Domain.Configuration;

public sealed class ProviderSettingsOptions
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? InstallInstructions { get; set; }
    public string? AuthCommand { get; set; }
    public string? DocumentationUrl { get; set; }
}

public sealed class ProvidersOptions : Dictionary<string, ProviderSettingsOptions>
{
    public const string SectionName = "Providers";
}
