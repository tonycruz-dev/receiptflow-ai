using System.Text.RegularExpressions;
using ReceiptFlow.Application.Abstractions.Assistant;
using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Search.Receipts;

namespace ReceiptFlow.Application.Assistant.Receipts;

public sealed partial class AskReceiptQuestionHandler(
	ICurrentUser currentUser,
	ReceiptSearchHandler searchHandler,
	IReceiptAnswerGenerator answerGenerator)
{
	public const int MaximumQuestionLength = 1000;
	public const int MaximumRetrievedChunks = 5;
	public const int MaximumEvidenceCharacters = 12000;
	private const string NoEvidenceAnswer =
		"I could not find this in your receipts.";

	public async Task<AskReceiptQuestionResponse> HandleAsync(
		AskReceiptQuestionRequest request,
		CancellationToken cancellationToken = default)
	{
		var question = Validate(request);

		if (!currentUser.IsAuthenticated ||
			string.IsNullOrWhiteSpace(currentUser.UserId))
		{
			throw new UnauthorizedAccessException();
		}

		var search = await searchHandler.HandleAsync(
			new ReceiptSearchRequest(
				question,
				Page: 1,
				PageSize: MaximumRetrievedChunks),
			cancellationToken);

		var selected = SelectEvidence(search.Matches);
		if (selected.Count == 0)
			return new AskReceiptQuestionResponse(NoEvidenceAnswer, []);

		var generated = await answerGenerator.GenerateAsync(
			question,
			selected.Select(item => item.Evidence).ToArray(),
			cancellationToken);

		var allowed = selected
			.Select(item => item.Evidence.Citation)
			.ToHashSet();
		var declared = generated.CitationIdentifiers
			.Where(allowed.Contains)
			.ToHashSet();
		var answer = RemoveUnknownCitations(generated.Answer, declared);
		var citedInAnswer = CitationPattern()
			.Matches(answer)
			.Select(match => int.Parse(match.Groups[1].Value))
			.Where(citation => allowed.Contains(citation) && declared.Contains(citation))
			.ToHashSet();

		var sources = selected
			.Where(item => citedInAnswer.Contains(item.Evidence.Citation))
			.Select(item => item.Source)
			.ToArray();

		return new AskReceiptQuestionResponse(answer.Trim(), sources);
	}

	private static IReadOnlyList<SelectedEvidence> SelectEvidence(
		IReadOnlyList<ReceiptSearchMatchResponse> matches)
	{
		var selected = new List<SelectedEvidence>();
		var citations = new Dictionary<(Guid ReceiptId, Guid DocumentId), int>();
		var usedCharacters = 0;

		foreach (var match in matches.Take(MaximumRetrievedChunks))
		{
			var key = (match.ReceiptId, match.DocumentId);
			var separatorCharacters = citations.ContainsKey(key) ? 1 : 0;
			var remaining = MaximumEvidenceCharacters - usedCharacters - separatorCharacters;
			if (remaining <= 0)
				break;

			var content = match.Content.Length <= remaining
				? match.Content
				: match.Content[..remaining];
			if (string.IsNullOrWhiteSpace(content))
				continue;

			if (!citations.TryGetValue(key, out var citation))
			{
				citation = citations.Count + 1;
				citations.Add(key, citation);
			}

			selected.Add(new SelectedEvidence(
				new ReceiptAnswerEvidence(
					citation,
					content,
					match.MerchantName,
					match.TransactionDate,
					match.Total,
					match.Currency),
				new ReceiptAnswerSourceResponse(
					citation,
					match.ReceiptId,
					match.DocumentId,
					match.MerchantName,
					match.TransactionDate,
					match.Total,
					match.Currency)));
			usedCharacters += content.Length + separatorCharacters;
		}

		return selected
			.GroupBy(item => item.Evidence.Citation)
			.Select(group => new SelectedEvidence(
				group.First().Evidence with
				{
					Content = string.Join("\n", group.Select(item => item.Evidence.Content))
				},
				group.First().Source))
			.ToArray();
	}

	private static string Validate(AskReceiptQuestionRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Question))
			throw new ReceiptQuestionValidationException("Question is required.");

		var question = request.Question.Trim();
		if (question.Length > MaximumQuestionLength)
		{
			throw new ReceiptQuestionValidationException(
				$"Question must not exceed {MaximumQuestionLength} characters.");
		}

		return question;
	}

	private static string RemoveUnknownCitations(
		string answer,
		IReadOnlySet<int> allowed) =>
		CitationPattern().Replace(answer ?? string.Empty, match =>
			allowed.Contains(int.Parse(match.Groups[1].Value))
				? match.Value
				: string.Empty);

	[GeneratedRegex(@"\[(\d+)\]", RegexOptions.CultureInvariant)]
	private static partial Regex CitationPattern();

	private sealed record SelectedEvidence(
		ReceiptAnswerEvidence Evidence,
		ReceiptAnswerSourceResponse Source);
}
