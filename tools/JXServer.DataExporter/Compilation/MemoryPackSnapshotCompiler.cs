using System.IO;
using JXServer.GameAssets.Snapshots;
using MemoryPack;

namespace JXServer.DataExporter.Compilation;

/// <summary>
/// Compiles validated <c>data/**/*.json</c> files into a deterministic <c>MemoryPack</c> binary snapshot (<c>artifacts/jxdata.pack</c>)
/// stamped with its source SHA-256 digest (`FR-001`).
/// </summary>
public sealed class MemoryPackSnapshotCompiler
{
    public string CompileSnapshot(string dataDirectory, string artifactsDirectory)
    {
        Directory.CreateDirectory(artifactsDirectory);
        GameAssetsSnapshotBundle bundle = GameAssetsSnapshotLoader.LoadFromCanonicalJsonDirectory(dataDirectory);
        byte[] serialized = MemoryPackSerializer.Serialize(bundle);
        string outputPath = Path.Combine(artifactsDirectory, GameAssetsSnapshotLoader.DefaultSnapshotFileName);
        File.WriteAllBytes(outputPath, serialized);
        return outputPath;
    }
}
