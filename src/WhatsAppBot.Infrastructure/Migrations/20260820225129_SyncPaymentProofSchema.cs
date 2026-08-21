using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncPaymentProofSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "payment_proofs");

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppMediaId",
                table: "payment_proofs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WhatsAppMediaId",
                table: "payment_proofs");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "payment_proofs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
