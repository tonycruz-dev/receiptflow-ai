using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Configurations;

internal sealed class DocumentExtractionConfiguration
	: IEntityTypeConfiguration<DocumentExtraction>
{
	public void Configure(EntityTypeBuilder<DocumentExtraction> builder)
	{
		builder.ToTable("document_extractions");

		builder.HasKey(extraction => extraction.Id);

		builder.Property(extraction => extraction.Id)
			.HasColumnName("id")
			.ValueGeneratedNever();

		builder.Property(extraction => extraction.DocumentId)
			.HasColumnName("document_id")
			.IsRequired();

		builder.Property(extraction => extraction.RawText)
			.HasColumnName("raw_text");

		builder.Property(extraction => extraction.MerchantName)
			.HasColumnName("merchant_name")
			.HasMaxLength(500);

		builder.Property(extraction => extraction.TransactionDate)
			.HasColumnName("transaction_date")
			.HasColumnType("timestamp with time zone");

		builder.Property(extraction => extraction.Subtotal)
			.HasColumnName("subtotal")
			.HasPrecision(18, 2);

		builder.Property(extraction => extraction.Tax)
			.HasColumnName("tax")
			.HasPrecision(18, 2);

		builder.Property(extraction => extraction.Total)
			.HasColumnName("total")
			.HasPrecision(18, 2);

		builder.Property(extraction => extraction.Currency)
			.HasColumnName("currency")
			.HasMaxLength(3);

		builder.Property(extraction => extraction.OverallConfidence)
			.HasColumnName("overall_confidence")
			.HasPrecision(5, 4);

		builder.Property(extraction => extraction.Provider)
			.HasColumnName("provider")
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(extraction => extraction.ModelId)
			.HasColumnName("model_id")
			.HasMaxLength(100)
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
			.HasDatabaseName("ux_document_extractions_document_id");

		builder.HasOne(extraction => extraction.Document)
			.WithOne(document => document.Extraction)
			.HasForeignKey<DocumentExtraction>(extraction =>
				extraction.DocumentId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
