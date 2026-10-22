namespace AIAgentHub.Domain.Configuration;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string CookieName { get; set; } = string.Empty;
}
