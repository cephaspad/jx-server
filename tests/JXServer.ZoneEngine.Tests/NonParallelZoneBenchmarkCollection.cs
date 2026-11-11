using Xunit;

namespace JXServer.ZoneEngine.Tests;

/// <summary>
/// Isolates wall-clock latency benchmarks from concurrent <c>GameAssetsCatalogTests</c> snapshot deserialization
/// so intra-assembly Gen2 GC pauses do not distort 18-Hz tick SLA measurements.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NonParallelZoneBenchmarkCollection
{
    public const string Name = "NonParallelZoneBenchmarks";
}
