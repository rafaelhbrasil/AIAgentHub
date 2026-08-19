using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIAgentHub.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddConversationLastUserInteraction : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastUserInteractionAtUtc",
            table: "Conversations",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastUserInteractionAtUtc",
            table: "Conversations");
    }
}
