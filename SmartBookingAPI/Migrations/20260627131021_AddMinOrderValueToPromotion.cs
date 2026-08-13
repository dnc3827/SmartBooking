using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartBookingAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddMinOrderValueToPromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinOrderValue",
                table: "Promotions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinOrderValue",
                table: "Promotions");
        }
    }
}
