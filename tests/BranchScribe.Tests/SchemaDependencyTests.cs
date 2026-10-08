namespace BranchScribe.Tests;

using BranchScribe.Models;
using BranchScribe.Services.Planning;
using Xunit;

public class SchemaDependencyTests
{
    private static FormSchema CreateTestSchema() => new()
    {
        SchemaId = "test",
        Name = "Test Schema",
        Fields =
        [
            new FormField { FieldId = "name", Label = "Name", Required = true, DependsOn = [] },
            new FormField { FieldId = "dob", Label = "Date of Birth", Required = true, DependsOn = ["name"] },
            new FormField { FieldId = "income", Label = "Income", Required = true, DependsOn = ["name"] },
            new FormField { FieldId = "employer", Label = "Employer", Required = true, DependsOn = ["employment_status"] },
            new FormField { FieldId = "employment_status", Label = "Employment Status", Required = true, DependsOn = ["name"] },
            new FormField { FieldId = "deposit", Label = "Deposit", Required = true, DependsOn = ["income"] },
        ]
    };

    [Fact]
    public void GetResolvableMissingFields_WhenNothingExtracted_ReturnsOnlyRootFields()
    {
        var schema = CreateTestSchema();
        var extraction = new ExtractionResult();

        var result = NextQuestionPlanner.GetResolvableMissingFields(schema, extraction);

        // Only 'name' has no dependencies and should be resolvable
        Assert.Single(result);
        Assert.Equal("name", result[0].FieldId);
    }

    [Fact]
    public void GetResolvableMissingFields_WhenNameExtracted_UnlocksDependents()
    {
        var schema = CreateTestSchema();
        var extraction = new ExtractionResult
        {
            Fields = new Dictionary<string, ExtractedFieldValue>
            {
                ["name"] = new ExtractedFieldValue { FieldId = "name", Value = "Jane", Confidence = 0.9 },
            },
        };

        var result = NextQuestionPlanner.GetResolvableMissingFields(schema, extraction);

        var fieldIds = result.Select(f => f.FieldId).ToHashSet();
        Assert.Contains("dob", fieldIds);
        Assert.Contains("income", fieldIds);
        Assert.Contains("employment_status", fieldIds);
        // employer depends on employment_status which is not yet extracted
        Assert.DoesNotContain("employer", fieldIds);
    }

    [Fact]
    public void GetResolvableMissingFields_WhenChainResolved_UnlocksDeep()
    {
        var schema = CreateTestSchema();
        var extraction = new ExtractionResult
        {
            Fields = new Dictionary<string, ExtractedFieldValue>
            {
                ["name"] = new ExtractedFieldValue { FieldId = "name", Value = "Jane", Confidence = 0.9 },
                ["income"] = new ExtractedFieldValue { FieldId = "income", Value = "50000", Confidence = 0.8 },
            },
        };

        var result = NextQuestionPlanner.GetResolvableMissingFields(schema, extraction);

        var fieldIds = result.Select(f => f.FieldId).ToHashSet();
        // deposit depends on income, which is now available
        Assert.Contains("deposit", fieldIds);
    }

    [Fact]
    public void GetResolvableMissingFields_WhenAllExtracted_ReturnsEmpty()
    {
        var schema = CreateTestSchema();
        var extraction = new ExtractionResult
        {
            Fields = schema.Fields.ToDictionary(
                f => f.FieldId,
                f => new ExtractedFieldValue { FieldId = f.FieldId, Value = "test", Confidence = 0.9 }),
        };

        var result = NextQuestionPlanner.GetResolvableMissingFields(schema, extraction);

        Assert.Empty(result);
    }
}
