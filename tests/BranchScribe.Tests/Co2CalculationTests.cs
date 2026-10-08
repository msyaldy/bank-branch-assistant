namespace BranchScribe.Tests;

using BranchScribe.Models;
using BranchScribe.Services.Metrics;
using Microsoft.Extensions.Configuration;
using Xunit;

public class Co2CalculationTests
{
    private static MetricsEvaluator CreateEvaluator(
        double watts = 15.0,
        double gridFactor = 350.0,
        double baselineMinutes = 25.0)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Metrics:DeviceWatts"] = watts.ToString(),
                ["Metrics:GridCarbonFactorGramsPerKWh"] = gridFactor.ToString(),
                ["Metrics:BaselineMinutesSavedPerSession"] = baselineMinutes.ToString(),
            })
            .Build();

        return new MetricsEvaluator(config);
    }

    [Fact]
    public void CalculateCo2PerInference_WithDefaults_ReturnsExpected()
    {
        var evaluator = CreateEvaluator();

        // 1 second inference at 15W
        // Energy = 15 * 1 / 3600 = 0.004167 Wh = 0.000004167 kWh
        // CO2 = 0.000004167 * 350 = 0.001458 grams
        var co2 = evaluator.CalculateCo2PerInference(1.0);

        Assert.True(co2 > 0);
        Assert.InRange(co2, 0.001, 0.002);
    }

    [Fact]
    public void CalculateCo2PerInference_ZeroTime_ReturnsZero()
    {
        var evaluator = CreateEvaluator();
        var co2 = evaluator.CalculateCo2PerInference(0.0);
        Assert.Equal(0.0, co2);
    }

    [Fact]
    public void CalculateCo2PerInference_ScalesWithWatts()
    {
        var lowPower = CreateEvaluator(watts: 10.0);
        var highPower = CreateEvaluator(watts: 100.0);

        var co2Low = lowPower.CalculateCo2PerInference(1.0);
        var co2High = highPower.CalculateCo2PerInference(1.0);

        Assert.True(co2High > co2Low);
        // Should be proportional: 100/10 = 10x
        Assert.InRange(co2High / co2Low, 9.5, 10.5);
    }

    [Fact]
    public void CalculateCo2PerInference_ScalesWithGridFactor()
    {
        var cleanGrid = CreateEvaluator(gridFactor: 50.0);
        var dirtyGrid = CreateEvaluator(gridFactor: 500.0);

        var co2Clean = cleanGrid.CalculateCo2PerInference(1.0);
        var co2Dirty = dirtyGrid.CalculateCo2PerInference(1.0);

        Assert.True(co2Dirty > co2Clean);
        Assert.InRange(co2Dirty / co2Clean, 9.5, 10.5);
    }

    [Fact]
    public void RecordInference_ReturnsValidSnapshot()
    {
        var evaluator = CreateEvaluator();
        var snapshot = evaluator.RecordInference(200, 250, 512);

        Assert.Equal(200, snapshot.InferenceTimeMs);
        Assert.Equal(250, snapshot.LatencyMs);
        Assert.Equal(512, snapshot.MemoryUsageMb);
        Assert.True(snapshot.EnergyConsumptionWh > 0);
        Assert.True(snapshot.CarbonEmissionsGrams > 0);
        Assert.Equal(1.0, snapshot.HardwareLongevityScore);
    }

    [Fact]
    public void CalculateSessionMetrics_ReturnsPositiveValues()
    {
        var evaluator = CreateEvaluator();
        var session = new Session
        {
            AdvisorId = "test",
            CustomerAlias = "Test Customer",
        };

        var metrics = evaluator.CalculateSessionMetrics(session, totalInferences: 10);

        Assert.Equal(25.0, metrics.TimeSavedMinutes);
        Assert.True(metrics.Co2PerInferenceGrams > 0);
        Assert.True(metrics.Co2SavedEstimateGrams >= 0);
        Assert.Equal(1, metrics.CustomersHelped);
    }

    [Fact]
    public void CalculateTimeSaved_UsesBaseline()
    {
        var evaluator = CreateEvaluator(baselineMinutes: 30.0);
        var timeSaved = evaluator.CalculateTimeSaved(0);
        Assert.Equal(30.0, timeSaved);
    }

    [Fact]
    public void CalculateTimeSaved_UsesProvidedValue()
    {
        var evaluator = CreateEvaluator(baselineMinutes: 30.0);
        var timeSaved = evaluator.CalculateTimeSaved(15.0);
        Assert.Equal(15.0, timeSaved);
    }
}
