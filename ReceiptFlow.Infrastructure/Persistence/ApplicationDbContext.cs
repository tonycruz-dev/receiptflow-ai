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
