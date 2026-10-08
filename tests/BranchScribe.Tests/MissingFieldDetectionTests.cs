namespace BranchScribe.Tests;

using BranchScribe.Models;
using BranchScribe.Services.Planning;
using Xunit;

public class MissingFieldDetectionTests
{
    private static FormSchema CreateSchema() => new()
    {
        SchemaId = "test",
        Fields =
        [
            new FormField { FieldId = "f1", Label = "Field 1", Required = true, DependsOn = [] },
            new FormField { FieldId = "f2", Label = "Field 2", Required = true, DependsOn = [] },
            new FormField { FieldId = "f3", Label = "Field 3", Required = false, DependsOn = [] },
            new FormField { FieldId = "f4", Label = "Field 4", Required = true, DependsOn = ["f1"] },
        ]
    };

    [Fact]
    public void MissingFields_WhenNoExtraction_AllRequiredWithNoDepsAreMissing()
    {
        var schema = CreateSchema();
        var extraction = new ExtractionResult();

        var missing = NextQuestionPlanner.GetResolvableMissingFields(schema, extraction);

        Assert.Equal(2, missing.Count);
        Assert.Contains(missing, f => f.FieldId == "f1");
        Assert.Contains(missing, f => f.FieldId == "f2");
        // f3 is not required, f4 has unmet dependency
        Assert.DoesNotContain(missing, f => f.FieldId == "f3");
        Assert.DoesNotContain(missing, f => f.FieldId == "f4");
    }

    [Fact]
    public void MissingFields_ExplicitlyMissing_IncludedInResult()
    {
        var schema = CreateSchema();
        var extraction = new ExtractionResult
        {
            Fields = new Dictionary<string, ExtractedFieldValue>
            {
                ["f1"] = new ExtractedFieldValue { FieldId = "f1", Value = "val", Confidence = 0.9 },
            },
            MissingFieldIds = ["f2"],
        };

        var missing = NextQuestionPlanner.GetResolvableMissingFields(schema, extraction);

        Assert.Contains(missing, f => f.FieldId == "f2");
        Assert.Contains(missing, f => f.FieldId == "f4"); // f4's dep (f1) now satisfied
    }

    [Fact]
    public void MissingFields_WhenPartialExtraction_ShowsCorrectMissing()
    {
        var schema = CreateSchema();
        var extraction = new ExtractionResult
        {
            Fields = new Dictionary<string, ExtractedFieldValue>
            {
                ["f1"] = new ExtractedFieldValue { FieldId = "f1", Value = "val1", Confidence = 0.9 },
                ["f2"] = new ExtractedFieldValue { FieldId = "f2", Value = "val2", Confidence = 0.9 },
            },
        };

        var missing = NextQuestionPlanner.GetResolvableMissingFields(schema, extraction);

        // f4 depends on f1 which is extracted, so f4 is now resolvable and missing
        Assert.Single(missing);
        Assert.Equal("f4", missing[0].FieldId);
    }
}
