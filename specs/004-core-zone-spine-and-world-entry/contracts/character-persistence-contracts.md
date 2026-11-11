# Contract 03: Foundational Character & Item Persistence

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)  
**Requirements Covered**: `FR-010`–`FR-012`, `SC-006`  
**Target Projects**: `src/JXServer.Primitives`, `src/JXServer.Domain`, `src/JXServer.Contracts`, `src/JXServer.Application`, `src/JXServer.Infrastructure`, `src/JXServer.ZoneEngine`  

---

## 1. Persisted Attributes Specification

| Entity | Attribute Name | Type | Storage Column | Invariant & Validation Rules |
| :--- | :--- | :--- | :--- | :--- |
| **`Character`** | `TaskValues` | `IReadOnlyDictionary<ushort, int>` | `TaskValuesJson` (NVARCHAR(MAX)) | Sparse map for task IDs `0..65535`. Reading unset returns `0`. Values reset to `0` are pruned from JSON serialization. |
| **`Character`** | `RepositoryPasswordHash` | `string?` | `RepositoryPasswordHash` (NVARCHAR(32)) | Optional 32-char uppercase MD5 hex string. `null` means unconfigured/unlocked. Validated via `KsgPasswordHasher.IsHexDigest32`. |
| **`CharacterItem`** | `IsBroken` | `bool` | `IsBroken` (BIT) | True when durability reaches 0. Suspends combat stat bonuses. Defaults to `false`. |
| **`CharacterItem`** | `EnchaseLevel` | `byte` | `EnchaseLevel` (TINYINT) | Purple equipment crafting enhancement tier (`0..10`). Defaults to `0`. |

---

## 2. Invariant Mutator Contracts on `Character`

```csharp
namespace JXServer.Domain.Characters;

public sealed class Character : AggregateRoot<Guid>
{
    public int GetTaskValue(ushort taskId) => _taskValues.GetValueOrDefault(taskId, 0);

    public void SetTaskValue(ushort taskId, int value)
    {
        if (value == 0) _taskValues.Remove(taskId);
        else _taskValues[taskId] = value;
    }

    public void SetRepositoryPasswordHash(string? hash)
    {
        if (hash is not null && !KsgPasswordHasher.IsHexDigest32(hash))
            throw new BusinessException(JXServerErrorCodes.CharacterInvalidRepositoryCredential, "Invalid repository hash.");
        RepositoryPasswordHash = hash;
    }
}
```

---

## 3. Backward Compatibility & Safe Defaults

- **Pre-Existing Characters**: Hydrate with empty `TaskValues` (all reads return `0`) and `RepositoryPasswordHash = null`.
- **Pre-Existing Items**: Hydrate with `IsBroken = false` and `EnchaseLevel = 0`.
- **Round-Trip Parity**: Handled losslessly across `CharacterDto` $\leftrightarrow$ `PlayerZoneState` $\leftrightarrow$ `UpdateCharacterWorldSnapshotDto` $\leftrightarrow$ `JXServerDbContext`.
- **Lease Fencing Guarantee**: `SessionLeaseAppService.CompleteLogoutOrEvictAsync` verifies `LeaseEpoch` under `accountLock` *before* invoking `ICharacterRepository.UpdateAsync`, preventing stale writes from overwriting newer character states.
