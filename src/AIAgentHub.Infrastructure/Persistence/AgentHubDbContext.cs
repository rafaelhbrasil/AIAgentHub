using System.Text.Json;

using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Mcp;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Security;
using AIAgentHub.Domain.Skills;
using AIAgentHub.Domain.Workspaces;

using Microsoft.EntityFrameworkCore;

namespace AIAgentHub.Infrastructure.Persistence;

public sealed class AgentHubDbContext(DbContextOptions<AgentHubDbContext> options) : DbContext(options)
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
    public DbSet<ProviderModelSetting> ProviderModelSettings => Set<ProviderModelSetting>();
    public DbSet<ProviderDetectionRecord> ProviderDetectionRecords => Set<ProviderDetectionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Workspace
        _ = modelBuilder.Entity<Workspace>(b =>
        {
            _ = b.HasKey(w => w.Id);
            _ = b.Property(w => w.Name).IsRequired().HasMaxLength(256);
            _ = b.Property(w => w.Path).IsRequired().HasMaxLength(1024);
            _ = b.HasIndex(w => w.Path).IsUnique();

            _ = b.OwnsOne(w => w.Settings, sb =>
            {
                _ = sb.Property(s => s.DefaultProviderId).HasMaxLength(64);
                _ = sb.Property(s => s.DefaultModelId).HasMaxLength(128);
                _ = sb.Property(s => s.IgnoredFiles)
                    .HasConversion(
                        v => string.Join(';', v),
                        v => v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList());
            });

            _ = b.HasMany(w => w.Conversations)
                .WithOne()
                .HasForeignKey(c => c.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Conversation
        _ = modelBuilder.Entity<Conversation>(b =>
        {
            _ = b.HasKey(c => c.Id);
            _ = b.Property(c => c.Title).IsRequired().HasMaxLength(256);
            _ = b.Property(c => c.ProviderId).IsRequired().HasMaxLength(64);
            _ = b.Property(c => c.ModelId).HasMaxLength(128);
            _ = b.Property(c => c.Effort).HasMaxLength(64);
            _ = b.Property(c => c.ProviderSessionId).HasMaxLength(256);
            _ = b.HasIndex(c => c.WorkspaceId);

            _ = b.HasMany(c => c.Messages)
                .WithOne()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = b.HasMany(c => c.FileChanges)
                .WithOne()
                .HasForeignKey(fc => fc.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Message
        _ = modelBuilder.Entity<Message>(b =>
        {
            _ = b.HasKey(m => m.Id);
            _ = b.Property(m => m.Content).IsRequired();
            _ = b.HasIndex(m => m.ConversationId);

            _ = b.Property(m => m.Metadata)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<ExecutionMetadata>(v, (JsonSerializerOptions?)null));
        });

        // FileChange
        _ = modelBuilder.Entity<FileChange>(b =>
        {
            _ = b.HasKey(fc => fc.Id);
            _ = b.Property(fc => fc.RelativePath).IsRequired().HasMaxLength(1024);
            _ = b.Property(fc => fc.SnapshotPath).HasMaxLength(1024);
            _ = b.HasIndex(fc => fc.ConversationId);
        });

        // FileSnapshot
        _ = modelBuilder.Entity<FileSnapshot>(b =>
        {
            _ = b.HasKey(fs => fs.Id);
            _ = b.Property(fs => fs.RelativePath).IsRequired().HasMaxLength(1024);
            _ = b.Property(fs => fs.StorageKey).IsRequired().HasMaxLength(256);
            _ = b.Property(fs => fs.FileHash).IsRequired().HasMaxLength(128);
            _ = b.HasIndex(fs => new { fs.WorkspaceId, fs.RelativePath });
        });

        // UserAccount
        _ = modelBuilder.Entity<UserAccount>(b =>
        {
            _ = b.HasKey(u => u.Id);
            _ = b.Property(u => u.Username).IsRequired().HasMaxLength(128);
            _ = b.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
            _ = b.Property(u => u.PasswordSalt).IsRequired().HasMaxLength(128);
            _ = b.Property(u => u.RecoveryCodeHash).IsRequired().HasMaxLength(256);
            _ = b.HasIndex(u => u.Username).IsUnique();
        });

        // ServerSettings
        _ = modelBuilder.Entity<ServerSettings>(b =>
        {
            _ = b.HasKey(s => s.Id);
            _ = b.Property(s => s.SelectedInterfaces)
                .HasConversion(
                    v => string.Join(';', v),
                    v => v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList());
        });

        // EncryptedSecret
        _ = modelBuilder.Entity<EncryptedSecret>(b =>
        {
            _ = b.HasKey(s => s.Id);
            _ = b.Property(s => s.ProviderId).IsRequired().HasMaxLength(64);
            _ = b.Property(s => s.KeyName).IsRequired().HasMaxLength(128);
            _ = b.HasIndex(s => new { s.ProviderId, s.KeyName }).IsUnique();
        });

        // Skill
        _ = modelBuilder.Entity<Skill>(b =>
        {
            _ = b.HasKey(s => s.Id);
            _ = b.Property(s => s.Name).IsRequired().HasMaxLength(128);
        });

        // McpServer
        _ = modelBuilder.Entity<McpServer>(b =>
        {
            _ = b.HasKey(m => m.Id);
            _ = b.Property(m => m.Name).IsRequired().HasMaxLength(128);
            _ = b.Property(m => m.Command).IsRequired().HasMaxLength(512);

            _ = b.Ignore(m => m.Tools);
            _ = b.Property(m => m.EnvironmentVariables)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new());
        });

        // PermissionRequest
        _ = modelBuilder.Entity<PermissionRequest>(b =>
        {
            _ = b.HasKey(p => p.Id);
            _ = b.Property(p => p.ProviderId).IsRequired().HasMaxLength(64);
            _ = b.Property(p => p.Target).IsRequired().HasMaxLength(1024);
            _ = b.Property(p => p.Reason).HasMaxLength(1024);
            _ = b.HasIndex(p => p.ConversationId);
        });

        // ProviderModelSetting
        _ = modelBuilder.Entity<ProviderModelSetting>(b =>
        {
            _ = b.HasKey(s => s.Id);
            _ = b.Property(s => s.ProviderId).IsRequired().HasMaxLength(64);
            _ = b.Property(s => s.ModelId).IsRequired().HasMaxLength(128);
            _ = b.Property(s => s.DisplayName).HasMaxLength(256);
            _ = b.Property(s => s.Description).HasMaxLength(1024);
            _ = b.HasIndex(s => new { s.ProviderId, s.ModelId }).IsUnique();
        });

        // ProviderDetectionRecord
        _ = modelBuilder.Entity<ProviderDetectionRecord>(b =>
        {
            _ = b.HasKey(r => r.Id);
            _ = b.Property(r => r.ProviderId).IsRequired().HasMaxLength(64);
            _ = b.Property(r => r.StatusDetails).HasMaxLength(1024);
            _ = b.Property(r => r.Version).HasMaxLength(64);
            _ = b.Property(r => r.ExecutablePath).HasMaxLength(1024);
            _ = b.HasIndex(r => r.ProviderId).IsUnique();
        });
    }
}
