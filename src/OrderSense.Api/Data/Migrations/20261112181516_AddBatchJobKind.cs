using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderSense.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchJobKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_batch_jobs_active_predict_missing",
                table: "batch_jobs");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "batch_jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Upload");

            migrationBuilder.Sql("UPDATE batch_jobs SET kind = 'PredictMissing' WHERE file_name = 'predict-missing'");

            migrationBuilder.CreateIndex(
                name: "ux_batch_jobs_active_predict_missing",
                table: "batch_jobs",
                column: "kind",
                unique: true,
                filter: "kind = 'PredictMissing' AND status IN ('Queued', 'Running')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_batch_jobs_active_predict_missing",
                table: "batch_jobs");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "batch_jobs");

            migrationBuilder.CreateIndex(
                name: "ux_batch_jobs_active_predict_missing",
                table: "batch_jobs",
                column: "file_name",
                unique: true,
                filter: "file_name = 'predict-missing' AND status IN ('Queued', 'Running')");
        }
    }
}
