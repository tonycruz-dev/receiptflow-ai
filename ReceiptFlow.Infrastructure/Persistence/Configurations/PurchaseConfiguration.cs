using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseConfiguration
	: IEntityTypeConfiguration<Purchase>
{
	public void Configure(EntityTypeBuilder<Purchase> builder)
	{
		builder.ToTable("purchases", table =>
		{
			table.HasCheckConstraint(
				"ck_purchases_quantity",
				"quantity > 0");
			table.HasCheckConstraint(
				"ck_purchases_warranty_source",
				"(warranty_source_product_manual_id IS NULL AND warranty_duration_months_snapshot IS NULL) OR (warranty_source_product_manual_id IS NOT NULL AND warranty_duration_months_snapshot IS NOT NULL)");
		});

		builder.HasKey(purchase => purchase.Id);

		builder.Property(purchase => purchase.Id)
			.HasColumnName("id")
			.ValueGeneratedNever();

		builder.Property(purchase => purchase.OwnerUserId)
			.HasColumnName("owner_user_id")
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(purchase => purchase.ProductId)
			.HasColumnName("product_id")
			.IsRequired();

		builder.Property(purchase => purchase.ReceiptId)
			.HasColumnName("receipt_id")
			.IsRequired();

		builder.Property(purchase => purchase.ReceiptLineItemId)
			.HasColumnName("receipt_line_item_id");

		builder.Property(purchase => purchase.Quantity)
			.HasColumnName("quantity")
			.HasPrecision(18, 4)
			.IsRequired();

		builder.Property(purchase => purchase.WarrantySourceProductManualId)
			.HasColumnName("warranty_source_product_manual_id");

		builder.Property(purchase => purchase.WarrantyDurationMonthsSnapshot)
			.HasColumnName("warranty_duration_months_snapshot");

		builder.Property(purchase => purchase.CreatedAtUtc)
			.HasColumnName("created_at_utc")
			.HasColumnType("timestamp with time zone")
			.IsRequired();

		builder.Property(purchase => purchase.UpdatedAtUtc)
			.HasColumnName("updated_at_utc")
			.HasColumnType("timestamp with time zone");

		builder.HasIndex(purchase => purchase.OwnerUserId)
			.HasDatabaseName("ix_purchases_owner_user_id");

		builder.HasIndex(purchase => new
		{
			purchase.OwnerUserId,
			purchase.ProductId,
			purchase.ReceiptId,
			purchase.ReceiptLineItemId
		})
			.IsUnique()
			.HasFilter("receipt_line_item_id IS NOT NULL")
			.HasDatabaseName("ux_purchases_product_receipt_line_item");

		builder.HasIndex(purchase => new
		{
			purchase.OwnerUserId,
			purchase.ProductId,
			purchase.ReceiptId
		})
			.IsUnique()
			.HasFilter("receipt_line_item_id IS NULL")
			.HasDatabaseName("ux_purchases_product_receipt_without_line_item");

		builder.HasOne(purchase => purchase.Receipt)
			.WithMany()
			.HasForeignKey(purchase => new
			{
				purchase.ReceiptId,
				purchase.OwnerUserId
			})
			.HasPrincipalKey(receipt => new
			{
				receipt.Id,
				receipt.OwnerUserId
			})
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(purchase => purchase.ReceiptLineItem)
			.WithMany()
			.HasForeignKey(purchase => purchase.ReceiptLineItemId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasOne(purchase => purchase.WarrantySourceProductManual)
			.WithMany()
			.HasForeignKey(purchase => new
			{
				purchase.WarrantySourceProductManualId,
				purchase.ProductId,
				purchase.OwnerUserId
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
