using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Configurations;

internal sealed class ManualSectionConfiguration
	: IEntityTypeConfiguration<ManualSection>
{
	public void Configure(EntityTypeBuilder<ManualSection> builder)
	{
		builder.ToTable("manual_sections", table =>
		{
			table.HasCheckConstraint(
				"ck_manual_sections_ordinal",
				"ordinal >= 0");
			table.HasCheckConstraint(
				"ck_manual_sections_pages",
				"(page_start IS NULL OR page_start > 0) AND " +
				"(page_end IS NULL OR page_end > 0) AND " +
				"(page_start IS NULL OR page_end IS NULL OR page_end >= page_start)");
		});

		builder.HasKey(section => section.Id);

		builder.Property(section => section.Id)
			.HasColumnName("id")
			.ValueGeneratedNever();
		builder.Property(section => section.OwnerUserId)
			.HasColumnName("owner_user_id")
			.HasMaxLength(100)
			.IsRequired();
		builder.Property(section => section.ProductId)
			.HasColumnName("product_id")
			.IsRequired();
		builder.Property(section => section.ProductManualId)
			.HasColumnName("product_manual_id")
			.IsRequired();
		builder.Property(section => section.Ordinal)
			.HasColumnName("ordinal")
			.IsRequired();
		builder.Property(section => section.HeadingPath)
			.HasColumnName("heading_path")
			.HasMaxLength(500)
			.IsRequired();
		builder.Property(section => section.PageStart)
			.HasColumnName("page_start");
		builder.Property(section => section.PageEnd)
			.HasColumnName("page_end");
		builder.Property(section => section.Content)
			.HasColumnName("content")
			.IsRequired();
		builder.Property(section => section.ContentChecksum)
			.HasColumnName("content_checksum")
			.HasMaxLength(64)
			.IsFixedLength()
			.IsRequired();

		builder.HasIndex(section => new
		{
			section.OwnerUserId,
			section.ProductManualId,
			section.Ordinal
		})
			.IsUnique()
			.HasDatabaseName("ux_manual_sections_owner_manual_ordinal");

		builder.HasOne(section => section.ProductManual)
			.WithMany(manual => manual.Sections)
			.HasForeignKey(section => new
			{
				section.ProductManualId,
				section.ProductId,
				section.OwnerUserId
			})
			.HasPrincipalKey(manual => new
			{
				manual.Id,
				manual.ProductId,
				manual.OwnerUserId
			})
			.OnDelete(DeleteBehavior.Cascade);
	}
}
