using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIAgentHub.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.CreateTable(
            name: "FileSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                RelativePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                StorageKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FileHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Size = table.Column<long>(type: "INTEGER", nullable: false),
                CapturedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_FileSnapshots", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "McpServers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Command = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                Arguments = table.Column<string>(type: "TEXT", nullable: true),
                EnvironmentVariables = table.Column<string>(type: "TEXT", nullable: false),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_McpServers", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "PermissionRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Target = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                Reason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                Decision = table.Column<int>(type: "INTEGER", nullable: false),
                RequestedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                DecidedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_PermissionRequests", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "Secrets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                KeyName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                CiphertextBase64 = table.Column<string>(type: "TEXT", nullable: false),
                NonceBase64 = table.Column<string>(type: "TEXT", nullable: false),
                TagBase64 = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_Secrets", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "ServerSettings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                IsSetupCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                NetworkMode = table.Column<int>(type: "INTEGER", nullable: false),
                ListeningPortHttps = table.Column<int>(type: "INTEGER", nullable: false),
                ListeningPortHttp = table.Column<int>(type: "INTEGER", nullable: false),
                SelectedInterfaces = table.Column<string>(type: "TEXT", nullable: false),
                Theme = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ServerSettings", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "Skills",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                Author = table.Column<string>(type: "TEXT", nullable: true),
                ProviderId = table.Column<string>(type: "TEXT", nullable: true),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                FilePath = table.Column<string>(type: "TEXT", nullable: true),
                Content = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_Skills", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Username = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                PasswordSalt = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                RecoveryCodeHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                LastLoginAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_Users", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "Workspaces",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                Origin = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                LastAccessedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Settings_DefaultProviderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Settings_DefaultModelId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                Settings_IgnoredFiles = table.Column<string>(type: "TEXT", nullable: false),
                Settings_AutoAcceptDiffs = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_Workspaces", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "Conversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ProviderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ModelId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                Effort = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                ProviderSessionId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_Conversations", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_Conversations_Workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "Workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "FileChanges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                RelativePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                ChangeType = table.Column<int>(type: "INTEGER", nullable: false),
                SnapshotPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_FileChanges", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_FileChanges_Conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateTable(
            name: "Messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Role = table.Column<int>(type: "INTEGER", nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Metadata = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_Messages", x => x.Id);
                _ = table.ForeignKey(
                    name: "FK_Messages_Conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        _ = migrationBuilder.CreateIndex(
            name: "IX_Conversations_WorkspaceId",
            table: "Conversations",
            column: "WorkspaceId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_FileChanges_ConversationId",
            table: "FileChanges",
            column: "ConversationId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_FileSnapshots_WorkspaceId_RelativePath",
            table: "FileSnapshots",
            columns: ["WorkspaceId", "RelativePath"]);

        _ = migrationBuilder.CreateIndex(
            name: "IX_Messages_ConversationId",
            table: "Messages",
            column: "ConversationId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_PermissionRequests_ConversationId",
            table: "PermissionRequests",
            column: "ConversationId");

        _ = migrationBuilder.CreateIndex(
            name: "IX_Secrets_ProviderId_KeyName",
            table: "Secrets",
            columns: ["ProviderId", "KeyName"],
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_Users_Username",
            table: "Users",
            column: "Username",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_Workspaces_Path",
            table: "Workspaces",
            column: "Path",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.DropTable(
            name: "FileChanges");

        _ = migrationBuilder.DropTable(
            name: "FileSnapshots");

        _ = migrationBuilder.DropTable(
            name: "McpServers");

        _ = migrationBuilder.DropTable(
            name: "Messages");

        _ = migrationBuilder.DropTable(
            name: "PermissionRequests");

        _ = migrationBuilder.DropTable(
            name: "Secrets");

        _ = migrationBuilder.DropTable(
            name: "ServerSettings");

        _ = migrationBuilder.DropTable(
            name: "Skills");

        _ = migrationBuilder.DropTable(
            name: "Users");

        _ = migrationBuilder.DropTable(
            name: "Conversations");

        _ = migrationBuilder.DropTable(
            name: "Workspaces");
    }
}
