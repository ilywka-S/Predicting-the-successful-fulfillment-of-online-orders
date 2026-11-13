using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderSense.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictMissingGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "batch_jobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "ux_batch_jobs_active_predict_missing",
                table: "batch_jobs",
                column: "file_name",
                unique: true,
                filter: "file_name = 'predict-missing' AND status IN ('Queued', 'Running')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_batch_jobs_active_predict_missing",
                table: "batch_jobs");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "batch_jobs");
        }
    }
}
