using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ReceiptFlow.Application.Abstractions.Storage;

namespace ReceiptFlow.Infrastructure.Storage;

internal sealed class AzureBlobDocumentStorage(
	BlobContainerClient containerClient)
	: IDocumentStorage
{
	private readonly SemaphoreSlim containerLock = new(1, 1);
	private bool containerInitialized;

	public async Task<StoredDocument> SaveAsync(
		Stream content,
		string fileName,
		string contentType,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);

		await EnsureContainerAsync(cancellationToken);

		var extension = Path.GetExtension(Path.GetFileName(fileName))
			.ToLowerInvariant();
		var storageKey = CreateStorageKey(extension);
		var blobClient = GetBlobClient(storageKey);
		var metadata = new Dictionary<string, string>
		{
			["originalfilename"] =
				Convert.ToBase64String(
					System.Text.Encoding.UTF8.GetBytes(fileName)),
			["storedatutc"] = DateTimeOffset.UtcNow.ToString("O")
		};

		await using var hashingContent =
			new HashingReadStream(content);

		await blobClient.UploadAsync(
			hashingContent,
			new BlobUploadOptions
			{
				HttpHeaders = new BlobHttpHeaders
				{
					ContentType = contentType
				},
				Metadata = metadata
			},
			cancellationToken);

		return new StoredDocument(
			storageKey,
			hashingContent.BytesRead,
			hashingContent.GetHash());
	}

	public async Task DeleteAsync(
		string storageKey,
		CancellationToken cancellationToken)
	{
		await EnsureContainerAsync(cancellationToken);

		await GetBlobClient(storageKey).DeleteIfExistsAsync(
			DeleteSnapshotsOption.IncludeSnapshots,
			cancellationToken: cancellationToken);
	}

	public async Task<Stream> OpenReadAsync(
		string storageKey,
		CancellationToken cancellationToken)
	{
		await EnsureContainerAsync(cancellationToken);

		return await GetBlobClient(storageKey).OpenReadAsync(
			cancellationToken: cancellationToken);
	}

	private async Task EnsureContainerAsync(CancellationToken cancellationToken)
	{
		if (containerInitialized)
			return;

		await containerLock.WaitAsync(cancellationToken);

		try
		{
			if (containerInitialized)
				return;

			await containerClient.CreateIfNotExistsAsync(
				PublicAccessType.None,
				cancellationToken: cancellationToken);

			containerInitialized = true;
		}
		finally
		{
			containerLock.Release();
		}
	}

	private BlobClient GetBlobClient(string storageKey)
	{
		ValidateStorageKey(storageKey);

		return containerClient.GetBlobClient(storageKey);
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

	private static void ValidateStorageKey(string storageKey)
	{
		if (string.IsNullOrWhiteSpace(storageKey) ||
			Path.IsPathRooted(storageKey) ||
			storageKey.Contains('\\') ||
			storageKey.Split('/').Any(static segment =>
				segment is "" or "." or ".."))
		{
			throw new InvalidOperationException(
				"The storage key is invalid.");
		}
	}

	private sealed class HashingReadStream(Stream inner) : Stream
	{
		private readonly SHA256 sha256 = SHA256.Create();
		private bool finalized;

		public long BytesRead { get; private set; }

		public override bool CanRead => inner.CanRead;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public string GetHash()
		{
			if (!finalized)
			{
				sha256.TransformFinalBlock([], 0, 0);
				finalized = true;
			}

			return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
		}

		public override int Read(
			byte[] buffer,
			int offset,
			int count)
		{
			var bytesRead = inner.Read(buffer, offset, count);
			Transform(buffer.AsSpan(offset, bytesRead));

			return bytesRead;
		}

		public override async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default)
		{
			var bytesRead = await inner.ReadAsync(buffer, cancellationToken);
			Transform(buffer.Span[..bytesRead]);

			return bytesRead;
		}

		public override Task<int> ReadAsync(
			byte[] buffer,
			int offset,
			int count,
			CancellationToken cancellationToken)
		{
			return base.ReadAsync(buffer, offset, count, cancellationToken);
		}

		private void Transform(ReadOnlySpan<byte> bytes)
		{
			if (bytes.IsEmpty)
				return;

			sha256.TransformBlock(
				bytes.ToArray(),
				0,
				bytes.Length,
				null,
				0);

			BytesRead += bytes.Length;
		}

		public override void Flush() =>
			throw new NotSupportedException();

		public override long Seek(
			long offset,
			SeekOrigin origin) =>
			throw new NotSupportedException();

		public override void SetLength(long value) =>
			throw new NotSupportedException();

		public override void Write(
			byte[] buffer,
			int offset,
			int count) =>
			throw new NotSupportedException();

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				sha256.Dispose();

			base.Dispose(disposing);
		}

		public override async ValueTask DisposeAsync()
		{
			sha256.Dispose();
			await base.DisposeAsync();
		}
	}
}
