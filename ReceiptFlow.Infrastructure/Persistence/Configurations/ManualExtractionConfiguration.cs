using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Configurations;

internal sealed class ManualExtractionConfiguration
	: IEntityTypeConfiguration<ManualExtraction>
{
	public void Configure(EntityTypeBuilder<ManualExtraction> builder)
	{
		builder.ToTable("manual_extractions", table =>
		{
			table.HasCheckConstraint(
				"ck_manual_extractions_confidence",
				"overall_confidence IS NULL OR (overall_confidence >= 0 AND overall_confidence <= 1)");
			table.HasCheckConstraint(
				"ck_manual_extractions_warranty_duration",
				"suggested_warranty_duration_months IS NULL OR (suggested_warranty_duration_months >= 1 AND suggested_warranty_duration_months <= 1200)");
		});

		builder.HasKey(extraction => extraction.Id);

		builder.Property(extraction => extraction.Id)
			.HasColumnName("id")
			.ValueGeneratedNever();
		builder.Property(extraction => extraction.OwnerUserId)
			.HasColumnName("owner_user_id")
			.HasMaxLength(100)
			.IsRequired();
		builder.Property(extraction => extraction.ProductId)
			.HasColumnName("product_id")
			.IsRequired();
		builder.Property(extraction => extraction.ProductManualId)
			.HasColumnName("product_manual_id")
			.IsRequired();
		builder.Property(extraction => extraction.DocumentId)
			.HasColumnName("document_id")
			.IsRequired();
		builder.Property(extraction => extraction.SuggestedManufacturer)
			.HasColumnName("suggested_manufacturer")
			.HasMaxLength(200);
		builder.Property(extraction => extraction.SuggestedProductName)
			.HasColumnName("suggested_product_name")
			.HasMaxLength(200);
		builder.Property(extraction => extraction.SuggestedModelNumber)
			.HasColumnName("suggested_model_number")
			.HasMaxLength(100);
		builder.Property(extraction => extraction.SuggestedVersionLabel)
			.HasColumnName("suggested_version_label")
			.HasMaxLength(100);
		builder.Property(extraction => extraction.SuggestedWarrantyDurationMonths)
			.HasColumnName("suggested_warranty_duration_months");
		builder.Property(extraction => extraction.OverallConfidence)
			.HasColumnName("overall_confidence")
			.HasPrecision(5, 4);
		builder.Property(extraction => extraction.Provider)
			.HasColumnName("provider")
			.HasMaxLength(100)
			.IsRequired();
		builder.Property(extraction => extraction.ModelId)
			.HasColumnName("model_id")
			.HasMaxLength(200)
			.IsRequired();
		builder.Property(extraction => extraction.ExtractedAtUtc)
			.HasColumnName("extracted_at_utc")
			.HasColumnType("timestamp with time zone")
			.IsRequired();
		builder.Property(extraction => extraction.StructuredDataJson)
			.HasColumnName("structured_data_json")
			.HasColumnType("jsonb");

		builder.HasIndex(extraction => extraction.DocumentId)
			.IsUnique()
			.HasDatabaseName("ux_manual_extractions_document_id");
		builder.HasIndex(extraction => extraction.ProductManualId)
			.IsUnique()
			.HasDatabaseName("ux_manual_extractions_product_manual_id");

		builder.HasOne(extraction => extraction.Document)
			.WithOne(document => document.ManualExtraction)
			.HasForeignKey<ManualExtraction>(extraction => new
			{
				extraction.DocumentId,
				extraction.OwnerUserId
			})
			.HasPrincipalKey<Document>(document => new
			{
				document.Id,
				document.OwnerUserId
			})
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(extraction => extraction.ProductManual)
			.WithOne(manual => manual.Extraction)
			.HasForeignKey<ManualExtraction>(extraction => new
			{
				extraction.ProductManualId,
				extraction.ProductId,
				extraction.OwnerUserId
			})
			.HasPrincipalKey<ProductManual>(manual => new
			{
				manual.Id,
				manual.ProductId,
				manual.OwnerUserId
			})
			.OnDelete(DeleteBehavior.Cascade);
	}
}
