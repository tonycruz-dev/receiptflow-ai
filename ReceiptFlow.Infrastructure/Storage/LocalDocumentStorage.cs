using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Storage;

namespace ReceiptFlow.Infrastructure.Storage;

internal sealed class LocalDocumentStorage(
	IOptions<LocalDocumentStorageOptions> options)
	: IDocumentStorage
{
	private const string PlaceholderRoot = "__SET_DOCUMENT_STORAGE_ROOT__";

	private readonly string rootPath = ResolveRootPath(options.Value.RootPath);

	public async Task<StoredDocument> SaveAsync(
		Stream content,
		string fileName,
		string contentType,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);

		var extension = Path.GetExtension(Path.GetFileName(fileName))
			.ToLowerInvariant();
		var storageKey = CreateStorageKey(extension);
		var fullPath = GetFullPath(rootPath, storageKey);
		var directory = Path.GetDirectoryName(fullPath)
			?? throw new InvalidOperationException(
				"The storage path is invalid.");

		Directory.CreateDirectory(directory);

		await using var output = new FileStream(
			fullPath,
			FileMode.CreateNew,
			FileAccess.Write,
			FileShare.None,
			bufferSize: 81920,
			FileOptions.Asynchronous | FileOptions.SequentialScan);

		using var sha256 = SHA256.Create();
		var buffer = new byte[81920];
		long fileSize = 0;

		while (true)
		{
			var bytesRead = await content.ReadAsync(
				buffer,
				cancellationToken);

			if (bytesRead == 0)
				break;

			await output.WriteAsync(
				buffer.AsMemory(0, bytesRead),
				cancellationToken);

			sha256.TransformBlock(
				buffer,
				0,
				bytesRead,
				null,
				0);

			fileSize += bytesRead;
		}

		sha256.TransformFinalBlock([], 0, 0);

		return new StoredDocument(
			storageKey,
			fileSize,
			Convert.ToHexString(sha256.Hash!).ToLowerInvariant());
	}

	public Task DeleteAsync(
		string storageKey,
		CancellationToken cancellationToken)
	{
		var fullPath = GetFullPath(rootPath, storageKey);

		if (File.Exists(fullPath))
			File.Delete(fullPath);

		return Task.CompletedTask;
	}

	private static string CreateStorageKey(string extension)
	{
		var now = DateTimeOffset.UtcNow;

		return string.Join(
			'/',
			now.Year.ToString("0000"),
			now.Month.ToString("00"),
			$"{Guid.NewGuid():N}{extension}");
	}

	private static string ResolveRootPath(string configuredRootPath)
	{
		var rootPath = string.IsNullOrWhiteSpace(configuredRootPath) ||
			string.Equals(
				configuredRootPath,
				PlaceholderRoot,
				StringComparison.Ordinal)
			? Path.Combine(
				Path.GetTempPath(),
				"ReceiptFlow.AI",
				"documents")
			: configuredRootPath;

		return Path.GetFullPath(
			Environment.ExpandEnvironmentVariables(rootPath));
	}

	private static string GetFullPath(
		string rootPath,
		string storageKey)
	{
		if (string.IsNullOrWhiteSpace(storageKey) ||
			Path.IsPathRooted(storageKey))
		{
			throw new InvalidOperationException(
				"The storage key is invalid.");
		}

		var fullPath = Path.GetFullPath(
			Path.Combine(rootPath, storageKey));
		var rootWithSeparator = rootPath.EndsWith(
			Path.DirectorySeparatorChar)
			? rootPath
			: rootPath + Path.DirectorySeparatorChar;

		if (!fullPath.StartsWith(
			rootWithSeparator,
			StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(
				"The storage key is outside the configured root path.");
		}

		return fullPath;
	}
}
