# Original User Request

## Initial Request — 2026-11-11T06:50:00Z

Thực hiện Phương án B (Zero-Regression Re-baseline) cho dự án JXServer tại d:\Projects\jx-server theo nguyên tắc: không đập bỏ code đã chạy đúng, không đánh rơi một byte đặc tả gốc, tách rời vật lý MapZoneWorker thành kiến trúc Modular Spine, tách Golden Reference Plug-in Module ChatZoneModule, bóc tách song song 12 hồ sơ SpecKit chuẩn hóa (001-011 và 016), và nghiệm thu tự động đạt trạng thái Converged 100%.

Working directory: d:\Projects\jx-server
Integrity mode: development

## Reference Material
- Quy hoạch lộ trình: `docs/specs-roadmap.md`
- Thiết kế hợp đồng Modular Spine: `specs/004-zone-modular-spine/contracts/` (bao gồm `zone-modular-spine-and-harness.md`, `domain-outbound-sinks.md`, `character-persistence-contracts.md`)
- Tiêu chuẩn hiến pháp dự án: `.specify/memory/constitution.md` (v1.2.0)
- Tiêu chuẩn kiến trúc dữ liệu: `docs/data-architecture-standard.md`

## Requirements

### R1. Lưu trữ và bảo toàn nguồn đối chiếu Ground-Truth (Bước 0)
- Di chuyển các thư mục `specs/001-gateway-world-entry`, `specs/002-spatial-aoi-movement-chat`, `specs/003-combat-missiles-loot-inventory`, và `specs/004-zone-modular-spine` vào thư mục tạm `specs/_archive_m1_m4/` để làm nguồn đối chiếu tuyệt đối (giữ trọn vẹn mọi Clarifications, hex test vectors, byte offsets, công thức C++).
- Sau khi hoàn thành và nghiệm thu đạt 100% ở Bước 3, tự động xóa bỏ thư mục tạm `specs/_archive_m1_m4/`.

### R2. Tách xương sống vật lý Modular Spine trên Code hiện có (Bước 1)
- Triển khai đầy đủ hệ thống Modular Spine từ `_archive_m1_m4/004-zone-modular-spine/contracts/`:
  - `IZoneModule`, `IZoneExtensionBuilder`, `ZoneExtensionRegistry`, `IZoneCommandHandler<TCommand>`, `IZoneTickSystem`, `ZoneTickPhase`, `ZoneExecutionContext`, `ZoneAssetServices`, `PlayerFeatureSlotKey<TSlot>`, `IPlayerFeatureSlot`, và cơ chế cô lập lỗi (fault isolation) theo session.
  - Tách `IPlayerOutboundSink` / `GameServerClientSession` theo các Domain Outbound Notification Channels (`World`, `Combat`, `Inventory`, `Chat`, và khả năng mở rộng cho các module sau).
  - Bổ sung 4 thuộc tính lưu trữ nền tảng trên `Character` & `CharacterItem` (`TaskValues` sparse map 0..65535, `RepositoryPasswordHash` digest, `IsBroken`, `EnchaseLevel`) kèm tương thích ngược và lease-epoch fencing.
  - Xây dựng `ZoneSimulationHarness` trong `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/` có khả năng chạy 1.000 frames ảo dưới 5ms với `FakeTimeProvider`, `RandomSeed` cố định và `PlayerOutboundEventRecorder`.
- Tách `MapZoneWorker.cs` thành các command handlers & tick systems đơn nhiệm khớp với các miền 004-011.
- Tách hoàn toàn `Chats/` ra thành `ChatZoneModule : IZoneModule` độc lập (Golden Reference Plug-in Module `016-module-chat-channels`), không nằm cứng trong core zone module.
- Dọn dẹp các bộ đọc file di sản không còn dùng ở runtime khỏi `src/JXServer.GameAssets`: chuyển sang `tools/JXServer.DataExporter` nếu cần cho việc xuất dữ liệu hoặc loại bỏ để tuân thủ Hiến pháp v1.2.0 Principle V và `docs/data-architecture-standard.md` (runtime chỉ nạp MemoryPack `.pack` hoặc `data/**/*.json`).
- Đảm bảo toàn bộ mã nguồn tuân thủ `.specify/memory/constitution.md` (v1.2.0): .NET 10 / C# 14, 1 type per `.cs` file, sealed concrete classes, pluralized feature folders, hậu tố đơn vị tường minh (`*MpsX`, `*CellX`, `*RegionX`, `*Frames`), `TreatWarningsAsErrors=true` (0 warnings).

