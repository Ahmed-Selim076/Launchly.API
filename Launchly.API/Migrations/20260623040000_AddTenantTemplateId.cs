using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Launchly.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantTemplateId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Which of the 3 layout templates for the tenant's StoreType
            // they picked at signup. Defaults to 1 so existing tenants
            // (created before this column existed) land on a defined value
            // rather than 0, which isn't a valid template id.
            // See BACKEND_PLAN.md Section 17.
            migrationBuilder.AddColumn<int>(
                name: "TemplateId",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Tenants");
        }
    }
}
