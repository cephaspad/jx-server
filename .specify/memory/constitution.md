<!--
Sync Impact Report:
- Version change: 1.1.0 → 1.2.0
- Modified principles:
  - V. UTF-8 Domain Core & Boundary-Only Legacy Transcoding → V. UTF-8 Domain Core, Dual-Tier Game Data Standard & Boundary-Only Protocol Transcoding
  - VI. Clean Modular Boundaries, ABP DDD Role Separation & Compilation-Checked Scripting → VI. Clean Modular Boundaries, ABP DDD Role Separation & C# 14 Fluent Scripting API
- Modified sections:
  - Technical Stack, Performance & Structural Constraints (mandated JXData Standard v1: Canonical UTF-8 JSON + JSON Schema authoring format, MemoryPack runtime snapshots, dual id + code identifiers, and C# 14 Fluent Scripting API with Hot-Reload)
- Added sections: None
- Removed sections: None
- Follow-up TODOs: None
-->

# JXServer Constitution

## Core Principles

### I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Architecture
- The server architecture MUST separate responsibilities into four decoupled operational planes that can execute either inside a single-process Modular Monolith (for local development and small-to-medium deployments) or across a horizontally scaled Multi-Node Cluster (for high-concurrency realms) without modifying domain or simulation logic:
  1. **Stateless Edge Gateways**: Terminate client network connections, handle packet framing, execute per-session stream encryption, and multiplex player traffic to the appropriate Control Plane and Zone nodes without holding authoritative world state.
  2. **Distributed Control Plane**: Orchestrate account authentication, role selection, session leases with monotonic fencing epochs, cross-zone handoffs, global and guild chat routing, party and guild state, and atomic economy transactions.
  3. **Sharded 18-Hz Zone Simulation Engine**: Isolate each active map instance (or map shard) as an independent, lock-free, single-threaded deterministic simulation loop running at 18 Hz (approximately 55.5 milliseconds per tick) with data-oriented spatial partitioning (3x3 region area-of-interest grids). High-density combat or missile bursts on one map MUST NEVER degrade tick latency on other maps.
  4. **Asynchronous Write-Behind Persistence**: Decouple relational database storage from the real-time 18-Hz zone loop by combining write-ahead journaling for critical asset transfers with periodic write-behind snapshots for character progression.

*Rationale*: Eliminates the single-threaded GameServer bottleneck and cross-server disconnection stalls of the legacy Kingsoft architecture while supporting seamless vertical and horizontal scaling.

### II. Multi-Protocol Transport Abstraction & Evolutionary Client Compatibility
- The core world simulation engine and control plane MUST remain 100% agnostic of wire-level client protocols, encryption algorithms, and frame layouts.
- All client-facing network protocols MUST be implemented as pluggable transport adapters translating wire frames into canonical internal commands and domain events:
  1. **Legacy JX1 Protocol Adapter (Phase 1)**: Provides byte-exact compatibility with the original Võ Lâm Truyền Kỳ client executable and autonomous headless bots over the legacy Bishop and GameServer TCP handshake, stream cipher, and fixed/variable opcode framing.
  2. **Modern Transport Adapter (Phase 2)**: Enables modern cross-platform clients (desktop, mobile, web) to connect over modern transports and UTF-8 serialization schemes to the exact same Zone Engine and Control Plane, supporting simultaneous cross-play and eventual retirement of the legacy client without rewriting server rules.

*Rationale*: Allows immediate end-to-end validation using the existing game client and autonomous bot swarm while guaranteeing a non-destructive upgrade path to modern clients and higher frame rates.

### III. Zero-Hallucination Evidence & Ground-Truth Fidelity (NON-NEGOTIABLE)
- Contributors and AI agents MUST NEVER guess, invent, or approximate network opcodes, byte offsets, stream cipher key mutations, static asset binary formats, five-elements combat formulas, 64-direction missile trajectories, or equipment attribute generation rules.
- Every protocol contract, asset parser, and gameplay formula MUST be derived from and traceable to at least one of the three authoritative ground-truth sources in the workspace:
  1. Verified protocol and asset specifications established in the companion `jx-swarm` documentation and test vectors.
  2. Original Kingsoft C++ reference implementations in `jx-win-src` (covering core mechanics, spatial regions, skills, missiles, item generators, and package codecs).
  3. Decompiled Linux 6.0 server binaries (`bishop_y`, `goddess_y`, `s3relay_y`, `jx_linux_y`) in `jx-linux-bin` inspected via Reverse Engineer Anything (REA) or headless Ghidra analysis when verifying version 6.0 extensions.
- **Mandatory Halt & Verify Rule**: Whenever a required packet layout, opcode size, or mechanical formula is absent or ambiguous across existing documentation, the agent MUST verify the exact behavior from `jx-win-src` or `jx-linux-bin` and document the evidence before implementing the feature.

*Rationale*: Binary protocols and deterministic 18-Hz game math fail catastrophically on single-byte or single-bit discrepancies; strict evidence traceability eliminates AI hallucination.

### IV. Zero-Dupe Economy & Lease-Fenced Session Integrity
- Every active account session MUST be governed by a single authoritative lease with a monotonically increasing fencing epoch issued by the Control Plane. Any stale zone or gateway message carrying a superseded epoch MUST be rejected immediately, preventing dual-login exploits and stuck-account lockups on abnormal disconnects.
- Every non-stackable equipment item and tracked currency/token transfer MUST carry a globally unique identity and version token.
- All multi-entity economic mutations (player-to-player trade, player stall purchases, repository box transfers, and guild treasury operations) MUST execute as atomic, journaled control-plane transactions rather than unsynchronized in-memory mutations across separate actors.

*Rationale*: Eradicates the classic item duplication and account-lock vulnerabilities inherent in the legacy asynchronous binary blob persistence model.

### V. UTF-8 Domain Core, Dual-Tier Game Data Standard & Boundary-Only Protocol Transcoding
- All internal domain entities, control-plane contracts, zone simulation states, scripting APIs, canonical game data files, logs, and database schemas MUST use standard UTF-8 Unicode strings exclusively.
- Static game data MUST adhere to the **Dual-Tier Game Data Standard (`JXData Standard v1` — [docs/data-architecture-standard.md](../../docs/data-architecture-standard.md))**:
  1. **Tier 1 — Authoring Source of Truth**: Domain-structured UTF-8 JSON documents under `data/` (`maps/`, `npcs/`, `players/`, `skills/`, `items/`, `loot/`, `shops/`) validated against JSON Schema (Draft 2020-12) contracts under `schemas/*.schema.json`.
  2. **Tier 2 — Runtime Delivery Snapshot**: Pre-compiled zero-copy binary snapshots (`MemoryPack` `.pack` files verified by `SHA-256` checksums) generated from the canonical JSON files for instantaneous loading into immutable `FrozenDictionary` / `ReadOnlyMemory<T>` catalogs.
- Legacy byte encodings (`TCVN3`/`ABC` and `GBK`/`Windows-1252`) required by the original game client wire protocol MUST be transcoded strictly at the `JXServer.Adapter.LegacyJX1` network boundary.

*Rationale*: Ensures human/AI-readable, Git-diffable, schema-validated game data alongside sub-millisecond zero-allocation runtime loading without polluting the server core with legacy encodings.

### VI. Clean Modular Boundaries, ABP DDD Role Separation & C# 14 Fluent Scripting API
- Dependencies MUST flow strictly inward across four architectural tiers:
  1. **Primitives (Domain.Shared) Tier**: Zero-dependency shared constants, enumerations, coordinate and fixed-point math primitives, structured error codes, and encoding transcoders.
  2. **Simulation & Asset Engine Tier**: Self-contained libraries for static asset catalogs (`JXServer.GameAssets`), the 18-Hz Zone Engine (`JXServer.ZoneEngine`), and strongly-typed gameplay scripting (`JXServer.Scripts`), free of database or web middleware dependencies.
  3. **Control-Plane DDD Tier**: Domain aggregates, domain services, application contracts, application use-case orchestrators, and EF Core persistence/cluster infrastructure implementations.
  4. **Adapters & Host Tier**: Legacy and modern network transport adapters, cluster communication bindings, and the executable server host.
- **ABP DDD Role Separation**:
  - **Domain Aggregates & Entities** MUST protect their internal state behind private or internal setters, provide a protected parameterless constructor for ORM materialization alongside invariant-enforcing public constructors, and reference other aggregates solely by identifier rather than navigation properties.
  - **Domain Services (`*Manager`)** MUST contain only core cross-aggregate business rules, MUST NOT perform authorization or send external notifications, and MUST NOT call database save operations directly.
  - **Application Services (`*AppService`)** MUST act as the transaction/unit-of-work boundary that orchestrates domain managers and repositories, persists changes, and accepts/returns only DTOs (never leaking domain entities outside the application layer).
- **Declarative Data Rules & C# 14 Fluent Scripting API (Mandatory)**:
  - Static game catalogs and formulas (map portals, skill scaling curves, monster stat scaling, loot tables, and shop merchandise lists) MUST be defined declaratively in `data/**/*.json`.
  - All interactive gameplay scripts—including **all NPC dialogs**, quests, item usage scripts, and dynamic missions/battlegrounds—MUST be implemented using the **C# 14 Fluent Scripting API (`JXServer.Scripts`)** (`INpcDialogScript`, `IItemUsageScript`, `IMissionController`) with compile-time Roslyn verification, thread-safe per-session contexts, zero-allocation 18-Hz execution, and runtime hot-reload via collectible `AssemblyLoadContext`. Embedding untyped dynamic scripting VMs (such as Lua) in the runtime server is strictly forbidden.

*Rationale*: Combines ABP Framework's battle-tested DDD boundaries in the Control Plane with zero-overhead data-oriented execution and compile-time verified, hot-reloadable C# 14 Fluent Scripting in the 18-Hz Zone Engine.

## Technical Stack, Performance & Structural Constraints

- **Runtime, Compiler & Persistence Baseline**:
  - Target .NET 10 and C# 14 using the XML solution format, centralized build properties, and central package version management.
  - Enforce nullable reference types, the latest Roslyn analysis level, and warnings-as-errors across all projects.
  - Standardize relational persistence on **EF Core 10 with Microsoft SQL Server** as the primary production database provider and **SQLite (WAL) / InMemory** for lightweight local development and automated test suites.
- **ABP DDD, 18-Hz Game Server & `JXData Standard v1` Naming Conventions (Mandatory)**:
  - **One Type Per File & Sealed Concrete Classes**: Every class, structure, record, interface, or enumeration MUST reside in its own dedicated source file matching the exact type name. Concrete implementation classes (`*AppService`, `*Manager`, `EfCore*Repository`, `*PacketHandler`, `*TickSystem`, `*Script`) MUST be marked sealed by default.
  - **Pluralized Feature/Aggregate Folders**: Generic technical bucket folders (`Entities/`, `Dtos/`, `Services/`, `Repositories/`, `Helpers/`, `Utils/`) at the project root are strictly forbidden. Source files MUST be organized into pluralized aggregate/feature directories (`Accounts/`, `Characters/`, `SessionLeases/`, `WorldZones/`) mirrored consistently across layers.
  - **ABP DDD Layer Suffixes**:
    - **Primitives Tier**: `<Feature>Consts` for validation limits and `<Module>ErrorCodes` formatted as `JXServer:<Domain>:<Code>`.
    - **Domain Tier**: Singular nouns for Aggregate Roots and Entities (never suffixed with `Entity` or `Model`), `<Feature>Manager` for domain services, `<Rule>Specification` for reusable query filters, and `I<Aggregate>Repository` for repository interfaces.
    - **Contracts & Application Tiers**: `I<Feature>AppService` and `<Feature>AppService` for application services; directional DTOs strictly separated into `<Entity>Dto` / `<Entity>SummaryDto` (read), `Create<Entity>Dto` (create), `Update<Entity>Dto` (update), `Get<Entities>Input` (query), and `<Event>Eto` (distributed event transfer objects).
    - **Infrastructure Tier**: `EfCore<Aggregate>Repository` for EF Core repository implementations.
  - **Five Real-Time Game Server & Binary Protocol Suffixes**:
    1. **Directional Packet Contracts**: Client-to-server packets MUST be suffixed with `*C2SPacket` and server-to-client packets with `*S2CPacket`, dispatched via `I<Endpoint>PacketHandler` / `<Action>PacketHandler`.
    2. **Zone Simulation vs. ORM Separation**: In-memory 18-Hz zone representations MUST use `*ZoneState` or `*Slot` suffixes (never reusing EF Core `Character` or `Account` entities inside the zone loop), mutated via `<Action>ZoneCommand` and processed by `<Subsystem>TickSystem`.
    3. **Unit-Explicit Coordinate & Timing Naming**: Spatial properties and parameters in both C# and JSON schemas MUST explicitly include their coordinate unit suffix (`*MpsX`/`*MpsY`/`*RadiusMps` for world pixels, `*CellX`/`*CellY`/`*WidthCells`/`*HeightCells` for 32x32 obstacle cells, `*RegionX`/`*RegionY`/`*RegionId` for 512x1024 regions), and time values MUST distinguish between `*Frames` (18-Hz ticks) and `*Ms` / `*Seconds` / `*Minutes`.
    4. **Immutable Static Assets & Dual Identifiers (`id` + `code`)**: Pre-loaded game data records MUST be suffixed with `*Template` or `*Descriptor` and exposed via singleton read-only `I<Asset>Catalog` / `<Asset>Catalog` registries. Every primary record in `data/**/*.json` MUST define both a numeric `id` (for wire-protocol fidelity) and a `kebab-case` string `code` (for human/AI readability), with obstacle grids stored inline as RLE + Base64 inside unified `*.map.json` documents.
    5. **Source-Generated Logging**: Each module MUST define structured log methods inside a dedicated `<Module>LogMessages` class rather than allocating interpolated strings on hot paths.
- **Real-Time 18-Hz Hot-Path Standards**:
  - **Non-Blocking Simulation Loop**: Database queries, disk I/O, HTTP calls, and cross-zone blocking locks MUST NEVER execute inside the 18-Hz zone tick thread.
  - **Zero-Allocation Hot Paths**: Packet framing, stream cipher routines, region broadcasts, and per-tick missile/combat evaluations MUST utilize pooled buffers and stack/span memory to prevent garbage collection pauses under high player density.
  - **Deterministic Time Abstraction**: All tick timers, skill cooldowns, buff durations, and respawn schedules MUST depend on an abstract time provider so zone simulations can be deterministically stepped in automated tests.

## Development Workflow & Verification Gates

1. **Ground-Truth Specification Gate**: Before implementing any protocol packet, asset loader, or combat/item mechanic, verify that its exact structure and formulas are documented with traceable references to `jx-swarm` docs, `jx-win-src` source files, or `jx-linux-bin` binary addresses.
2. **Symmetric Protocol, Schema & Formula Unit Gate**: Every legacy packet serializer/deserializer, cipher routine, canonical JSON asset file against `schemas/*.schema.json`, and core mathematical formula (five-elements damage, 64-direction trigonometry, coordinate conversion, item generation) MUST pass deterministic unit tests against verified test vectors before integration into a Zone or Gateway.
3. **Autonomous Swarm & Headless Integration Gate**: New gateway and zone features MUST be validated end-to-end by connecting automated headless client sessions (compatible with `jx-swarm`) to verify full handshake, world entry, movement, combat, and state synchronization without manual guesswork.
4. **High-Density Stress & Fault Isolation Gate**: Malformed client packets, sudden socket drops, or script exceptions for a single player MUST be contained to that session without crashing the Zone loop, Edge Gateway, or sibling player sessions.
5. **Structural & Zero-Warning Build Gate**: All plans, specifications, task breakdowns, and pull requests MUST pass formatting verification, build with zero warnings under warnings-as-errors, and strictly respect the one-type-per-file, ABP DDD, and 18-Hz game server naming conventions.

## Governance

- This Constitution is the supreme architectural and engineering authority for the **JXServer** repository and supersedes ad-hoc implementation shortcuts.
- Concrete module breakdowns, database schemas, packet tables, and milestone tasks belong in feature-level specifications, implementation plans, and technical documentation, provided they comply with Principles I–VI and the constraints above.
- Any amendment to this Constitution requires:
  1. Documenting the technical rationale and impact on compatibility, scalability, and verification gates.
  2. Explicit approval from the project owner.
  3. Incrementing the semantic version below (`MAJOR` for backward-incompatible governance or architectural redefinitions, `MINOR` for new principles or expanded constraints, `PATCH` for wording clarifications).
- Every feature specification, implementation plan, task list, and code review MUST explicitly verify compliance with this Constitution.

**Version**: 1.2.0 | **Ratified**: 2026-11-08 | **Last Amended**: 2026-11-09
