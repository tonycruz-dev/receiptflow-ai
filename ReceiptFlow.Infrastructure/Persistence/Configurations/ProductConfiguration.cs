using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration
	: IEntityTypeConfiguration<Product>
{
	public void Configure(EntityTypeBuilder<Product> builder)
	{
		builder.ToTable("products");

		builder.HasKey(product => product.Id);

		builder.Property(product => product.Id)
			.HasColumnName("id")
			.ValueGeneratedNever();

		builder.Property(product => product.OwnerUserId)
			.HasColumnName("owner_user_id")
			.HasMaxLength(100)
			.IsRequired();

		builder.Property(product => product.Manufacturer)
			.HasColumnName("manufacturer")
			.HasMaxLength(200)
			.IsRequired();

		builder.Property(product => product.Name)
			.HasColumnName("name")
			.HasMaxLength(200)
			.IsRequired();

		builder.Property(product => product.ModelNumber)
			.HasColumnName("model_number")
			.HasMaxLength(100);

		builder.Property(product => product.NormalizedManufacturer)
			.HasColumnName("normalized_manufacturer")
			.HasMaxLength(200)
			.IsRequired();

		builder.Property(product => product.NormalizedModelNumber)
			.HasColumnName("normalized_model_number")
			.HasMaxLength(100);

		builder.Property(product => product.CreatedAtUtc)
			.HasColumnName("created_at_utc")
			.HasColumnType("timestamp with time zone")
			.IsRequired();

		builder.Property(product => product.UpdatedAtUtc)
			.HasColumnName("updated_at_utc")
			.HasColumnType("timestamp with time zone");

		builder.HasAlternateKey(product => new
		{
			product.Id,
			product.OwnerUserId
		})
			.HasName("ak_products_id_owner_user_id");

		builder.HasIndex(product => product.OwnerUserId)
			.HasDatabaseName("ix_products_owner_user_id");

		builder.HasIndex(product => new
		{
			product.OwnerUserId,
			product.NormalizedManufacturer,
			product.NormalizedModelNumber
		})
			.IsUnique()
			.HasFilter("normalized_model_number IS NOT NULL")
			.HasDatabaseName("ux_products_owner_manufacturer_model");

		builder.HasMany(product => product.Manuals)
			.WithOne(manual => manual.Product)
			.HasForeignKey(manual => new
			{
				manual.ProductId,
				manual.OwnerUserId
			})
			.HasPrincipalKey(product => new
			{
				product.Id,
				product.OwnerUserId
			})
			.OnDelete(DeleteBehavior.Restrict);

		builder.Navigation(product => product.Manuals)
			.UsePropertyAccessMode(PropertyAccessMode.Field);

		builder.HasMany(product => product.Purchases)
			.WithOne(purchase => purchase.Product)
			.HasForeignKey(purchase => new
			{
				purchase.ProductId,
				purchase.OwnerUserId
			})
			.HasPrincipalKey(product => new
			{
				product.Id,
				product.OwnerUserId
			})
			.OnDelete(DeleteBehavior.Restrict);

		builder.Navigation(product => product.Purchases)
			.UsePropertyAccessMode(PropertyAccessMode.Field);
	}
}
