using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIAgentHub.Infrastructure.Migrations;

/// <inheritdoc />
public partial class v0_2_0_AddVersion02MultiProviderTracking : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsArchived",
            table: "Workspaces",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsFavorite",
            table: "Workspaces",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsPinned",
            table: "Conversations",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "Status",
            table: "Conversations",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "OriginModelId",
            table: "Messages",
            type: "TEXT",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OriginProviderId",
            table: "Messages",
            type: "TEXT",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SequenceIndex",
            table: "Messages",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "DefaultEffort",
            table: "ProviderDetectionRecords",
            type: "TEXT",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DefaultModelId",
            table: "ProviderDetectionRecords",
            type: "TEXT",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsHidden",
            table: "ProviderDetectionRecords",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "ConversationProviderSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ProviderSessionId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                LastSharedMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                LastSharedSequenceIndex = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                LastActiveAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConversationProviderSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ConversationProviderSessions_Conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalTable: "Conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationProviderSessions_ConversationId_ProviderId",
            table: "ConversationProviderSessions",
            columns: new[] { "ConversationId", "ProviderId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Messages_ConversationId_SequenceIndex",
            table: "Messages",
            columns: new[] { "ConversationId", "SequenceIndex" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ConversationProviderSessions");

        migrationBuilder.DropColumn(
            name: "IsArchived",
            table: "Workspaces");

        migrationBuilder.DropColumn(
            name: "IsFavorite",
            table: "Workspaces");

        migrationBuilder.DropColumn(
            name: "IsPinned",
            table: "Conversations");

        migrationBuilder.DropColumn(
            name: "Status",
            table: "Conversations");

        migrationBuilder.DropIndex(
            name: "IX_Messages_ConversationId_SequenceIndex",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "OriginModelId",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "OriginProviderId",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "SequenceIndex",
            table: "Messages");

        migrationBuilder.DropColumn(
            name: "DefaultEffort",
            table: "ProviderDetectionRecords");

        migrationBuilder.DropColumn(
            name: "DefaultModelId",
            table: "ProviderDetectionRecords");

        migrationBuilder.DropColumn(
            name: "IsHidden",
            table: "ProviderDetectionRecords");
    }
}
