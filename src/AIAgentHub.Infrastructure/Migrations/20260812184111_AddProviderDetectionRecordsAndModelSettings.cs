using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIAgentHub.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddProviderDetectionRecordsAndModelSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.CreateTable(
            name: "ProviderDetectionRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                StatusDetails = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                ExecutablePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                IsInstalled = table.Column<bool>(type: "INTEGER", nullable: false),
                IsAuthenticated = table.Column<bool>(type: "INTEGER", nullable: false),
                QuotaResetsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                DetectedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ProviderDetectionRecords", x => x.Id);
            });

        _ = migrationBuilder.CreateTable(
            name: "ProviderModelSettings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ModelId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                IsDisplayed = table.Column<bool>(type: "INTEGER", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                _ = table.PrimaryKey("PK_ProviderModelSettings", x => x.Id);
            });

        _ = migrationBuilder.CreateIndex(
            name: "IX_ProviderDetectionRecords_ProviderId",
            table: "ProviderDetectionRecords",
            column: "ProviderId",
            unique: true);

        _ = migrationBuilder.CreateIndex(
            name: "IX_ProviderModelSettings_ProviderId_ModelId",
            table: "ProviderModelSettings",
            columns: ["ProviderId", "ModelId"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        _ = migrationBuilder.DropTable(
            name: "ProviderDetectionRecords");

        _ = migrationBuilder.DropTable(
            name: "ProviderModelSettings");
    }
}
