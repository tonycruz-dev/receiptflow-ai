using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Domain.Entities;

using ReceiptFlow.Application.Dashboard;
using ReceiptFlow.Application.Receipts;
using ReceiptFlow.Application.Receipts.ListReceipts;
using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Infrastructure.Persistence.Repositories;

internal sealed class ReceiptRepository(
	ApplicationDbContext dbContext)
	: IReceiptRepository
{
	public async Task AddAsync(
		Receipt receipt,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(receipt);

		await dbContext.Receipts.AddAsync(
			receipt,
			cancellationToken);
	}

	public Task<Receipt?> GetByIdAsync(
		Guid id,
		string ownerUserId,
		CancellationToken cancellationToken = default)
	{
		return dbContext.Receipts
			.AsNoTracking()
			.Include(receipt => receipt.LineItems)
			.Include(receipt => receipt.Documents)
			.SingleOrDefaultAsync(
				receipt =>
					receipt.Id == id &&
					receipt.OwnerUserId == ownerUserId,
				cancellationToken);
	}

	public Task<Receipt?> GetByIdForUpdateAsync(
		Guid id,
		string ownerUserId,
		CancellationToken cancellationToken = default)
	{
		return dbContext.Receipts
			.Include(receipt => receipt.LineItems)
			.Include(receipt => receipt.Documents)
				.ThenInclude(document => document.Extraction)
			.SingleOrDefaultAsync(
				receipt =>
					receipt.Id == id &&
					receipt.OwnerUserId == ownerUserId,
				cancellationToken);
	}

	public async Task<IReadOnlyList<Receipt>> GetAllAsync(
		string ownerUserId,
		CancellationToken cancellationToken = default)
	{
		return await dbContext.Receipts
			.AsNoTracking()
			.Where(receipt =>
				receipt.OwnerUserId == ownerUserId)
			.OrderByDescending(receipt =>
				receipt.PurchaseDate ?? receipt.CreatedAtUtc)
			.ToListAsync(cancellationToken);
	}

	public async Task<DashboardResponse> GetDashboardAsync(
		string ownerUserId,
		int recentReceiptLimit,
		CancellationToken cancellationToken = default)
	{
		var ownedReceipts = dbContext.Receipts
			.AsNoTracking()
			.Where(receipt => receipt.OwnerUserId == ownerUserId);

		var totalReceipts = await ownedReceipts.CountAsync(cancellationToken);
		var spendingTotals = await ownedReceipts
			.Where(receipt =>
				receipt.LifecycleStatus == ReceiptLifecycleStatus.Confirmed &&
				receipt.Currency != null &&
				receipt.TotalAmount != null)
			.GroupBy(receipt => receipt.Currency)
			.Select(group => new
			{
				Currency = group.Key,
				Amount = group.Sum(receipt => receipt.TotalAmount!.Value)
			})
			.OrderBy(total => total.Currency)
			.ToListAsync(cancellationToken);
		var spendingByCurrency = spendingTotals
			.Select(total => new CurrencyAmountResponse(
					total.Currency!,
				total.Amount))
			.ToList();
		var documentsProcessing = await dbContext.Documents
			.AsNoTracking()
			.CountAsync(
				document =>
					document.OwnerUserId == ownerUserId &&
					(document.ProcessingStatus == DocumentProcessingStatus.Pending ||
					 document.ProcessingStatus == DocumentProcessingStatus.Queued ||
					 document.ProcessingStatus == DocumentProcessingStatus.Processing),
				cancellationToken);
		var recentReceipts = await ProjectReceiptSummaries(ownedReceipts)
			.Take(recentReceiptLimit)
			.ToListAsync(cancellationToken);

		return new DashboardResponse(
			totalReceipts,
			spendingByCurrency,
			documentsProcessing,
			recentReceipts.Select(MapSummary).ToList());
	}

	public async Task<ReceiptListResponse> GetPageAsync(
		string ownerUserId,
		int page,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var ownedReceipts = dbContext.Receipts
			.AsNoTracking()
			.Where(receipt => receipt.OwnerUserId == ownerUserId);
		var total = await ownedReceipts.CountAsync(cancellationToken);
		var receipts = await ProjectReceiptSummaries(ownedReceipts)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		return new ReceiptListResponse(
			page,
			pageSize,
			total,
			receipts.Select(MapSummary).ToList());
	}

	private static IQueryable<ReceiptSummaryProjection> ProjectReceiptSummaries(
		IQueryable<Receipt> receipts)
	{
		return receipts
			.OrderByDescending(receipt => receipt.PurchaseDate ?? receipt.CreatedAtUtc)
			.ThenByDescending(receipt => receipt.CreatedAtUtc)
			.ThenByDescending(receipt => receipt.Id)
			.Select(receipt => new ReceiptSummaryProjection(
				receipt.Id,
				receipt.MerchantName,
				receipt.PurchaseDate,
				receipt.TotalAmount,
				receipt.Currency,
				receipt.Category,
				receipt.LifecycleStatus,
				receipt.Documents
					.OrderByDescending(document => document.CreatedAtUtc)
					.ThenByDescending(document => document.Id)
					.Select(document => (Guid?)document.Id)
					.FirstOrDefault(),
				receipt.Documents
					.OrderByDescending(document => document.CreatedAtUtc)
					.ThenByDescending(document => document.Id)
					.Select(document => document.OriginalFileName)
					.FirstOrDefault(),
				receipt.Documents
					.OrderByDescending(document => document.CreatedAtUtc)
					.ThenByDescending(document => document.Id)
					.Select(document => (DocumentProcessingStatus?)document.ProcessingStatus)
					.FirstOrDefault()));
	}

	private static ReceiptSummaryResponse MapSummary(
		ReceiptSummaryProjection receipt)
	{
		return new ReceiptSummaryResponse(
			receipt.ReceiptId,
			receipt.MerchantName,
			receipt.PurchaseDate,
			receipt.TotalAmount,
			receipt.Currency,
			receipt.Category,
			receipt.LifecycleStatus.ToString(),
			receipt.DocumentId,
			receipt.OriginalFileName,
			receipt.ProcessingStatus?.ToString());
	}

	private sealed record ReceiptSummaryProjection(
		Guid ReceiptId,
		string? MerchantName,
		DateTimeOffset? PurchaseDate,
		decimal? TotalAmount,
		string? Currency,
		string? Category,
		ReceiptLifecycleStatus LifecycleStatus,
		Guid? DocumentId,
		string? OriginalFileName,
		DocumentProcessingStatus? ProcessingStatus);
}
