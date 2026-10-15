using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentId",
                schema: "payments",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "payments",
                table: "Payments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderPaymentId",
                schema: "payments",
                table: "Payments",
                column: "ProviderPaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ProviderPaymentId",
                schema: "payments",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentId",
                schema: "payments",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "payments",
                table: "Payments");
        }
    }
}
