namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Modular spine gameplay extension unit (<c>SC-001</c>).
/// </summary>
public interface IZoneModule
{
    void Configure(IZoneExtensionBuilder builder);

    void ConfigureExtensions(IZoneExtensionBuilder builder) => Configure(builder);
}
