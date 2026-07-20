using MassTransit;
using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
	DbContextOptions<ApplicationDbContext> options)
	: DbContext(options), IUnitOfWork
{
	public DbSet<Receipt> Receipts => Set<Receipt>();

	public DbSet<Document> Documents => Set<Document>();

	public DbSet<DocumentExtraction> DocumentExtractions =>
		Set<DocumentExtraction>();

	public DbSet<ReceiptLineItem> ReceiptLineItems =>
		Set<ReceiptLineItem>();

	public DbSet<Product> Products => Set<Product>();

	public DbSet<ProductManual> ProductManuals => Set<ProductManual>();

	public DbSet<Purchase> Purchases => Set<Purchase>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		modelBuilder.ApplyConfigurationsFromAssembly(
			typeof(ApplicationDbContext).Assembly);

		modelBuilder.AddInboxStateEntity();
		modelBuilder.AddOutboxMessageEntity();
		modelBuilder.AddOutboxStateEntity();

		base.OnModelCreating(modelBuilder);
	}
}
