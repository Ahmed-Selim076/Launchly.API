using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Launchly.API.Migrations
{
    /// <inheritdoc />
    public partial class AddProductRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Maps Product.RowVersion (uint, [Timestamp]/.IsRowVersion() in
            // AppDbContext) to PostgreSQL's xmin system column, used as an
            // optimistic concurrency token so two simultaneous orders can't
            // both decrement the same unit of stock. See OrderService and
            // StoreService.PlaceOrderAsync for the retry logic this enables.
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Products",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Products");
        }
    }
}
