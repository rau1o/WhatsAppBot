using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantWabaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WabaId = WhatsApp Business Account ID de Meta.
            // Nullable: los tenants existentes no lo tienen hasta que completen
            // el flujo de Embedded Signup o lo carguen manualmente desde el panel.
            migrationBuilder.AddColumn<string>(
                name: "WabaId",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WabaId",
                table: "tenants");
        }
    }
}
