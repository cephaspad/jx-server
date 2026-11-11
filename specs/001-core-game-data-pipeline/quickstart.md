# Quickstart Guide: Core Game Data Pipeline (`JXData Standard v1`)

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  

---

## 1. Prerequisites

- **.NET 10 SDK** installed (`dotnet --version` outputs `10.0.x`)
- Working repository at `d:\Projects\jx-server`

---

## 2. Compiling and Verifying the Game Data Pipeline

### Step 1: Run Static Asset Validation and Migration Exporter CLI
The standalone `JXServer.DataExporter` CLI tool validates canonical JSON files against schemas, evaluates balance curves, compiles MemoryPack snapshots, and verifies parity against legacy assets.

```powershell
# From repository root (d:\Projects\jx-server)
dotnet run --project tools/JXServer.DataExporter/JXServer.DataExporter.csproj -- --validate --compile-pack
```

Expected output:
```text
[INFO] Validating schemas across data/**/*.json...
[INFO] 100% of documents passed schema validation.
[INFO] Compiling MemoryPack snapshot bundle...
[INFO] Written artifacts/jxdata.pack (SHA-256 verified).
[INFO] Parity verifier: 0 unaccounted files, 1:1 record parity confirmed.
```

### Step 2: Execute Automated Catalog Parity & Loading Tests

```powershell
dotnet test tests/JXServer.ZoneEngine.Tests/JXServer.ZoneEngine.Tests.csproj --filter FullyQualifiedName~JxDataCatalogLoadingTests
dotnet test tests/JXServer.ZoneEngine.Tests/JXServer.ZoneEngine.Tests.csproj --filter FullyQualifiedName~GameAssetsCatalogTests
dotnet test tests/JXServer.ZoneEngine.Tests/JXServer.ZoneEngine.Tests.csproj --filter FullyQualifiedName~NpcAssetLoadingTests
```

All tests should pass with 0 failures:
```text
Passed!  - Failed: 0, Passed: X, Skipped: 0 - JXServer.ZoneEngine.Tests.dll
```

---

## 3. Registering Catalogs in Dependency Injection

In `Program.cs` or service composition roots, register the Game Assets catalog suite:

```csharp
using JXServer.GameAssets;

var builder = Host.CreateApplicationBuilder(args);

// Registers IMapCatalog, INpcTemplateCatalog, IPlayerTemplateCatalog,
// ISkillTemplateCatalog, IMissileTemplateCatalog, IItemTemplateCatalog,
// ILootTableCatalog, and IShopCatalog as singletons loaded from snapshot or JSON.
builder.Services.AddGameAssetsCatalogs(options =>
{
    options.ArtifactsRootPath = "artifacts";
    options.DataRootPath = "data";
    options.UseBinarySnapshot = true;
});

var app = builder.Build();
```

---

## 4. Querying In-Memory Catalogs at 18 Hz

```csharp
// Instant O(1) lookup by ID or Code slug with ZERO allocations
var npcCatalog = serviceProvider.GetRequiredService<INpcTemplateCatalog>();
if (npcCatalog.TryGetTemplate(0, out var tigerTemplate))
{
    Console.WriteLine($"NPC 0: {tigerTemplate.Name} (HP: {tigerTemplate.BaseLife})");
}

var itemCatalog = serviceProvider.GetRequiredService<IItemTemplateCatalog>();
if (itemCatalog.TryGetItemByCode("thanh-phong-kiem", out var swordTemplate))
{
    Console.WriteLine($"Sword Level: {swordTemplate.Level}, Price: {swordTemplate.PriceGold}");
}
```
