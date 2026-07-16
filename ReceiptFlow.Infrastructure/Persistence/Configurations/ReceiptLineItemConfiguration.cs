using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Configurations;

internal sealed class ReceiptLineItemConfiguration
	: IEntityTypeConfiguration<ReceiptLineItem>
{
	public void Configure(
		EntityTypeBuilder<ReceiptLineItem> builder)
	{
		builder.ToTable("receipt_line_items");

		builder.HasKey(lineItem => lineItem.Id);

		builder.Property(lineItem => lineItem.Id)
			.HasColumnName("id")
			.ValueGeneratedNever();

		builder.Property(lineItem => lineItem.ReceiptId)
			.HasColumnName("receipt_id")
			.IsRequired();

		builder.Property(lineItem => lineItem.Description)
			.HasColumnName("description")
			.HasMaxLength(500)
			.IsRequired();

		builder.Property(lineItem => lineItem.ProductCode)
			.HasColumnName("product_code")
			.HasMaxLength(100);

		builder.Property(lineItem => lineItem.Quantity)
			.HasColumnName("quantity")
			.HasPrecision(18, 4)
			.IsRequired();

		builder.Property(lineItem => lineItem.UnitPrice)
			.HasColumnName("unit_price")
			.HasPrecision(18, 2)
			.IsRequired();

		builder.Property(lineItem => lineItem.LineTotal)
			.HasColumnName("line_total")
			.HasPrecision(18, 2)
			.IsRequired();

		builder.Property(lineItem => lineItem.TaxAmount)
			.HasColumnName("tax_amount")
			.HasPrecision(18, 2);

		builder.Property(lineItem => lineItem.DisplayOrder)
			.HasColumnName("display_order")
			.IsRequired();

		builder.HasIndex(lineItem => new
		{
			lineItem.ReceiptId,
			lineItem.DisplayOrder
		})
			.IsUnique()
			.HasDatabaseName(
				"ux_receipt_line_items_receipt_id_display_order");
	}
}