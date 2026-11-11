## 2026-11-11T06:53:22Z
You are the ZoneEngine & MapZoneWorker Architecture Surveyor.
Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\explorer_zone_survey\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md

Task:
1. Read `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`.
2. Inspect and analyze `src/JXServer.ZoneEngine/`:
   - `MapZoneWorker.cs`: current responsibilities, methods, tick loop, command processing, player session handling, and outbound sink usage. Map out how it can be cleanly refactored into single-responsibility command handlers & tick systems matching domains 004-011.
   - `Chats/`: all chat classes, packet handlers, dependencies, and how it can be completely detached into `ChatZoneModule : IZoneModule` (golden reference plug-in module `016-module-chat-channels`).
   - Sinks: `IPlayerOutboundSink` and `GameServerClientSession`, and how to split them into Domain Outbound Notification Channels (`World`, `Combat`, `Inventory`, `Chat`, etc.).
   - Entity persistence: `Character` and `CharacterItem` models in `src/JXServer.Domain/`, storage, lease-epoch fencing, and how to add `TaskValues` (sparse map 0..65535), `RepositoryPasswordHash` (digest), `IsBroken`, and `EnchaseLevel`.
   - Existing tests in `tests/JXServer.ZoneEngine.Tests/` and requirements for creating `ZoneSimulationHarness` in `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/` (running 1,000 virtual frames under 5ms deterministically with FakeTimeProvider, fixed RandomSeed, and PlayerOutboundEventRecorder).
3. Produce a structured handoff report in `d:\Projects\jx-server\.agents\teamwork\explorer_zone_survey\handoff.md` with complete evidence, file paths, and concrete refactoring blueprints. Send a completion message when done.
