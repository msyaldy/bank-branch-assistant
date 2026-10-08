namespace BranchScribe.Services.Extraction;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using System.Text.Json;

public class FormExtractor : IFormExtractor
{
    private readonly ILocalLLM _llm;

    public FormExtractor(ILocalLLM llm)
    {
        _llm = llm;
    }

    /// <summary>
    /// Instant keyword-based extraction (no LLM call). Returns immediately.
    /// </summary>
    public static ExtractionResult QuickExtract(
        FormSchema schema,
        IReadOnlyList<TranscriptSegment> transcript)
    {
        var result = new ExtractionResult();
        return FallbackExtraction(schema, transcript, result);
    }

    public async Task<ExtractionResult> ExtractAsync(
        FormSchema schema,
        IReadOnlyList<TranscriptSegment> transcript,
        CancellationToken ct = default)
    {
        var transcriptText = string.Join("\n", transcript.Select(t =>
            $"[{t.Timestamp:mm\\:ss}] {t.Speaker}: {t.Text}"));

        var fieldDescriptions = string.Join("\n", schema.Fields.Select(f =>
            $"- {f.FieldId} ({f.Type}, required={f.Required}): {f.Label}"));

        var request = new LlmRequest
        {
            SystemPrompt = "You are a form extraction assistant. Extract field values from the transcript. " +
                           "Return a JSON object with field IDs as keys and extracted values. " +
                           "Only include fields that have clear evidence in the transcript.",
            UserPrompt = $"Schema fields:\n{fieldDescriptions}\n\nTranscript:\n{transcriptText}\n\n" +
                         "Extract all possible field values as JSON.",
            Temperature = 0.1,
        };

        var response = await _llm.CompleteAsync(request, ct);
        return ParseExtractionResponse(response.Content, schema, transcript);
    }

