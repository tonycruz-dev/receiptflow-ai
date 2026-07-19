using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Configurations;

internal sealed class ReceiptConfiguration
	: IEntityTypeConfiguration<Receipt>
{
	public void Configure(EntityTypeBuilder<Receipt> builder)
	{
		builder.ToTable("receipts");

		builder.HasKey(receipt => receipt.Id);

		builder.Property(receipt => receipt.Id)
			.HasColumnName("id")
			.ValueGeneratedNever();

		builder.Property(receipt => receipt.OwnerUserId)
			.HasColumnName("owner_user_id")
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(receipt => receipt.MerchantName)
			.HasColumnName("merchant_name")
			.HasMaxLength(200);

		builder.Property(receipt => receipt.PurchaseDate)
			.HasColumnName("purchase_date")
			.HasColumnType("timestamp with time zone");

		builder.Property(receipt => receipt.SubtotalAmount)
			.HasColumnName("subtotal_amount")
			.HasPrecision(18, 2);

		builder.Property(receipt => receipt.TaxAmount)
			.HasColumnName("tax_amount")
			.HasPrecision(18, 2);

		builder.Property(receipt => receipt.TotalAmount)
			.HasColumnName("total_amount")
			.HasPrecision(18, 2);

		builder.Property(receipt => receipt.Currency)
			.HasColumnName("currency")
			.HasMaxLength(3);

		builder.Property(receipt => receipt.Category)
			.HasColumnName("category")
			.HasMaxLength(100);

		builder.Property(receipt => receipt.LifecycleStatus)
			.HasColumnName("lifecycle_status")
			.HasConversion<string>()
			.HasMaxLength(50)
			.IsRequired();

		builder.Property(receipt => receipt.CreatedAtUtc)
			.HasColumnName("created_at_utc")
			.HasColumnType("timestamp with time zone")
			.IsRequired();

		builder.Property(receipt => receipt.UpdatedAtUtc)
			.HasColumnName("updated_at_utc")
			.HasColumnType("timestamp with time zone");

		builder.HasIndex(receipt => receipt.OwnerUserId)
			.HasDatabaseName("ix_receipts_owner_user_id");

		builder.HasIndex(receipt => new
		{
			receipt.OwnerUserId,
			receipt.LifecycleStatus
		})
			.HasDatabaseName("ix_receipts_owner_user_id_lifecycle_status");

		builder.HasIndex(receipt => new
		{
			receipt.OwnerUserId,
			receipt.PurchaseDate
		})
			.HasDatabaseName(
				"ix_receipts_owner_user_id_purchase_date");

		builder.HasMany(receipt => receipt.LineItems)
			.WithOne(lineItem => lineItem.Receipt)
			.HasForeignKey(lineItem => lineItem.ReceiptId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Navigation(receipt => receipt.LineItems)
			.UsePropertyAccessMode(PropertyAccessMode.Field);

		builder.HasMany(receipt => receipt.Documents)
			.WithOne(document => document.Receipt)
			.HasForeignKey(document => document.ReceiptId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.Navigation(receipt => receipt.Documents)
			.UsePropertyAccessMode(PropertyAccessMode.Field);
	}
}
