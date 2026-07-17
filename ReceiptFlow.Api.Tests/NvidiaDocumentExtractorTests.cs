using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReceiptFlow.Application.Abstractions.Extraction;
using ReceiptFlow.Infrastructure;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ReceiptFlow.Api.Tests;

public sealed class NvidiaDocumentExtractorTests
{
	[Fact]
	public async Task JpegStructuredExtraction_MapsResponse()
	{
		var handler = new FakeNvidiaHandler(SuccessResponse());
		var extractor = CreateExtractor(handler);

		var result = await extractor.ExtractAsync(
			new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]),
			"image/jpeg",
			CancellationToken.None);

		Assert.Equal("NvidiaNIM", result.Provider);
		Assert.Equal("test-vision-model", result.ModelId);
		Assert.Equal("Corner Shop", result.Fields.MerchantName);
		Assert.Equal(12, result.Fields.Total);
		Assert.Single(result.LineItems);
		Assert.Contains("data:image/jpeg;base64,", handler.RequestBody);
		Assert.Equal(1, handler.CallCount);
	}

	[Fact]
	public async Task PngStructuredExtraction_IncludesPngMimeType()
	{
		var handler = new FakeNvidiaHandler(SuccessResponse());
		var extractor = CreateExtractor(handler);

		await extractor.ExtractAsync(
			new MemoryStream([0x89, 0x50, 0x4E, 0x47]),
			"image/png",
			CancellationToken.None);

		Assert.Contains("data:image/png;base64,", handler.RequestBody);
	}

	[Fact]
	public async Task TextBasedPdf_ExtractsTextLocally()
	{
		var handler = new FakeNvidiaHandler(SuccessResponse());
		var extractor = CreateExtractor(handler);

		await extractor.ExtractAsync(
			new MemoryStream(CreateTextPdf()),
			"application/pdf",
			CancellationToken.None);

		Assert.Contains("Corner Shop embedded receipt text", handler.RequestBody);
		Assert.DoesNotContain("image_url", handler.RequestBody);
	}

	[Fact]
	public async Task ScannedPdf_RendersPagesAsImages()
	{
		var handler = new FakeNvidiaHandler(SuccessResponse());
		var extractor = CreateExtractor(handler);

		await extractor.ExtractAsync(
			new MemoryStream(CreateBlankPdf()),
			"application/pdf",
			CancellationToken.None);

		Assert.Contains("image_url", handler.RequestBody);
		Assert.Contains("data:image/png;base64,", handler.RequestBody);
	}

	[Fact]
	public async Task UnknownValues_RemainNull()
	{
		var handler = new FakeNvidiaHandler(SuccessResponse(
			"""
			{
			  "merchantName": null,
			  "transactionDate": null,
			  "subtotal": null,
			  "tax": null,
			  "total": null,
			  "currency": null,
			  "items": [],
			  "rawText": "unreadable receipt",
			  "confidence": 0.1
			}
			"""));
		var extractor = CreateExtractor(handler);

		var result = await extractor.ExtractAsync(
			new MemoryStream([0xFF, 0xD8]),
			"image/jpeg",
			CancellationToken.None);

		Assert.Null(result.Fields.MerchantName);
		Assert.Null(result.Fields.TransactionDate);
		Assert.Null(result.Fields.Total);
		Assert.Empty(result.LineItems);
	}

	[Fact]
	public async Task MalformedResponse_FailsSafely()
	{
		var handler = new FakeNvidiaHandler(ChatResponse("{not-json"));
		var extractor = CreateExtractor(handler);

		var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => extractor.ExtractAsync(
				new MemoryStream([0xFF, 0xD8]),
				"image/jpeg",
				CancellationToken.None));

		Assert.False(exception.IsTransient);
	}

	[Fact]
	public async Task OversizedResponse_FailsSafely()
	{
		var handler = new FakeNvidiaHandler(
			new string('x', 1_000_001));
		var extractor = CreateExtractor(handler);

		var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => extractor.ExtractAsync(
				new MemoryStream([0xFF, 0xD8]),
				"image/jpeg",
				CancellationToken.None));

		Assert.False(exception.IsTransient);
	}

	[Theory]
	[InlineData(HttpStatusCode.TooManyRequests)]
	[InlineData(HttpStatusCode.InternalServerError)]
	public async Task RetryableHttpFailures_AreTransient(
		HttpStatusCode statusCode)
	{
		var handler = new FakeNvidiaHandler("", statusCode);
		var extractor = CreateExtractor(handler);

		var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => extractor.ExtractAsync(
				new MemoryStream([0xFF, 0xD8]),
				"image/jpeg",
				CancellationToken.None));

		Assert.True(exception.IsTransient);
	}

	[Fact]
	public async Task Permanent4xx_IsNotRetriedByExtractor()
	{
		var handler = new FakeNvidiaHandler("", HttpStatusCode.BadRequest);
		var extractor = CreateExtractor(handler);

		var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => extractor.ExtractAsync(
				new MemoryStream([0xFF, 0xD8]),
				"image/jpeg",
				CancellationToken.None));

		Assert.False(exception.IsTransient);
		Assert.Equal(1, handler.CallCount);
	}

	private static IDocumentExtractor CreateExtractor(
		FakeNvidiaHandler handler)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Nvidia:Endpoint"] = "https://example.test/v1",
				["Nvidia:Model"] = "test-vision-model",
				["Nvidia:ApiKey"] = "test-key",
				["Nvidia:MaxPdfPages"] = "5",
				["Nvidia:MinimumConfidence"] = "0.70"
			})
			.Build();

		var services = new ServiceCollection();
		services.AddDocumentExtraction(configuration);
		services.AddHttpClient("NvidiaDocumentExtractor")
			.ConfigurePrimaryHttpMessageHandler(() => handler);

		return services
			.BuildServiceProvider()
			.GetRequiredService<IDocumentExtractor>();
	}

	private static string SuccessResponse(string? payload = null)
	{
		return ChatResponse(payload ??
			"""
			{
			  "merchantName": "Corner Shop",
			  "transactionDate": "2026-07-17",
			  "subtotal": 10.0,
			  "tax": 2.0,
			  "total": 12.0,
			  "currency": "gbp",
			  "items": [
			    {
			      "description": "Milk",
			      "quantity": 1,
			      "unitPrice": 2.0,
			      "totalPrice": 2.0,
			      "confidence": 0.95
			    }
			  ],
			  "rawText": "Corner Shop Milk Total 12.00",
			  "confidence": 0.98
			}
			""");
	}

	private static string ChatResponse(string content)
	{
		return JsonSerializer.Serialize(new
		{
			choices = new[]
			{
				new
				{
					message = new
					{
						content
					}
				}
			}
		});
	}

	private static byte[] CreateTextPdf()
	{
		var builder = new PdfDocumentBuilder();
		var font = builder.AddStandard14Font(Standard14Font.Helvetica);
		var page = builder.AddPage(PageSize.A4);
		page.AddText(
			"Corner Shop embedded receipt text total 12.00 tax 2.00",
			12,
			new PdfPoint(50, 750),
			font);

		return builder.Build();
	}

	private static byte[] CreateBlankPdf()
	{
		var builder = new PdfDocumentBuilder();
		builder.AddPage(PageSize.A4);

		return builder.Build();
	}

	private sealed class FakeNvidiaHandler(
		string responseBody,
		HttpStatusCode statusCode = HttpStatusCode.OK)
		: HttpMessageHandler
	{
		public string RequestBody { get; private set; } = string.Empty;

		public int CallCount { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			CallCount++;
			RequestBody = request.Content is null
				? string.Empty
				: await request.Content.ReadAsStringAsync(
					cancellationToken);

			Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
			Assert.Equal(
				"https://example.test/v1/chat/completions",
				request.RequestUri?.ToString());

			return new HttpResponseMessage(statusCode)
			{
				Content = new StringContent(
					responseBody,
					Encoding.UTF8,
					"application/json")
			};
		}
	}
}
