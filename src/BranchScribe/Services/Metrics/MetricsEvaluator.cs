namespace BranchScribe.Services.Metrics;

using BranchScribe.Models;
using BranchScribe.Services.Interfaces;
using Microsoft.Extensions.Configuration;

public class MetricsEvaluator : IMetricsEvaluator
{
    private readonly double _deviceWatts;
    private readonly double _gridCarbonFactor;
    private readonly double _baselineMinutesSaved;

    public MetricsEvaluator(IConfiguration configuration)
    {
        _deviceWatts = double.TryParse(configuration["Metrics:DeviceWatts"], out var w) ? w : 15.0;
        _gridCarbonFactor = double.TryParse(configuration["Metrics:GridCarbonFactorGramsPerKWh"], out var g) ? g : 350.0;
        _baselineMinutesSaved = double.TryParse(configuration["Metrics:BaselineMinutesSavedPerSession"], out var b) ? b : 25.0;
    }

    public MetricsSnapshot RecordInference(double inferenceTimeMs, double latencyMs, double memoryUsageMb)
    {
        var inferenceTimeSec = inferenceTimeMs / 1000.0;
        var energyWh = (_deviceWatts * inferenceTimeSec) / 3600.0;
        var carbonGrams = (energyWh / 1000.0) * _gridCarbonFactor;

        return new MetricsSnapshot
        {
            InferenceTimeMs = inferenceTimeMs,
            LatencyMs = latencyMs,
            MemoryUsageMb = memoryUsageMb,
            EnergyConsumptionWh = energyWh,
            CarbonEmissionsGrams = carbonGrams,
            HardwareLongevityScore = 1.0,
        };
    }

    public Metrics CalculateSessionMetrics(Session session, int totalInferences)
    {
        var avgInferenceTimeSec = 0.2; // 200ms average mock inference
        var totalCo2 = CalculateCo2PerInference(avgInferenceTimeSec) * totalInferences;

        // Estimate CO₂ saved: traditional process might use cloud GPUs
        // Rough estimate: cloud GPU inference uses ~300W vs local NPU ~15W
        var cloudCo2Estimate = totalCo2 * (300.0 / _deviceWatts);
        var co2Saved = cloudCo2Estimate - totalCo2;

        return new Metrics
        {
            TimeSavedMinutes = _baselineMinutesSaved,
            Co2PerInferenceGrams = CalculateCo2PerInference(avgInferenceTimeSec),
            Co2SavedEstimateGrams = Math.Max(0, co2Saved),
            CustomersHelped = 1,
            SentimentScore = 0.0,
        };
    }

    public double CalculateCo2PerInference(double inferenceTimeSeconds)
    {
        // Energy (Wh) = Watts * time(s) / 3600
        var energyWh = (_deviceWatts * inferenceTimeSeconds) / 3600.0;
        // CO₂ (g) = Energy(kWh) * GridFactor(g/kWh)
        return (energyWh / 1000.0) * _gridCarbonFactor;
    }

    public double CalculateTimeSaved(double baselineMinutes)
    {
        return baselineMinutes > 0 ? baselineMinutes : _baselineMinutesSaved;
    }
}
