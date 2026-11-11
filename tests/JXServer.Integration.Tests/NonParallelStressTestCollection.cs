using Xunit;

namespace JXServer.Integration.Tests;

/// <summary>
/// Isolates wall-clock latency stress benchmarks from concurrent Kestrel/SQLite integration test hosts
/// so OS thread scheduling and cross-test Gen2 GC pauses do not distort 18-Hz tick SLA measurements.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NonParallelStressTestCollection
{
    public const string Name = "NonParallelStressTests";
}
