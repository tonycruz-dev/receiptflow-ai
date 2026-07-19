using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260719190000_AddReceiptLifecycle")]
public sealed class AddReceiptLifecycle : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(
			name: "category",
			table: "document_extractions",
			type: "character varying(100)",
			maxLength: 100,
			nullable: true);

		migrationBuilder.AddColumn<string>(
			name: "lifecycle_status",
			table: "receipts",
			type: "character varying(50)",
			maxLength: 50,
			nullable: false,
			defaultValue: "Confirmed");

		migrationBuilder.AlterColumn<string>(
			name: "merchant_name",
			table: "receipts",
			type: "character varying(200)",
			maxLength: 200,
			nullable: true,
			oldClrType: typeof(string),
			oldType: "character varying(200)",
			oldMaxLength: 200);

		migrationBuilder.AlterColumn<DateTimeOffset>(
			name: "purchase_date",
			table: "receipts",
			type: "timestamp with time zone",
			nullable: true,
			oldClrType: typeof(DateTimeOffset),
			oldType: "timestamp with time zone");

		migrationBuilder.AlterColumn<decimal>(
			name: "total_amount",
			table: "receipts",
			type: "numeric(18,2)",
			precision: 18,
			scale: 2,
			nullable: true,
			oldClrType: typeof(decimal),
			oldType: "numeric(18,2)",
			oldPrecision: 18,
			oldScale: 2);

		migrationBuilder.AlterColumn<string>(
			name: "currency",
			table: "receipts",
			type: "character varying(3)",
			maxLength: 3,
			nullable: true,
			oldClrType: typeof(string),
			oldType: "character varying(3)",
			oldMaxLength: 3);

		migrationBuilder.AlterColumn<string>(
			name: "category",
			table: "receipts",
			type: "character varying(100)",
			maxLength: 100,
			nullable: true,
			oldClrType: typeof(string),
			oldType: "character varying(100)",
			oldMaxLength: 100);

		migrationBuilder.CreateIndex(
			name: "ix_receipts_owner_user_id_lifecycle_status",
			table: "receipts",
			columns: ["owner_user_id", "lifecycle_status"]);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "ix_receipts_owner_user_id_lifecycle_status",
			table: "receipts");

		migrationBuilder.DropColumn(
			name: "category",
			table: "document_extractions");

		migrationBuilder.DropColumn(
			name: "lifecycle_status",
			table: "receipts");

		migrationBuilder.AlterColumn<string>(
			name: "merchant_name",
			table: "receipts",
			type: "character varying(200)",
			maxLength: 200,
			nullable: false,
			oldClrType: typeof(string),
			oldType: "character varying(200)",
			oldMaxLength: 200,
			oldNullable: true);

		migrationBuilder.AlterColumn<DateTimeOffset>(
			name: "purchase_date",
			table: "receipts",
			type: "timestamp with time zone",
			nullable: false,
			oldClrType: typeof(DateTimeOffset),
			oldType: "timestamp with time zone",
			oldNullable: true);

		migrationBuilder.AlterColumn<decimal>(
			name: "total_amount",
			table: "receipts",
			type: "numeric(18,2)",
			precision: 18,
			scale: 2,
			nullable: false,
			oldClrType: typeof(decimal),
			oldType: "numeric(18,2)",
			oldPrecision: 18,
			oldScale: 2,
			oldNullable: true);

		migrationBuilder.AlterColumn<string>(
			name: "currency",
			table: "receipts",
			type: "character varying(3)",
			maxLength: 3,
			nullable: false,
			oldClrType: typeof(string),
			oldType: "character varying(3)",
			oldMaxLength: 3,
			oldNullable: true);

		migrationBuilder.AlterColumn<string>(
			name: "category",
			table: "receipts",
			type: "character varying(100)",
			maxLength: 100,
			nullable: false,
			oldClrType: typeof(string),
			oldType: "character varying(100)",
			oldMaxLength: 100,
			oldNullable: true);
	}
}
