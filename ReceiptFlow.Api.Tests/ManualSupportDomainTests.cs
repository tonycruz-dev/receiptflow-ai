using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Api.Tests;

public sealed class ManualSupportDomainTests
{
	[Fact]
	public void Product_NormalizesOwnerScopedIdentity()
	{
		var product = new Product(
			"owner-a",
			"  Acme   Industries ",
			" Toaster ",
			" tx-100 ");

		Assert.Equal("owner-a", product.OwnerUserId);
		Assert.Equal("Acme   Industries", product.Manufacturer);
		Assert.Equal("Toaster", product.Name);
		Assert.Equal("tx-100", product.ModelNumber);
		Assert.Equal("ACME INDUSTRIES", product.NormalizedManufacturer);
		Assert.Equal("TX-100", product.NormalizedModelNumber);
	}

	[Fact]
	public void ManualVersion_RequiresOwnedPdfManualDocument()
	{
		var product = CreateProduct("owner-a");
		var otherOwnerDocument = CreateManualDocument("owner-b");
		var receiptDocument = new Document(
			"owner-a",
			"receipt.pdf",
			"receipt-key",
			"application/pdf",
			100,
			DocumentType.ReceiptPdf);
		var imageManual = new Document(
			"owner-a",
			"manual.pdf",
			"image-key",
			"image/png",
			100,
			DocumentType.ProductManual);

		Assert.Throws<InvalidOperationException>(() =>
			product.AddManualVersion(otherOwnerDocument));
		Assert.Throws<InvalidOperationException>(() =>
			product.AddManualVersion(receiptDocument));
		Assert.Throws<InvalidOperationException>(() =>
			product.AddManualVersion(imageManual));
	}

	[Fact]
	public void Receipt_RejectsProductManualDocument()
	{
		var receipt = CreateReceipt("owner-a");
		var document = CreateManualDocument("owner-a");

		Assert.Throws<InvalidOperationException>(() => receipt.AddDocument(document));
		Assert.Null(document.ReceiptId);
	}

	[Fact]
	public void ReplacementVersion_ReusesProductAndSupersedesOnlyOnActivation()
	{
		var product = CreateProduct("owner-a");
		var original = product.AddManualVersion(CreateManualDocument("owner-a", "v1.pdf"));
		original.MarkReviewRequired();
		product.ActivateManualVersion(original.Id, "1.0", 24);

		var replacement = product.AddManualVersion(
			CreateManualDocument("owner-a", "v2.pdf"),
			original);

		Assert.Equal(product.Id, original.ProductId);
		Assert.Equal(product.Id, replacement.ProductId);
		Assert.Equal(original.Id, replacement.SupersedesProductManualId);
		Assert.Equal(ProductManualLifecycleStatus.Active, original.LifecycleStatus);
		Assert.Equal(ProductManualLifecycleStatus.Processing, replacement.LifecycleStatus);

		replacement.MarkReviewRequired();
		product.ActivateManualVersion(replacement.Id, "2.0", 36);

		Assert.Equal(ProductManualLifecycleStatus.Superseded, original.LifecycleStatus);
		Assert.NotNull(original.SupersededAtUtc);
		Assert.Equal(ProductManualLifecycleStatus.Active, replacement.LifecycleStatus);
		Assert.Equal("2.0", replacement.VersionLabel);
		Assert.Equal(36, replacement.WarrantyDurationMonths);
		Assert.Single(product.Manuals, manual => manual.LifecycleStatus == ProductManualLifecycleStatus.Active);
	}

	[Fact]
	public void ReplacementVersion_RejectsAnotherProductsManual()
	{
		var firstProduct = CreateProduct("owner-a", "TX-100");
		var secondProduct = CreateProduct("owner-a", "TX-200");
		var firstManual = firstProduct.AddManualVersion(CreateManualDocument("owner-a"));
		firstProduct.ActivateManualVersion(firstManual.Id, "1.0", 12);

		Assert.Throws<InvalidOperationException>(() =>
			secondProduct.AddManualVersion(
				CreateManualDocument("owner-a", "replacement.pdf"),
				firstManual));
	}

	[Fact]
	public void Purchase_RequiresConfirmedSameOwnerReceiptAndMatchingLineItem()
	{
		var product = CreateProduct("owner-a");
		var otherOwnerReceipt = CreateReceipt("owner-b");
		var draftReceipt = Receipt.CreateDraft("owner-a");
		var receipt = CreateReceipt("owner-a");
		var differentReceipt = CreateReceipt("owner-a");
		var unrelatedItem = differentReceipt.AddLineItem("Toaster", 1, 49.99m);

		Assert.Throws<InvalidOperationException>(() => product.LinkPurchase(otherOwnerReceipt));
		Assert.Throws<InvalidOperationException>(() => product.LinkPurchase(draftReceipt));
		Assert.Throws<InvalidOperationException>(() => product.LinkPurchase(receipt, unrelatedItem));
	}

	[Fact]
	public void WarrantyExpiry_UsesConfirmedReceiptDateAndPinnedManualDuration()
	{
		var purchaseDate = new DateTimeOffset(2024, 1, 31, 10, 30, 0, TimeSpan.Zero);
		var receipt = new Receipt(
			"owner-a",
			"Acme Store",
			purchaseDate,
			49.99m);
		var product = CreateProduct("owner-a");
		var original = product.AddManualVersion(CreateManualDocument("owner-a", "v1.pdf"));
		product.ActivateManualVersion(original.Id, "1.0", 1);
		var purchase = product.LinkPurchase(receipt, warrantySource: original);

		var replacement = product.AddManualVersion(
			CreateManualDocument("owner-a", "v2.pdf"),
			original);
		product.ActivateManualVersion(replacement.Id, "2.0", 36);

		Assert.Equal(original.Id, purchase.WarrantySourceProductManualId);
		Assert.Equal(1, purchase.WarrantyDurationMonthsSnapshot);
		Assert.Equal(
			new DateTimeOffset(2024, 2, 29, 10, 30, 0, TimeSpan.Zero),
			purchase.CalculateWarrantyExpiry());
	}

	private static Product CreateProduct(
		string ownerUserId,
		string modelNumber = "TX-100") =>
		new(ownerUserId, "Acme", "Toaster", modelNumber);

	private static Receipt CreateReceipt(string ownerUserId) =>
		new(
			ownerUserId,
			"Acme Store",
			DateTimeOffset.UtcNow.AddDays(-1),
			49.99m);

	private static Document CreateManualDocument(
		string ownerUserId,
		string fileName = "manual.pdf") =>
		new(
			ownerUserId,
			fileName,
			$"manuals/{Guid.NewGuid():N}",
			"application/pdf",
			100,
			DocumentType.ProductManual);
}
