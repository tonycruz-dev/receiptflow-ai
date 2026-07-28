using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualExtractionPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manual_extractions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_manual_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggested_manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    suggested_product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    suggested_model_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    suggested_version_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    suggested_warranty_duration_months = table.Column<int>(type: "integer", nullable: true),
                    overall_confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    extracted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    structured_data_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_extractions", x => x.id);
                    table.CheckConstraint("ck_manual_extractions_confidence", "overall_confidence IS NULL OR (overall_confidence >= 0 AND overall_confidence <= 1)");
                    table.CheckConstraint("ck_manual_extractions_warranty_duration", "suggested_warranty_duration_months IS NULL OR (suggested_warranty_duration_months >= 1 AND suggested_warranty_duration_months <= 1200)");
                    table.ForeignKey(
                        name: "FK_manual_extractions_documents_document_id_owner_user_id",
                        columns: x => new { x.document_id, x.owner_user_id },
                        principalTable: "documents",
                        principalColumns: new[] { "id", "owner_user_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_manual_extractions_product_manuals_product_manual_id_produc~",
                        columns: x => new { x.product_manual_id, x.product_id, x.owner_user_id },
                        principalTable: "product_manuals",
                        principalColumns: new[] { "id", "product_id", "owner_user_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "manual_sections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_manual_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    heading_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    page_start = table.Column<int>(type: "integer", nullable: true),
                    page_end = table.Column<int>(type: "integer", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    content_checksum = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_sections", x => x.id);
                    table.CheckConstraint("ck_manual_sections_ordinal", "ordinal >= 0");
                    table.CheckConstraint("ck_manual_sections_pages", "(page_start IS NULL OR page_start > 0) AND (page_end IS NULL OR page_end > 0) AND (page_start IS NULL OR page_end IS NULL OR page_end >= page_start)");
                    table.ForeignKey(
                        name: "FK_manual_sections_product_manuals_product_manual_id_product_i~",
                        columns: x => new { x.product_manual_id, x.product_id, x.owner_user_id },
                        principalTable: "product_manuals",
                        principalColumns: new[] { "id", "product_id", "owner_user_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manual_extractions_document_id_owner_user_id",
                table: "manual_extractions",
                columns: new[] { "document_id", "owner_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_extractions_product_manual_id_product_id_owner_user_~",
                table: "manual_extractions",
                columns: new[] { "product_manual_id", "product_id", "owner_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_manual_extractions_document_id",
                table: "manual_extractions",
                column: "document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_manual_extractions_product_manual_id",
                table: "manual_extractions",
                column: "product_manual_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_sections_product_manual_id_product_id_owner_user_id",
                table: "manual_sections",
                columns: new[] { "product_manual_id", "product_id", "owner_user_id" });

            migrationBuilder.CreateIndex(
                name: "ux_manual_sections_owner_manual_ordinal",
                table: "manual_sections",
                columns: new[] { "owner_user_id", "product_manual_id", "ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manual_extractions");

            migrationBuilder.DropTable(
                name: "manual_sections");
        }
    }
}
