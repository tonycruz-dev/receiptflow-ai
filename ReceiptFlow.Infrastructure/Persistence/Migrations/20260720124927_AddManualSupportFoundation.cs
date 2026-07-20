using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualSupportFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_receipts_id_owner_user_id",
                table: "receipts",
                columns: new[] { "id", "owner_user_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_documents_id_owner_user_id",
                table: "documents",
                columns: new[] { "id", "owner_user_id" });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    model_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    normalized_manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_model_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.UniqueConstraint("ak_products_id_owner_user_id", x => new { x.id, x.owner_user_id });
                });

            migrationBuilder.CreateTable(
                name: "product_manuals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    warranty_duration_months = table.Column<int>(type: "integer", nullable: true),
                    lifecycle_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    supersedes_product_manual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_manuals", x => x.id);
                    table.UniqueConstraint("ak_product_manuals_id_product_id_owner_user_id", x => new { x.id, x.product_id, x.owner_user_id });
                    table.CheckConstraint("ck_product_manuals_warranty_duration", "warranty_duration_months IS NULL OR (warranty_duration_months >= 1 AND warranty_duration_months <= 1200)");
                    table.ForeignKey(
                        name: "FK_product_manuals_documents_document_id_owner_user_id",
                        columns: x => new { x.document_id, x.owner_user_id },
                        principalTable: "documents",
                        principalColumns: new[] { "id", "owner_user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_manuals_product_manuals_supersedes_product_manual_i~",
                        columns: x => new { x.supersedes_product_manual_id, x.product_id, x.owner_user_id },
                        principalTable: "product_manuals",
                        principalColumns: new[] { "id", "product_id", "owner_user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_manuals_products_product_id_owner_user_id",
                        columns: x => new { x.product_id, x.owner_user_id },
                        principalTable: "products",
                        principalColumns: new[] { "id", "owner_user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_line_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    warranty_source_product_manual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warranty_duration_months_snapshot = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchases", x => x.id);
                    table.CheckConstraint("ck_purchases_quantity", "quantity > 0");
                    table.CheckConstraint("ck_purchases_warranty_source", "(warranty_source_product_manual_id IS NULL AND warranty_duration_months_snapshot IS NULL) OR (warranty_source_product_manual_id IS NOT NULL AND warranty_duration_months_snapshot IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_purchases_product_manuals_warranty_source_product_manual_id~",
                        columns: x => new { x.warranty_source_product_manual_id, x.product_id, x.owner_user_id },
                        principalTable: "product_manuals",
                        principalColumns: new[] { "id", "product_id", "owner_user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchases_products_product_id_owner_user_id",
                        columns: x => new { x.product_id, x.owner_user_id },
                        principalTable: "products",
                        principalColumns: new[] { "id", "owner_user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchases_receipt_line_items_receipt_line_item_id",
                        column: x => x.receipt_line_item_id,
                        principalTable: "receipt_line_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchases_receipts_receipt_id_owner_user_id",
                        columns: x => new { x.receipt_id, x.owner_user_id },
                        principalTable: "receipts",
                        principalColumns: new[] { "id", "owner_user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_documents_product_manual_not_receipt",
                table: "documents",
                sql: "NOT (receipt_id IS NOT NULL AND document_type = 'ProductManual')");

            migrationBuilder.CreateIndex(
                name: "IX_product_manuals_document_id_owner_user_id",
                table: "product_manuals",
                columns: new[] { "document_id", "owner_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_manuals_product_id_owner_user_id",
                table: "product_manuals",
                columns: new[] { "product_id", "owner_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_manuals_supersedes_product_manual_id_product_id_own~",
                table: "product_manuals",
                columns: new[] { "supersedes_product_manual_id", "product_id", "owner_user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_product_manuals_active_family",
                table: "product_manuals",
                columns: new[] { "owner_user_id", "product_id", "manual_kind", "locale" },
                unique: true,
                filter: "lifecycle_status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ux_product_manuals_document_id",
                table: "product_manuals",
                column: "document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_owner_user_id",
                table: "products",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_products_owner_manufacturer_model",
                table: "products",
                columns: new[] { "owner_user_id", "normalized_manufacturer", "normalized_model_number" },
                unique: true,
                filter: "normalized_model_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_purchases_owner_user_id",
                table: "purchases",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_product_id_owner_user_id",
                table: "purchases",
                columns: new[] { "product_id", "owner_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_receipt_id_owner_user_id",
                table: "purchases",
                columns: new[] { "receipt_id", "owner_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_receipt_line_item_id",
                table: "purchases",
                column: "receipt_line_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_warranty_source_product_manual_id_product_id_owne~",
                table: "purchases",
                columns: new[] { "warranty_source_product_manual_id", "product_id", "owner_user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_purchases_product_receipt_line_item",
                table: "purchases",
                columns: new[] { "owner_user_id", "product_id", "receipt_id", "receipt_line_item_id" },
                unique: true,
                filter: "receipt_line_item_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_purchases_product_receipt_without_line_item",
                table: "purchases",
                columns: new[] { "owner_user_id", "product_id", "receipt_id" },
                unique: true,
                filter: "receipt_line_item_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchases");

            migrationBuilder.DropTable(
                name: "product_manuals");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_receipts_id_owner_user_id",
                table: "receipts");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_documents_id_owner_user_id",
                table: "documents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_documents_product_manual_not_receipt",
                table: "documents");
        }
    }
}
