using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ReceiptFlow.Application.Abstractions.Assistant;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Application.Assistant.Receipts;
using ReceiptFlow.Application.Search.Receipts;
using ReceiptFlow.Mcp.Authentication;

namespace ReceiptFlow.Mcp.Tools;

[McpServerToolType]
[Authorize]
public sealed class ReceiptTools(
	McpRequestUserContext userContext,
	ReceiptSearchHandler searchHandler,
	AskReceiptQuestionHandler answerHandler,
	ILogger<ReceiptTools> logger)
{
	[McpServerTool(
		Name = "search_receipts",
		ReadOnly = true,
		Idempotent = true,
		UseStructuredContent = true)]
	[Description("Searches only the authenticated user's receipts using tenant-isolated hybrid retrieval.")]
	public Task<ReceiptSearchResponse> SearchReceiptsAsync(
		ClaimsPrincipal user,
		[Description("Receipt search text, from 1 to 1000 characters.")] string query,
		[Description("One-based result page.")] int page = 1,
		[Description("Results per page, from 1 to 50.")] int pageSize = 10,
		CancellationToken cancellationToken = default) =>
		ExecuteAsync(
			"search_receipts",
			user,
			() => searchHandler.HandleAsync(
				new ReceiptSearchRequest(query, page, pageSize),
				cancellationToken));

	[McpServerTool(
		Name = "ask_receipts",
		ReadOnly = true,
		Idempotent = true,
		UseStructuredContent = true)]
	[Description("Answers a question using grounded evidence only from the authenticated user's receipts, with trusted citations.")]
	public Task<AskReceiptQuestionResponse> AskReceiptsAsync(
		ClaimsPrincipal user,
		[Description("Question about the authenticated user's receipts, from 1 to 1000 characters.")] string question,
		CancellationToken cancellationToken = default) =>
		ExecuteAsync(
			"ask_receipts",
			user,
			() => answerHandler.HandleAsync(
				new AskReceiptQuestionRequest(question),
				cancellationToken));

	private async Task<T> ExecuteAsync<T>(
		string toolName,
		ClaimsPrincipal user,
		Func<Task<T>> action)
	{
		userContext.SetPrincipal(user);
		var started = Stopwatch.GetTimestamp();
		var subject = MinimizeSubject(userContext.UserId);
		var correlationId = Activity.Current?.TraceId.ToString() ?? "not-provided";

		try
		{
			var result = await action();
			logger.LogInformation(
				"MCP tool {ToolName} completed in {DurationMs} ms for subject {Subject}, status {Status}, correlation {CorrelationId}.",
				toolName,
				Stopwatch.GetElapsedTime(started).TotalMilliseconds,
				subject,
				"success",
				correlationId);
			return result;
		}
		catch (ReceiptSearchValidationException exception)
		{
			LogFailure(toolName, started, subject, correlationId, "validation");
			throw new McpException(exception.Message);
		}
		catch (ReceiptQuestionValidationException exception)
		{
			LogFailure(toolName, started, subject, correlationId, "validation");
			throw new McpException(exception.Message);
		}
		catch (Exception exception)
			when (exception is SearchIndexingException or ReceiptAnswerGenerationException)
		{
			LogFailure(toolName, started, subject, correlationId, "dependency-unavailable");
			throw new McpException("A receipt dependency is temporarily unavailable.");
		}
	}

	private void LogFailure(
		string toolName,
		long started,
		string subject,
		string correlationId,
		string status) =>
		logger.LogWarning(
			"MCP tool {ToolName} completed in {DurationMs} ms for subject {Subject}, status {Status}, correlation {CorrelationId}.",
			toolName,
			Stopwatch.GetElapsedTime(started).TotalMilliseconds,
			subject,
			status,
			correlationId);

	private static string MinimizeSubject(string subject)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(subject));
		return Convert.ToHexString(hash)[..12];
	}
}
