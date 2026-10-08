namespace BranchScribe.Models;

public class MetricsSnapshot
{
    public double InferenceTimeMs { get; set; }
    public double LatencyMs { get; set; }
    public double MemoryUsageMb { get; set; }
    public double EnergyConsumptionWh { get; set; }
    public double CarbonEmissionsGrams { get; set; }
    public double HardwareLongevityScore { get; set; } = 1.0; // placeholder
}
