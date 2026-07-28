using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseWarrantySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_purchases_product_receipt_line_item",
                table: "purchases");

            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                table: "purchases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "purchases",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "purchase_date",
                table: "purchases",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateOnly>(
                name: "warranty_expires_on",
                table: "purchases",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_purchases_receipt_line_item",
                table: "purchases",
                columns: new[] { "owner_user_id", "receipt_id", "receipt_line_item_id" },
                unique: true,
                filter: "receipt_line_item_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_purchases_receipt_line_item",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "amount",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "purchase_date",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "warranty_expires_on",
                table: "purchases");

            migrationBuilder.CreateIndex(
                name: "ux_purchases_product_receipt_line_item",
                table: "purchases",
                columns: new[] { "owner_user_id", "product_id", "receipt_id", "receipt_line_item_id" },
                unique: true,
                filter: "receipt_line_item_id IS NOT NULL");
        }
    }
}
