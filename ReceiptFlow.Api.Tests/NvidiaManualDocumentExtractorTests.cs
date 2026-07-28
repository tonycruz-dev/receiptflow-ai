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

public sealed class NvidiaManualDocumentExtractorTests
{
	[Fact]
	public async Task TextPdf_MapsManualMetadataAndSections()
	{
		var handler = new FakeNvidiaHandler(SuccessResponse());
		var extractor = CreateExtractor(handler);

		var result = await extractor.ExtractAsync(
			new MemoryStream(CreateTextPdf(pageCount: 1)),
			CancellationToken.None);

		Assert.Equal("Acme", result.Metadata.Manufacturer);
		Assert.Equal("Toaster", result.Metadata.ProductName);
		Assert.Equal("TX-100", result.Metadata.ModelNumber);
		Assert.Equal("2.1", result.Metadata.VersionLabel);
		Assert.Equal(24, result.Metadata.WarrantyDurationMonths);
		Assert.Single(result.Sections);
		Assert.Equal(1, result.PageCount);
		Assert.Contains("Acme TX-100 product manual", handler.RequestBody);
		Assert.Equal(1, handler.CallCount);
	}

	[Fact]
	public async Task PdfOverPageLimit_IsRejectedBeforeProviderCall()
	{
		var handler = new FakeNvidiaHandler(SuccessResponse());
		var extractor = CreateExtractor(handler, maximumPages: 1);

		var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => extractor.ExtractAsync(
				new MemoryStream(CreateTextPdf(pageCount: 2)),
				CancellationToken.None));

		Assert.False(exception.IsTransient);
		Assert.Contains("page limit", exception.Message);
		Assert.Equal(0, handler.CallCount);
	}

	[Fact]
	public async Task PdfOverContentByteLimit_IsRejectedBeforeProviderCall()
	{
		var handler = new FakeNvidiaHandler(SuccessResponse());
		var extractor = CreateExtractor(handler, maximumFileBytes: 16);

		var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => extractor.ExtractAsync(
				new MemoryStream(CreateTextPdf(pageCount: 1)),
				CancellationToken.None));

		Assert.False(exception.IsTransient);
		Assert.Contains("file content", exception.Message);
		Assert.Equal(0, handler.CallCount);
	}

	[Fact]
	public async Task EncryptedPdf_IsRejectedBeforeProviderCall()
	{
		var handler = new FakeNvidiaHandler(SuccessResponse());
		var extractor = CreateExtractor(handler);

		var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => extractor.ExtractAsync(
				new MemoryStream(CreateEncryptedPdf()),
				CancellationToken.None));

		Assert.False(exception.IsTransient);
		Assert.Contains("Password-protected", exception.Message);
		Assert.Equal(0, handler.CallCount);
	}

	private static IManualDocumentExtractor CreateExtractor(
		FakeNvidiaHandler handler,
		int maximumPages = 100,
		long maximumFileBytes = 10 * 1024 * 1024)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AIProviders:Extraction"] = "Nvidia",
				["AIProviders:Embeddings"] = "Nvidia",
				["AIProviders:AnswerGeneration"] = "None",
				["Nvidia:Endpoint"] = "https://example.test/v1",
				["Nvidia:Model"] = "test-manual-model",
				["Nvidia:ApiKey"] = "test-key",
				["Nvidia:MaxPdfPages"] = "5",
				["ManualExtraction:MaximumFileBytes"] = maximumFileBytes.ToString(),
				["ManualExtraction:MaximumPages"] = maximumPages.ToString(),
				["ManualExtraction:MaximumExtractedCharacters"] = "500000",
				["ManualExtraction:MaximumSections"] = "500",
				["ManualExtraction:MaximumSectionCharacters"] = "50000",
				["ManualExtraction:MaximumRenderedImageBytes"] = "20971520",
				["ManualExtraction:ProcessingTimeoutSeconds"] = "180"
			})
			.Build();

		var services = new ServiceCollection();
		services.AddDocumentExtraction(configuration);
		services.AddHttpClient("NvidiaManualDocumentExtractor")
			.ConfigureAdditionalHttpMessageHandlers((handlers, _) =>
				handlers.Clear())
			.ConfigurePrimaryHttpMessageHandler(() => handler);

		return services
			.BuildServiceProvider()
			.GetRequiredService<IManualDocumentExtractor>();
	}

	private static byte[] CreateTextPdf(int pageCount)
	{
		var builder = new PdfDocumentBuilder();
		var font = builder.AddStandard14Font(Standard14Font.Helvetica);
		for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
		{
			var page = builder.AddPage(PageSize.A4);
			page.AddText(
				$"Acme TX-100 product manual page {pageNumber} warranty 24 months",
				12,
				new PdfPoint(50, 750),
				font);
		}

		return builder.Build();
	}

	private static byte[] CreateEncryptedPdf()
	{
		var objects = new[]
		{
			"<< /Type /Catalog /Pages 2 0 R >>",
			"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
			"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
			"<< /Filter /Standard /V 1 /R 2 /Length 40 " +
			"/O <0000000000000000000000000000000000000000000000000000000000000000> " +
			"/U <0000000000000000000000000000000000000000000000000000000000000000> " +
			"/P -4 >>"
		};
		var builder = new StringBuilder("%PDF-1.4\n");
		var offsets = new List<int> { 0 };
		for (var index = 0; index < objects.Length; index++)
		{
			offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
			builder.Append(index + 1)
				.Append(" 0 obj\n")
				.Append(objects[index])
				.Append("\nendobj\n");
		}

		var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
		builder.Append("xref\n0 5\n")
			.Append("0000000000 65535 f \n");
		foreach (var offset in offsets.Skip(1))
			builder.Append(offset.ToString("0000000000")).Append(" 00000 n \n");
		builder.Append(
				"trailer\n<< /Size 5 /Root 1 0 R /Encrypt 4 0 R " +
				"/ID [<00112233445566778899AABBCCDDEEFF><00112233445566778899AABBCCDDEEFF>] >>\n")
			.Append("startxref\n")
			.Append(xrefOffset)
			.Append("\n%%EOF");

		return Encoding.ASCII.GetBytes(builder.ToString());
	}

	private static string SuccessResponse()
	{
		var content =
			"""
			{
			  "manufacturer": "Acme",
			  "productName": "Toaster",
			  "modelNumber": "TX-100",
			  "versionLabel": "2.1",
			  "warrantyDurationMonths": 24,
			  "sections": [
			    {
			      "headingPath": "Safety",
			      "pageStart": 1,
			      "pageEnd": 1,
			      "content": "Disconnect before cleaning."
			    }
			  ],
			  "confidence": 0.96
			}
			""";
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

	private sealed class FakeNvidiaHandler(
		string responseBody,
		HttpStatusCode statusCode = HttpStatusCode.OK)
		: HttpMessageHandler
	{
		public int CallCount { get; private set; }

		public string RequestBody { get; private set; } = string.Empty;

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			CallCount++;
			RequestBody = request.Content is null
				? string.Empty
				: await request.Content.ReadAsStringAsync(cancellationToken);
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
