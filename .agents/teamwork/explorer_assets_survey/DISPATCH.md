## 2026-11-11T06:53:28Z
You are the GameAssets, Build, and Test Suite Surveyor.
Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\explorer_assets_survey\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md

Task:
1. Read `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`.
2. Inspect and survey:
   - `src/JXServer.GameAssets`: list every class/file. Identify legacy file readers/parsers not used at runtime versus runtime loaders (MemoryPack `.pack`, `data/**/*.json`). Check which legacy parsers should be moved to `tools/JXServer.DataExporter` or removed to comply with Constitution v1.2.0 Principle V and `docs/data-architecture-standard.md`.
   - Build environment: check `Directory.Build.props` or csproj files for `TreatWarningsAsErrors=true`, .NET 10 / C# 14 compliance, warning levels, sealed class rules, 1 type per `.cs` file, and unit suffix naming rules.
   - Run tests verification via `dotnet test` (you are allowed to run test/build commands to inspect status and gather evidence). Verify all 97 existing tests, which projects they belong to, pass/fail status, and document test command baseline.
3. Produce a structured handoff report in `d:\Projects\jx-server\.agents\teamwork\explorer_assets_survey\handoff.md` with complete evidence and file paths. Send a completion message when done.
