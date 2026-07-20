using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.Api.Tests;

public sealed class ManualSupportPersistenceTests
{
	[Fact]
	public async Task ProductGraph_PersistsManualAndPurchaseRelationships()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		var product = new Product("owner-a", "Acme", "Toaster", "TX-100");
		var document = new Document(
			"owner-a",
			"manual.pdf",
			"manual-key",
			"application/pdf",
			100,
			DocumentType.ProductManual);
		var manual = product.AddManualVersion(document);
		product.ActivateManualVersion(manual.Id, "1.0", 24);
		var receipt = new Receipt(
			"owner-a",
			"Acme Store",
			DateTimeOffset.UtcNow.AddDays(-1),
			49.99m);
		var lineItem = receipt.AddLineItem("Toaster", 1, 49.99m);
		var purchase = product.LinkPurchase(receipt, lineItem, warrantySource: manual);

		await using (var writeContext = new ApplicationDbContext(options))
		{
			writeContext.Products.Add(product);
			await writeContext.SaveChangesAsync();
		}

		await using var readContext = new ApplicationDbContext(options);
		var stored = await readContext.Products
			.Include(candidate => candidate.Manuals)
				.ThenInclude(candidate => candidate.Document)
			.Include(candidate => candidate.Purchases)
				.ThenInclude(candidate => candidate.Receipt)
			.SingleAsync(candidate => candidate.Id == product.Id);

		var storedManual = Assert.Single(stored.Manuals);
		var storedPurchase = Assert.Single(stored.Purchases);
		Assert.Equal(document.Id, storedManual.DocumentId);
		Assert.Equal(DocumentType.ProductManual, storedManual.Document.DocumentType);
		Assert.Equal(receipt.Id, storedPurchase.ReceiptId);
		Assert.Equal(lineItem.Id, storedPurchase.ReceiptLineItemId);
		Assert.Equal(manual.Id, storedPurchase.WarrantySourceProductManualId);
		Assert.Equal(purchase.CalculateWarrantyExpiry(), storedPurchase.CalculateWarrantyExpiry());
	}

	[Fact]
	public void Model_UsesOwnerAwareForeignKeysForTenantBoundaries()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseNpgsql("Host=localhost;Database=receiptflow_model;Username=model;Password=model")
			.Options;
		using var context = new ApplicationDbContext(options);

		var manualType = context.Model.FindEntityType(typeof(ProductManual));
		var purchaseType = context.Model.FindEntityType(typeof(Purchase));
		Assert.NotNull(manualType);
		Assert.NotNull(purchaseType);

		Assert.Contains(
			manualType.GetForeignKeys(),
			foreignKey => PropertyNames(foreignKey.Properties) is "DocumentId,OwnerUserId");
		Assert.Contains(
			manualType.GetForeignKeys(),
			foreignKey => PropertyNames(foreignKey.Properties) is "ProductId,OwnerUserId");
		Assert.Contains(
			purchaseType.GetForeignKeys(),
			foreignKey => PropertyNames(foreignKey.Properties) is "ReceiptId,OwnerUserId");
		Assert.Contains(
			purchaseType.GetForeignKeys(),
			foreignKey => PropertyNames(foreignKey.Properties) is "ProductId,OwnerUserId");
		Assert.Contains(
			purchaseType.GetForeignKeys(),
			foreignKey => PropertyNames(foreignKey.Properties) is "WarrantySourceProductManualId,ProductId,OwnerUserId");
	}

	[Fact]
	public void Migrations_MatchTheCurrentPersistenceModel()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseNpgsql("Host=localhost;Database=receiptflow_model;Username=model;Password=model")
			.Options;
		using var context = new ApplicationDbContext(options);

		Assert.False(context.Database.HasPendingModelChanges());
	}

	private static string PropertyNames(
		IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IProperty> properties) =>
		string.Join(',', properties.Select(property => property.Name));
}
