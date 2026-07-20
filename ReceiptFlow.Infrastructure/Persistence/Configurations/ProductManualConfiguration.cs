using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProductManualConfiguration
	: IEntityTypeConfiguration<ProductManual>
{
	public void Configure(EntityTypeBuilder<ProductManual> builder)
	{
		builder.ToTable("product_manuals", table =>
		{
			table.HasCheckConstraint(
				"ck_product_manuals_warranty_duration",
				"warranty_duration_months IS NULL OR (warranty_duration_months >= 1 AND warranty_duration_months <= 1200)");
		});

		builder.HasKey(manual => manual.Id);

		builder.Property(manual => manual.Id)
			.HasColumnName("id")
			.ValueGeneratedNever();

		builder.Property(manual => manual.OwnerUserId)
			.HasColumnName("owner_user_id")
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(manual => manual.ProductId)
			.HasColumnName("product_id")
			.IsRequired();

		builder.Property(manual => manual.DocumentId)
			.HasColumnName("document_id")
			.IsRequired();

		builder.Property(manual => manual.ManualKind)
			.HasColumnName("manual_kind")
			.HasConversion<string>()
			.HasMaxLength(50)
			.IsRequired();

		builder.Property(manual => manual.Locale)
			.HasColumnName("locale")
			.HasMaxLength(20)
			.IsRequired();

		builder.Property(manual => manual.VersionLabel)
			.HasColumnName("version_label")
			.HasMaxLength(100);

		builder.Property(manual => manual.WarrantyDurationMonths)
			.HasColumnName("warranty_duration_months");

		builder.Property(manual => manual.LifecycleStatus)
			.HasColumnName("lifecycle_status")
			.HasConversion<string>()
			.HasMaxLength(50)
			.IsRequired();

		builder.Property(manual => manual.SupersedesProductManualId)
			.HasColumnName("supersedes_product_manual_id");

		builder.Property(manual => manual.CreatedAtUtc)
			.HasColumnName("created_at_utc")
			.HasColumnType("timestamp with time zone")
			.IsRequired();

		builder.Property(manual => manual.ConfirmedAtUtc)
			.HasColumnName("confirmed_at_utc")
			.HasColumnType("timestamp with time zone");

		builder.Property(manual => manual.SupersededAtUtc)
			.HasColumnName("superseded_at_utc")
			.HasColumnType("timestamp with time zone");

		builder.HasAlternateKey(manual => new
		{
			manual.Id,
			manual.ProductId,
			manual.OwnerUserId
		})
			.HasName("ak_product_manuals_id_product_id_owner_user_id");

		builder.HasIndex(manual => manual.DocumentId)
			.IsUnique()
			.HasDatabaseName("ux_product_manuals_document_id");

		builder.HasIndex(manual => new
		{
			manual.OwnerUserId,
			manual.ProductId,
			manual.ManualKind,
			manual.Locale
		})
			.IsUnique()
			.HasFilter("lifecycle_status = 'Active'")
			.HasDatabaseName("ux_product_manuals_active_family");

		builder.HasOne(manual => manual.Document)
			.WithOne(document => document.ProductManual)
			.HasForeignKey<ProductManual>(manual => new
			{
				manual.DocumentId,
				manual.OwnerUserId
			})
			.HasPrincipalKey<Document>(document => new
			{
				document.Id,
				document.OwnerUserId
			})
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(manual => manual.SupersedesProductManual)
			.WithMany()
			.HasForeignKey(manual => new
			{
				manual.SupersedesProductManualId,
				manual.ProductId,
				manual.OwnerUserId
			})
			.HasPrincipalKey(manual => new
			{
				manual.Id,
				manual.ProductId,
				manual.OwnerUserId
			})
			.OnDelete(DeleteBehavior.Restrict);
	}
}