    private static ExtractionResult ParseExtractionResponse(
        string responseContent,
        FormSchema schema,
        IReadOnlyList<TranscriptSegment> transcript)
    {
        var result = new ExtractionResult();

        try
        {
            // Phi often wraps JSON in markdown code blocks — strip them
            var json = responseContent.Trim();
            if (json.Contains("```"))
            {
                var startIdx = json.IndexOf('{');
                var endIdx = json.LastIndexOf('}');
                if (startIdx >= 0 && endIdx > startIdx)
                    json = json[startIdx..(endIdx + 1)];
            }

            // Also handle cases where response has text before/after JSON
            if (!json.StartsWith('{'))
            {
                var startIdx = json.IndexOf('{');
                var endIdx = json.LastIndexOf('}');
                if (startIdx >= 0 && endIdx > startIdx)
                    json = json[startIdx..(endIdx + 1)];
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var extracted = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
            if (extracted is null) return FallbackExtraction(schema, transcript, result);

            foreach (var field in schema.Fields)
            {
                string? value = null;
                // Try exact match, then case-insensitive search
                if (extracted.TryGetValue(field.FieldId, out var rawValue))
                    value = rawValue?.ToString();
                else
                {
                    var match = extracted.FirstOrDefault(kv =>
                        kv.Key.Equals(field.FieldId, StringComparison.OrdinalIgnoreCase) ||
                        kv.Key.Replace("_", "").Equals(field.FieldId.Replace("_", ""), StringComparison.OrdinalIgnoreCase));
                    if (match.Value != null)
                        value = match.Value.ToString();
                }

                if (!string.IsNullOrWhiteSpace(value) && value != "null" && value != "unknown")
                {
                    var citations = FindCitations(field.Label, value, transcript);
                    result.Fields[field.FieldId] = new ExtractedFieldValue
                    {
                        FieldId = field.FieldId,
                        Value = value,
                        Confidence = citations.Count > 0 ? 0.85 : 0.5,
                        Citations = citations,
                    };
                }
                else if (field.Required)
                {
                    result.MissingFieldIds.Add(field.FieldId);
                }
            }
        }
        catch (JsonException)
        {
            // If JSON parse fails, do keyword-based extraction from transcript
            return FallbackExtraction(schema, transcript, result);
        }

        return result;
    }

    /// <summary>
    /// Simple keyword-based extraction when LLM JSON parsing fails.
    /// Scans transcript for obvious field values. Monetary values are converted to £ numerics.
    /// </summary>
    private static ExtractionResult FallbackExtraction(
        FormSchema schema,
        IReadOnlyList<TranscriptSegment> transcript,
        ExtractionResult result)
    {
        var fullText = string.Join(" ", transcript.Select(t => t.Text));

        foreach (var field in schema.Fields)
        {
            string? extracted = field.FieldId switch
            {
                "customer_name" => ExtractPattern(fullText, @"(?:I'm|name is|I am)\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)"),
                "date_of_birth" => ExtractPattern(fullText, @"born\s+(.+?\d{4})"),
                "employment_status" => fullText.Contains("full-time", StringComparison.OrdinalIgnoreCase) ? "Employed full-time" :
                                      fullText.Contains("part-time", StringComparison.OrdinalIgnoreCase) ? "Employed part-time" :
                                      fullText.Contains("self-employed", StringComparison.OrdinalIgnoreCase) ? "Self-employed" : null,
                "employer_name" => ExtractPattern(fullText, @"(?:at|for|with)\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*\s+(?:Ltd|Inc|Corp|Limited|PLC|Co))"),
                "annual_income" => FormatCurrency(ParseSpokenNumber(fullText, @"(?:income|earn|salary)[^.]*?([\w\s-]+(?:thousand|hundred)\s*(?:pounds|dollars)?)") ??
                                   ParseSpokenNumber(fullText, @"([A-Z][\w-]*(?:\s+[\w-]+)*\s+thousand\s*(?:pounds)?)")),
                "property_value_range" => FormatCurrency(ParseSpokenNumber(fullText, @"(?:listed at|worth|valued at|price[d]? at)\s+([\w\s]+?(?:thousand|hundred)[\w\s]*)")),
                "deposit_amount" => FormatCurrency(ParseSpokenNumber(fullText, @"(?:deposit[^.]*?|saved[^.]*?)([\w\s-]*(?:thousand|hundred)[\w\s]*)")),
                "outstanding_debts" => FormatDebts(fullText),
                "rate_preference" => fullText.Contains("fixed", StringComparison.OrdinalIgnoreCase)
                    ? ExtractPattern(fullText, @"(\w+-year fixed\s*(?:rate)?)") ?? "Fixed rate"
                    : fullText.Contains("variable", StringComparison.OrdinalIgnoreCase) ? "Variable rate" : null,
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(extracted))
            {
                result.Fields[field.FieldId] = new ExtractedFieldValue
                {
                    FieldId = field.FieldId,
                    Value = extracted.Trim(),
                    Confidence = 0.6,
                    Citations = FindCitations(field.Label, extracted, transcript),
                };
            }
            else if (field.Required)
            {
                result.MissingFieldIds.Add(field.FieldId);
            }
        }

        return result;
    }

    private static string? ExtractPattern(string text, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Parses spoken numbers like "sixty-two thousand" → 62000, "two hundred and eighty-five thousand" → 285000
    /// </summary>
    private static decimal? ParseSpokenNumber(string fullText, string pattern)
    {
        var raw = ExtractPattern(fullText, pattern);
        if (raw is null) return null;
        return WordsToNumber(raw);
    }

    private static string? FormatCurrency(decimal? value)
    {
        if (value is null || value == 0) return null;
        return $"£{value.Value:N0}";
    }

    private static string? FormatDebts(string fullText)
    {
        var debts = new List<string>();

        // Car loan
        var carLoan = ExtractPattern(fullText, @"car loan[^.]*?([\w\s-]+(?:thousand|hundred)\s*(?:remaining|left)?)");
        if (carLoan != null)
        {
            var amount = WordsToNumber(carLoan);
            debts.Add(amount > 0 ? $"Car loan: £{amount:N0}" : "Car loan");
        }
        else if (fullText.Contains("car loan", StringComparison.OrdinalIgnoreCase))
        {
            var nearAmount = ExtractPattern(fullText, @"car loan[^.]*?(\w+\s+thousand)");
            if (nearAmount != null)
            {
                var amt = WordsToNumber(nearAmount);
                if (amt > 0) debts.Add($"Car loan: £{amt:N0}");
            }
        }

        // Credit card
        var creditCard = ExtractPattern(fullText, @"credit card[^.]*?([\w\s-]+(?:thousand|hundred)\s*(?:limit)?)");
        if (creditCard != null)
        {
            var amount = WordsToNumber(creditCard);
            debts.Add(amount > 0 ? $"Credit card: £{amount:N0} limit" : "Credit card");
        }
        else if (fullText.Contains("credit card", StringComparison.OrdinalIgnoreCase))
        {
            var nearAmount = ExtractPattern(fullText, @"credit card[^.]*?(\w+\s+thousand)");
            if (nearAmount != null)
            {
                var amt = WordsToNumber(nearAmount);
                if (amt > 0) debts.Add($"Credit card: £{amt:N0} limit");
            }
        }

        return debts.Count > 0 ? string.Join("; ", debts) : null;
    }

    private static readonly Dictionary<string, long> WordValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
        ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9,
        ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13,
        ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16, ["seventeen"] = 17,
        ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20, ["thirty"] = 30,
        ["forty"] = 40, ["fifty"] = 50, ["sixty"] = 60, ["seventy"] = 70,
        ["eighty"] = 80, ["ninety"] = 90,
    };

    private static readonly Dictionary<string, long> Multipliers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hundred"] = 100,
        ["thousand"] = 1000,
        ["million"] = 1000000,
    };

    /// <summary>
    /// Converts spoken English numbers to decimal.
    /// "sixty-two thousand" → 62000, "twenty-eight thousand five hundred" → 28500
    /// "two hundred and eighty-five thousand" → 285000
    /// </summary>
    private static decimal WordsToNumber(string text)
    {
        // Clean up
        var cleaned = text.ToLower()
            .Replace("pounds", "").Replace("dollars", "")
            .Replace("remaining", "").Replace("limit", "")
            .Replace("left", "").Replace("about", "")
            .Replace(" and ", " ").Replace("-", " ")
            .Trim();

        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        long total = 0;
        long current = 0;

        foreach (var word in words)
        {
            if (WordValues.TryGetValue(word, out var val))
            {
                current += val;
            }
            else if (Multipliers.TryGetValue(word, out var mult))
            {
                if (current == 0) current = 1;
                if (mult == 1000 || mult == 1000000)
                {
                    current *= mult;
                    total += current;
                    current = 0;
                }
                else // hundred
                {
                    current *= mult;
                }
            }
        }

        total += current;
        return total;
    }

    private static List<Citation> FindCitations(
        string fieldLabel,
        string value,
        IReadOnlyList<TranscriptSegment> transcript)
    {
        var citations = new List<Citation>();
        for (int i = 0; i < transcript.Count; i++)
        {
            var segment = transcript[i];
            if (segment.Text.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                segment.Text.Contains(fieldLabel, StringComparison.OrdinalIgnoreCase))
            {
                citations.Add(new Citation
                {
                    SegmentIndex = i,
                    Timestamp = segment.Timestamp,
                    Snippet = segment.Text,
                });
            }
        }
        return citations;
    }
}
