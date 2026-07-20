using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration
	: IEntityTypeConfiguration<Document>
{
	public void Configure(EntityTypeBuilder<Document> builder)
	{
		builder.ToTable("documents", table =>
		{
			table.HasCheckConstraint(
				"ck_documents_product_manual_not_receipt",
				"NOT (receipt_id IS NOT NULL AND document_type = 'ProductManual')");
		});

		builder.HasKey(document => document.Id);

		builder.Property(document => document.Id)
			.HasColumnName("id")
			.ValueGeneratedNever();

		builder.Property(document => document.OwnerUserId)
			.HasColumnName("owner_user_id")
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(document => document.ReceiptId)
			.HasColumnName("receipt_id");

		builder.Property(document => document.OriginalFileName)
			.HasColumnName("original_file_name")
			.HasMaxLength(255)
			.IsRequired();

		builder.Property(document => document.StorageKey)
			.HasColumnName("storage_key")
			.HasMaxLength(500)
			.IsRequired();

		builder.Property(document => document.ContentType)
			.HasColumnName("content_type")
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(document => document.SizeBytes)
			.HasColumnName("size_bytes")
			.IsRequired();

		builder.Property(document => document.DocumentType)
			.HasColumnName("document_type")
			.HasConversion<string>()
			.HasMaxLength(50)
			.IsRequired();

		builder.Property(document => document.ProcessingStatus)
			.HasColumnName("processing_status")
			.HasConversion<string>()
			.HasMaxLength(50)
			.IsRequired();

		builder.Property(document => document.Sha256Hash)
			.HasColumnName("sha256_hash")
			.HasMaxLength(64);

		builder.Property(document => document.PageCount)
			.HasColumnName("page_count");

		builder.Property(document => document.ExtractedTextStorageKey)
			.HasColumnName("extracted_text_storage_key")
			.HasMaxLength(500);

		builder.Property(document => document.FailureReason)
			.HasColumnName("failure_reason")
			.HasMaxLength(2000);

		builder.Property(document => document.CreatedAtUtc)
			.HasColumnName("created_at_utc")
			.HasColumnType("timestamp with time zone")
			.IsRequired();

		builder.Property(document => document.ProcessingStartedAtUtc)
			.HasColumnName("processing_started_at_utc")
			.HasColumnType("timestamp with time zone");

		builder.Property(document => document.ProcessedAtUtc)
			.HasColumnName("processed_at_utc")
			.HasColumnType("timestamp with time zone");

		builder.HasIndex(document => document.StorageKey)
			.IsUnique()
			.HasDatabaseName("ux_documents_storage_key");

		builder.HasAlternateKey(document => new
		{
			document.Id,
			document.OwnerUserId
		})
			.HasName("ak_documents_id_owner_user_id");

		builder.HasIndex(document => new
		{
			document.OwnerUserId,
			document.ProcessingStatus
		})
			.HasDatabaseName(
				"ix_documents_owner_user_id_processing_status");

		builder.HasIndex(document => new
		{
			document.OwnerUserId,
			document.Sha256Hash
		})
			.HasDatabaseName(
				"ix_documents_owner_user_id_sha256_hash");
	}
}
