using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackupRestore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDedupVersioningAndSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DedupedBytes",
                table: "BackupJobs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "BackupJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "BackupFiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "BackupSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourcePath = table.Column<string>(type: "text", nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupSchedules_Enabled",
                table: "BackupSchedules",
                column: "Enabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupSchedules");

            migrationBuilder.DropColumn(
                name: "DedupedBytes",
                table: "BackupJobs");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "BackupJobs");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "BackupFiles");
        }
    }
}