### R3. Bóc tách song song 12 hồ sơ SpecKit chuẩn hóa (Bước 2)
Tạo đầy đủ 12 thư mục Spec đơn nhiệm dưới `d:\Projects\jx-server\specs\`:
1. `specs/001-core-game-data-pipeline`
2. `specs/002-core-legacy-wire-protocol`
3. `specs/003-core-auth-roster-and-leases`
4. `specs/004-core-zone-spine-and-world-entry`
5. `specs/005-core-spatial-aoi-and-spawns`
6. `specs/006-core-movement-and-obstacles`
7. `specs/007-core-portals-and-map-handoff`
8. `specs/008-core-inventory-and-equipment`
9. `specs/009-core-combat-and-missiles`
10. `specs/010-core-monster-ai-and-progression`
11. `specs/011-core-loot-and-item-generation`
12. `specs/016-module-chat-channels`
Mỗi thư mục chứa đầy đủ các artifact chuẩn SpecKit:
- `spec.md` (technology-agnostic WHAT & WHY, giữ đầy đủ Clarifications, User Stories, Edge Cases, Functional Requirements, Key Entities, Measurable Success Criteria)
- `checklists/requirements.md` (16/16 `[x]`)
- `plan.md` (Technical Context, Constitution Check v1.2.0, Project Structure thực tế)
- `research.md`, `data-model.md`, `contracts/*.md` (bảo toàn 100% độ chính xác từng byte giao thức, công thức C++, schema JSON từ `_archive_m1_m4`), `quickstart.md`
- `tasks.md` (đánh dấu `[X]` ánh xạ chính xác tới các file `.cs` và `*Tests.cs` thực tế của đúng Spec đó).

### R4. Nghiệm thu tự động và đồng bộ trạng thái Converged (Bước 3)
- Sử dụng quy trình `/speckit-analyze` và `/speckit-converge` đối chiếu từng spec trong 12 specs so với mã nguồn C# thực tế và hiến pháp dự án.
- Bù đắp và hoàn thiện mọi khoảng trống cho đến khi toàn bộ 12/12 specs đều đạt trạng thái Converged.
- Xóa thư mục tạm `specs/_archive_m1_m4/`.
- Cập nhật `.specify/feature.json` trỏ về `"specs/016-module-chat-channels"` (hoặc `"specs/011-core-loot-and-item-generation"`).

## Acceptance Criteria

### Build & Code Quality
- [ ] Toàn bộ solution `dotnet build` thành công với 0 warnings (`TreatWarningsAsErrors=true`).
- [ ] Toàn bộ file mã nguồn mới tuân thủ nghiêm ngặt quy tắc 1 type per `.cs` file, sealed concrete classes, đặt tên hậu tố đơn vị tường minh theo Hiến pháp v1.2.0.

### Test Execution & Simulation Harness
- [ ] Toàn bộ 97/97 tests hiện có tiếp tục Pass 100% (zero regression).
- [ ] Toàn bộ tests mới cho Spine, Simulation Harness, Outbound Channels, Persistence, và ChatZoneModule đều Pass 100%.
- [ ] `ZoneSimulationHarness` thực thi 1.000 frames ảo dưới 5ms một cách tiền định với `FakeTimeProvider` và `RandomSeed`.

### Modular Architecture
- [ ] `MapZoneWorker.cs` được tách thành các command handlers và tick systems đơn nhiệm khớp với các miền 004-011.
- [ ] `ChatZoneModule` hoạt động như một `IZoneModule` độc lập, cài đặt đầy đủ theo mẫu plug-in golden reference.
- [ ] Runtime `JXServer.GameAssets` không chứa các file parser di sản không dùng ở runtime.

### SpecKit Specifications & Convergence
- [ ] Đủ 12 thư mục spec (`001`–`011`, `016`) với đầy đủ các file artifact chuẩn SpecKit (`spec.md`, `plan.md`, `tasks.md`, `contracts/`, v.v.).
- [ ] 12/12 specs đều vượt qua kiểm tra `/speckit-analyze` và `/speckit-converge` đạt trạng thái Converged.
- [ ] Thư mục tạm `specs/_archive_m1_m4/` đã được xóa sạch sau khi nghiệm thu hoàn tất.
- [ ] `.specify/feature.json` được cập nhật trỏ về spec hợp lệ.
