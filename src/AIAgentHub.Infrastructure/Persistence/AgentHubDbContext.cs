using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Mcp;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Security;
using AIAgentHub.Domain.Skills;
using AIAgentHub.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace AIAgentHub.Infrastructure.Persistence;

public sealed class AgentHubDbContext : DbContext
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<FileChange> FileChanges => Set<FileChange>();
    public DbSet<FileSnapshot> FileSnapshots => Set<FileSnapshot>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<ServerSettings> ServerSettings => Set<ServerSettings>();
    public DbSet<EncryptedSecret> Secrets => Set<EncryptedSecret>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<McpServer> McpServers => Set<McpServer>();
    public DbSet<PermissionRequest> PermissionRequests => Set<PermissionRequest>();
    public DbSet<AIAgentHub.Domain.Providers.ProviderModelSetting> ProviderModelSettings => Set<AIAgentHub.Domain.Providers.ProviderModelSetting>();

    public AgentHubDbContext(DbContextOptions<AgentHubDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Workspace
        modelBuilder.Entity<Workspace>(b =>
        {
            b.HasKey(w => w.Id);
            b.Property(w => w.Name).IsRequired().HasMaxLength(256);
            b.Property(w => w.Path).IsRequired().HasMaxLength(1024);
            b.HasIndex(w => w.Path).IsUnique();

            b.OwnsOne(w => w.Settings, sb =>
            {
                sb.Property(s => s.DefaultProviderId).HasMaxLength(64);
                sb.Property(s => s.DefaultModelId).HasMaxLength(128);
                sb.Property(s => s.IgnoredFiles)
                    .HasConversion(
                        v => string.Join(';', v),
                        v => v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList());
            });

            b.HasMany(w => w.Conversations)
                .WithOne()
                .HasForeignKey(c => c.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Conversation
        modelBuilder.Entity<Conversation>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Title).IsRequired().HasMaxLength(256);
            b.Property(c => c.ProviderId).IsRequired().HasMaxLength(64);
            b.Property(c => c.ModelId).HasMaxLength(128);
            b.Property(c => c.Effort).HasMaxLength(64);
            b.Property(c => c.ProviderSessionId).HasMaxLength(256);
            b.HasIndex(c => c.WorkspaceId);

            b.HasMany(c => c.Messages)
                .WithOne()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(c => c.FileChanges)
                .WithOne()
                .HasForeignKey(fc => fc.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Message
        modelBuilder.Entity<Message>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.Content).IsRequired();
            b.HasIndex(m => m.ConversationId);

            b.Property(m => m.Metadata)
                .HasConversion(
                    v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? null : System.Text.Json.JsonSerializer.Deserialize<ExecutionMetadata>(v, (System.Text.Json.JsonSerializerOptions?)null));
        });

        // FileChange
        modelBuilder.Entity<FileChange>(b =>
        {
            b.HasKey(fc => fc.Id);
            b.Property(fc => fc.RelativePath).IsRequired().HasMaxLength(1024);
            b.Property(fc => fc.SnapshotPath).HasMaxLength(1024);
            b.HasIndex(fc => fc.ConversationId);
        });

        // FileSnapshot
        modelBuilder.Entity<FileSnapshot>(b =>
        {
            b.HasKey(fs => fs.Id);
            b.Property(fs => fs.RelativePath).IsRequired().HasMaxLength(1024);
            b.Property(fs => fs.StorageKey).IsRequired().HasMaxLength(256);
            b.Property(fs => fs.FileHash).IsRequired().HasMaxLength(128);
            b.HasIndex(fs => new { fs.WorkspaceId, fs.RelativePath });
        });

        // UserAccount
        modelBuilder.Entity<UserAccount>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Username).IsRequired().HasMaxLength(128);
            b.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
            b.Property(u => u.PasswordSalt).IsRequired().HasMaxLength(128);
            b.Property(u => u.RecoveryCodeHash).IsRequired().HasMaxLength(256);
            b.HasIndex(u => u.Username).IsUnique();
        });

        // ServerSettings
        modelBuilder.Entity<ServerSettings>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.SelectedInterfaces)
                .HasConversion(
                    v => string.Join(';', v),
                    v => v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList());
        });

        // EncryptedSecret
        modelBuilder.Entity<EncryptedSecret>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.ProviderId).IsRequired().HasMaxLength(64);
            b.Property(s => s.KeyName).IsRequired().HasMaxLength(128);
            b.HasIndex(s => new { s.ProviderId, s.KeyName }).IsUnique();
        });

        // Skill
        modelBuilder.Entity<Skill>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).IsRequired().HasMaxLength(128);
        });

        // McpServer
        modelBuilder.Entity<McpServer>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.Name).IsRequired().HasMaxLength(128);
            b.Property(m => m.Command).IsRequired().HasMaxLength(512);

            b.Ignore(m => m.Tools);
            b.Property(m => m.EnvironmentVariables)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
        });

        // PermissionRequest
        modelBuilder.Entity<PermissionRequest>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.ProviderId).IsRequired().HasMaxLength(64);
            b.Property(p => p.Target).IsRequired().HasMaxLength(1024);
            b.Property(p => p.Reason).HasMaxLength(1024);
            b.HasIndex(p => p.ConversationId);
        });

        // ProviderModelSetting
        modelBuilder.Entity<AIAgentHub.Domain.Providers.ProviderModelSetting>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.ProviderId).IsRequired().HasMaxLength(64);
            b.Property(s => s.ModelId).IsRequired().HasMaxLength(128);
            b.HasIndex(s => new { s.ProviderId, s.ModelId }).IsUnique();
        });
    }
}
