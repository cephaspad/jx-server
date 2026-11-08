<!--
Sync Impact Report:
- Version change: [CONSTITUTION_VERSION] (template) → 1.0.0
- Modified principles:
  - [PRINCIPLE_1_NAME] → I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Architecture
  - [PRINCIPLE_2_NAME] → II. Multi-Protocol Transport Abstraction & Evolutionary Client Compatibility
  - [PRINCIPLE_3_NAME] → III. Zero-Hallucination Evidence & Ground-Truth Fidelity (NON-NEGOTIABLE)
  - [PRINCIPLE_4_NAME] → IV. Zero-Dupe Economy & Lease-Fenced Session Integrity
  - [PRINCIPLE_5_NAME] → V. UTF-8 Domain Core & Boundary-Only Legacy Transcoding
- Added principles:
  - VI. Clean Modular Boundaries, SOLID Design & Compilation-Checked Scripting
- Added sections:
  - Technical Stack, Performance & Structural Constraints (replaces [SECTION_2_NAME])
  - Development Workflow & Verification Gates (replaces [SECTION_3_NAME])
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

### V. UTF-8 Domain Core & Boundary-Only Legacy Transcoding
- All internal domain entities, control-plane contracts, zone simulation states, scripting APIs, logs, and database schemas MUST use standard UTF-8 Unicode strings exclusively.
- Legacy byte encodings required by original game assets and the legacy client (TCVN3/ABC for Vietnamese text and GBK/Windows-1252 for raw asset identifiers and script paths) MUST be transcoded strictly at the Legacy Protocol Adapter and Static Game Asset Loader boundaries.

*Rationale*: Prevents legacy single-byte encoding corruption from polluting domain logic, modern client adapters, database queries, and administrative tools.

### VI. Clean Modular Boundaries, SOLID Design & Compilation-Checked Scripting
- Dependencies MUST flow strictly inward across four architectural tiers:
  1. **Primitives Tier**: Zero-dependency shared constants, enumerations, coordinate and fixed-point math primitives, error codes, and encoding transcoders.
  2. **Simulation & Asset Engine Tier**: Self-contained libraries for static asset loading (`/maps`, `/settings`), the 18-Hz Zone Engine, and strongly-typed gameplay scripting interfaces, free of database or web middleware dependencies.
  3. **Control-Plane DDD Tier**: Domain aggregates, domain services, application contracts, application use-case orchestrators, and persistence/cluster infrastructure implementations.
  4. **Adapters & Host Tier**: Legacy and modern network transport adapters, cluster communication bindings, and the executable server host.
- **Open/Closed Dispatching & Scripting**: Network opcode handlers, NPC dialog scripts, item usage scripts, and mission/event controllers MUST be registered through extensible handler registries rather than monolithic conditional switch blocks.
- **Encapsulated Domain Invariants**: Domain aggregate roots MUST protect their internal state behind private or internal setters and expose state transitions solely through invariant-enforcing domain methods.

*Rationale*: Enables parallel development across subsystems, keeps the 18-Hz simulation core lightweight and testable in isolation, and replaces fragile untyped scripts with compilation-checked C# modules.

## Technical Stack, Performance & Structural Constraints

- **Runtime & Compiler Baseline**:
  - Target .NET 10 and C# 14 using the XML solution format, centralized build properties, and central package version management.
  - Enforce nullable reference types, the latest Roslyn analysis level, and warnings-as-errors across all projects.
- **File Organization & Naming Discipline**:
  - **One Type Per File**: Every class, structure, record, interface, or enumeration MUST reside in its own dedicated source file whose filename matches the type name exactly.
  - **Pluralized Feature/Aggregate Directories**: Generic technical bucket folders (such as Entities, Dtos, Services, Helpers, or Utils) at the project root are forbidden. Files MUST be grouped into pluralized domain feature folders mirrored consistently across architectural layers.
  - **Role-Explicit Suffixes**: Use explicit suffixes matching architectural responsibility (such as `*Consts`, `*ErrorCodes`, `*Manager` for domain services, `I*Repository`, `I*AppService` / `*AppService`, directional DTOs `Create*Dto` / `Update*Dto` / `Get*Input` / `*Dto`, `*Packet`, `*PacketHandler`, and `*Options`).
- **Real-Time 18-Hz Hot-Path Standards**:
  - **Non-Blocking Simulation Loop**: Database queries, disk I/O, HTTP calls, and cross-zone blocking locks MUST NEVER execute inside the 18-Hz zone tick thread.
  - **Zero-Allocation Hot Paths**: Packet framing, stream cipher routines, region broadcasts, and per-tick missile/combat evaluations MUST utilize pooled buffers and stack/span memory to prevent garbage collection pauses under high player density.
  - **Deterministic Time Abstraction**: All tick timers, skill cooldowns, buff durations, and respawn schedules MUST depend on an abstract time provider so zone simulations can be deterministically stepped in automated tests.

## Development Workflow & Verification Gates

1. **Ground-Truth Specification Gate**: Before implementing any protocol packet, asset loader, or combat/item mechanic, verify that its exact structure and formulas are documented with traceable references to `jx-swarm` docs, `jx-win-src` source files, or `jx-linux-bin` binary addresses.
2. **Symmetric Protocol & Formula Unit Gate**: Every legacy packet serializer/deserializer, cipher routine, and core mathematical formula (five-elements damage, 64-direction trigonometry, coordinate conversion, item generation) MUST pass deterministic unit tests against verified test vectors before integration into a Zone or Gateway.
3. **Autonomous Swarm & Headless Integration Gate**: New gateway and zone features MUST be validated end-to-end by connecting automated headless client sessions (compatible with `jx-swarm`) to verify full handshake, world entry, movement, combat, and state synchronization without manual guesswork.
4. **High-Density Stress & Fault Isolation Gate**: Malformed client packets, sudden socket drops, or script exceptions for a single player MUST be contained to that session without crashing the Zone loop, Edge Gateway, or sibling player sessions.
5. **Structural & Zero-Warning Build Gate**: All plans, specifications, task breakdowns, and pull requests MUST pass formatting verification, build with zero warnings under warnings-as-errors, and strictly respect the one-type-per-file and feature-folder conventions.

## Governance

- This Constitution is the supreme architectural and engineering authority for the **JXServer** repository and supersedes ad-hoc implementation shortcuts.
- Concrete module breakdowns, database schemas, packet tables, and milestone tasks belong in feature-level specifications, implementation plans, and technical documentation, provided they comply with Principles I–VI and the constraints above.
- Any amendment to this Constitution requires:
  1. Documenting the technical rationale and impact on compatibility, scalability, and verification gates.
  2. Explicit approval from the project owner.
  3. Incrementing the semantic version below (`MAJOR` for backward-incompatible governance or architectural redefinitions, `MINOR` for new principles or expanded constraints, `PATCH` for wording clarifications).
- Every feature specification, implementation plan, task list, and code review MUST explicitly verify compliance with this Constitution.

**Version**: 1.0.0 | **Ratified**: 2026-11-08 | **Last Amended**: 2026-11-08
