using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIAgentHub.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddModelInfoToProviderModelSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ContextWindow",
            table: "ProviderModelSettings",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "ProviderModelSettings",
            type: "TEXT",
            maxLength: 1024,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DisplayName",
            table: "ProviderModelSettings",
            type: "TEXT",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "IsDefault",
            table: "ProviderModelSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ContextWindow",
            table: "ProviderModelSettings");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "ProviderModelSettings");

        migrationBuilder.DropColumn(
            name: "DisplayName",
            table: "ProviderModelSettings");

        migrationBuilder.DropColumn(
            name: "IsDefault",
            table: "ProviderModelSettings");
    }
}
