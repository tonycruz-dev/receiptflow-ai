using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReceiptFlow.Application.Abstractions.Assistant;
using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Search.Receipts;

namespace ReceiptFlow.Application.Assistant.Receipts;

public sealed partial class AskReceiptQuestionHandler(
	ICurrentUser currentUser,
	ReceiptSearchHandler searchHandler,
	IReceiptAnswerGenerator answerGenerator,
	ILogger<AskReceiptQuestionHandler> logger)
{
	public const int MaximumQuestionLength = 1000;
	public const int MaximumRetrievedChunks = 5;
	public const int MaximumCandidateChunks = 25;
	public const int MaximumEvidenceCharacters = 12000;
	private const string NoEvidenceAnswer =
		"I could not find this in your receipts or product manuals.";
	private const string UnsupportedAnswer =
		"Relevant material was found, but I could not produce a supported answer.";
	private static readonly HashSet<string> StopWords = new(
		[
			"about", "after", "also", "could", "does", "from", "have",
			"long", "should", "that", "the", "their", "this", "what",
			"when", "where", "which", "with", "would", "your"
		],
		StringComparer.OrdinalIgnoreCase);

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
				PageSize: MaximumCandidateChunks,
				DocumentType: request.DocumentType),
			cancellationToken);

		var selected = SelectEvidence(question, search.Matches);
		if (selected.Count == 0)
			return new AskReceiptQuestionResponse(NoEvidenceAnswer, []);

		var generated = await answerGenerator.GenerateAsync(
			question,
			selected.Select(item => item.Evidence).ToArray(),
			cancellationToken);

		var allowed = selected.ToDictionary(
			item => item.Evidence.CitationToken,
			StringComparer.Ordinal);
		var declared = generated.CitationIdentifiers;
		var validated = declared
			.Where(allowed.ContainsKey)
			.Distinct(StringComparer.Ordinal)
			.ToArray();

		if (validated.Length == 0)
		{
			logger.LogWarning(
				"Receipt answer citation compliance fallback used. Retrieved match count {RetrievedMatchCount}, evidence count {EvidenceCount}, declared citation count {DeclaredCitationCount}, valid citation count 0.",
				search.Matches.Count,
				selected.Count,
				declared.Count);

			if (!TryCreateEvidenceFallback(
					question,
					selected,
					out var fallbackAnswer,
					out var fallbackSource))
			{
				return new AskReceiptQuestionResponse(UnsupportedAnswer, []);
			}

			return new AskReceiptQuestionResponse(
				fallbackAnswer,
				[fallbackSource]);
		}

		var answer = RemoveUnknownCitations(generated.Answer, validated).Trim();
		var citedInAnswer = CitationPattern()
			.Matches(answer)
			.Select(match => $"S{match.Groups[1].Value}")
			.Where(validated.Contains)
			.ToHashSet(StringComparer.Ordinal);
		if (citedInAnswer.Count == 0)
		{
			answer = string.Concat(
				answer,
				" ",
				string.Join(" ", validated.Select(citation => $"[{citation}]")));
		}

		var sources = validated
			.Select(citation => allowed[citation].Source)
			.ToArray();

		logger.LogInformation(
			"Receipt answer citation validation completed. Retrieved match count {RetrievedMatchCount}, evidence count {EvidenceCount}, declared citation count {DeclaredCitationCount}, valid citation count {ValidCitationCount}, returned source count {ReturnedSourceCount}.",
			search.Matches.Count,
			selected.Count,
			declared.Count,
			validated.Length,
			sources.Length);

		return new AskReceiptQuestionResponse(answer, sources);
	}

	private static IReadOnlyList<SelectedEvidence> SelectEvidence(
		string question,
		IReadOnlyList<ReceiptSearchMatchResponse> matches)
	{
		var selected = new List<SelectedEvidence>();
		var citations = new Dictionary<(ReceiptSearchDocumentType DocumentType, Guid DocumentId), int>();
		var usedCharacters = 0;
		var ranked = matches
			.OrderByDescending(match => IsExplicitVersionQuery(question, match))
			.ThenByDescending(match => EvidencePreference(question, match))
			.ThenByDescending(match => ProductAffinity(question, match))
			.ThenByDescending(match => match.DocumentType == ReceiptSearchDocumentType.ProductManual && match.IsActiveManual)
			.ThenByDescending(match => match.RelevanceScore)
			.ToArray();
		var candidates = ranked
			.Take(MaximumRetrievedChunks)
			.ToList();

		if (ManualIntentScore(question) > 0 && ReceiptIntentScore(question) > 0)
		{
			EnsureDocumentType(
				candidates,
				ranked,
				ReceiptSearchDocumentType.ProductManual);
			EnsureDocumentType(
				candidates,
				ranked,
				ReceiptSearchDocumentType.Receipt);
		}

		foreach (var match in candidates)
		{
			var key = (match.DocumentType, match.DocumentId);
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
					match.DocumentType.ToString(),
					match.MerchantName,
					match.TransactionDate,
					match.Total,
					match.Currency,
					match.ProductManufacturer,
					match.ProductName,
					match.ModelNumber,
					match.ManualVersion,
					match.Locale,
					match.WarrantyDurationMonths,
					match.SectionHeading,
					match.IsActiveManual),
				new ReceiptAnswerSourceResponse(
					citation,
					match.DocumentType.ToString(),
					match.ReceiptId,
					match.ProductId,
					match.ProductManualId,
					match.DocumentId,
					match.MerchantName,
					match.TransactionDate,
					match.Total,
					match.Currency,
					match.ProductManufacturer,
					match.ProductName,
					match.ModelNumber,
					match.ManualVersion,
					match.Locale,
					match.WarrantyDurationMonths,
					match.SectionHeading,
					match.IsActiveManual)));
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

	private static int ProductAffinity(
		string question,
		ReceiptSearchMatchResponse match)
	{
		var metadata = new[]
		{
			match.ProductManufacturer,
			match.ProductName,
			match.ModelNumber
		};

		return metadata
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.SelectMany(value => WordPattern()
				.Matches(value!)
				.Select(token => token.Value))
			.Where(token => token.Length >= 2 && !StopWords.Contains(token))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Count(token =>
				question.Contains(
					token,
					StringComparison.OrdinalIgnoreCase));
	}

	private static int EvidencePreference(
		string question,
		ReceiptSearchMatchResponse match)
	{
		var manualIntent = ManualIntentScore(question);
		var receiptIntent = ReceiptIntentScore(question);

		return match.DocumentType switch
		{
			ReceiptSearchDocumentType.ProductManual => manualIntent,
			ReceiptSearchDocumentType.Receipt => receiptIntent,
			_ => 0
		};
	}

	private static int ManualIntentScore(string question) =>
		CountTerms(
			question,
			"clean", "filter", "wash", "dry", "maintain", "maintenance",
			"instruction", "manual", "operate", "use", "troubleshoot");

	private static int ReceiptIntentScore(string question) =>
		CountTerms(
			question,
			"pay", "paid", "price", "cost", "purchase", "purchased",
			"buy", "bought", "receipt", "total", "date", "warranty");

	private static void EnsureDocumentType(
		List<ReceiptSearchMatchResponse> candidates,
		IReadOnlyList<ReceiptSearchMatchResponse> ranked,
		ReceiptSearchDocumentType documentType)
	{
		if (candidates.Any(match => match.DocumentType == documentType))
			return;

		var missing = ranked.FirstOrDefault(match =>
			match.DocumentType == documentType);
		if (missing is null)
			return;

		var replacementIndex = candidates.FindLastIndex(match =>
			match.DocumentType != documentType);
		if (replacementIndex >= 0)
		{
			candidates.RemoveAt(replacementIndex);
			candidates.Add(missing);
		}
	}

	private static int CountTerms(string value, params string[] terms) =>
		terms.Count(term =>
			value.Contains(term, StringComparison.OrdinalIgnoreCase));

	private static bool TryCreateEvidenceFallback(
		string question,
		IReadOnlyList<SelectedEvidence> selected,
		out string answer,
		out ReceiptAnswerSourceResponse source)
	{
		var questionTerms = WordPattern()
			.Matches(question)
			.Select(match => match.Value)
			.Where(term => term.Length >= 3 && !StopWords.Contains(term))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var requiresDuration =
			question.Contains("how long", StringComparison.OrdinalIgnoreCase) ||
			question.Contains("dry", StringComparison.OrdinalIgnoreCase);
		var requiresAmount =
			question.Contains("how much", StringComparison.OrdinalIgnoreCase) ||
			question.Contains("price", StringComparison.OrdinalIgnoreCase) ||
			question.Contains("cost", StringComparison.OrdinalIgnoreCase) ||
			question.Contains("paid", StringComparison.OrdinalIgnoreCase);
		var candidates = selected
			.SelectMany((item, evidenceOrder) => SentencePattern()
				.Split(item.Evidence.Content)
				.Select(sentence => sentence.Trim())
				.Where(sentence => sentence.Length is >= 10 and <= 500)
				.Select(sentence => new
				{
					Item = item,
					EvidenceOrder = evidenceOrder,
					Sentence = sentence,
					Score = questionTerms.Count(term =>
						sentence.Contains(
							term,
							StringComparison.OrdinalIgnoreCase))
				}))
			.Where(candidate =>
				candidate.Score >=
					(requiresDuration || requiresAmount ? 1 : 2))
			.Where(candidate =>
				!requiresDuration ||
				DurationPattern().IsMatch(candidate.Sentence))
			.Where(candidate =>
				!requiresAmount ||
				AmountPattern().IsMatch(candidate.Sentence))
			.OrderByDescending(candidate => candidate.Score)
			.ThenBy(candidate => candidate.EvidenceOrder)
			.ThenBy(candidate => candidate.Sentence.Length)
			.ToArray();

		if (candidates.FirstOrDefault() is not { } best)
		{
			answer = string.Empty;
			source = null!;
			return false;
		}

		var sentence = best.Sentence.Length <= 320
			? best.Sentence
			: string.Concat(best.Sentence.AsSpan(0, 317), "...");
		answer = $"The relevant source states: {sentence} [{best.Item.Evidence.CitationToken}]";
		source = best.Item.Source;
		return true;
	}

	private static bool IsExplicitVersionQuery(
		string question,
		ReceiptSearchMatchResponse match) =>
		match.DocumentType == ReceiptSearchDocumentType.ProductManual &&
		!string.IsNullOrWhiteSpace(match.ManualVersion) &&
		Regex.IsMatch(
			question,
			$@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(match.ManualVersion)}(?![\p{{L}}\p{{N}}])",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
		IReadOnlyCollection<string> allowed) =>
		CitationPattern().Replace(answer ?? string.Empty, match =>
			allowed.Contains($"S{match.Groups[1].Value}")
				? match.Value
				: string.Empty);

	[GeneratedRegex(@"\[S(\d+)\]", RegexOptions.CultureInvariant)]
	private static partial Regex CitationPattern();

	[GeneratedRegex(@"\p{L}[\p{L}\p{N}_-]*", RegexOptions.CultureInvariant)]
	private static partial Regex WordPattern();

	[GeneratedRegex(@"(?<=[.!?])\s+|\r?\n+", RegexOptions.CultureInvariant)]
	private static partial Regex SentencePattern();

	[GeneratedRegex(
		@"\b\d+(?:\.\d+)?\s*(?:seconds?|minutes?|hours?|days?|weeks?|months?|years?)\b",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex DurationPattern();

	[GeneratedRegex(
		@"(?:[$£€]\s*\d|\b\d+(?:[.,]\d{1,2})?\s*(?:GBP|USD|EUR|pounds?|dollars?|euros?)\b)",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex AmountPattern();

	private sealed record SelectedEvidence(
		ReceiptAnswerEvidence Evidence,
		ReceiptAnswerSourceResponse Source);
}
