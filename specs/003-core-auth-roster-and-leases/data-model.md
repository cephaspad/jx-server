# Data Model: Core Authentication, Character Roster & Session Leases

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Research**: [research.md](research.md)  

---

## 1. Domain Entities & Aggregate Roots

### 1.1 `Account` Aggregate Root (`JXServer.Domain.Accounts`)

```csharp
public sealed class Account : AggregateRoot<Guid>
{
    public string UserName { get; private set; } // 1..31 ASCII chars, unique
    public string PasswordHashUpperMd5 { get; private set; } // 32 uppercase hex chars
    public string? SecondaryPasswordHashUpperMd5 { get; private set; } // optional 32 uppercase hex chars
    public AccountStatus Status { get; private set; } // Active=0, Frozen=1, Locked=2
    public uint RemainingTimeSeconds { get; private set; }
    public long CurrentLeaseEpoch { get; private set; } // Monotonic fencing token (starts at 1)

    public bool VerifyPassword(string candidateUpperMd5);
    public bool VerifyDeletionPassword(string candidateUpperMd5);
    public long AdvanceLeaseEpoch();
}
```

---

### 1.2 `Character` Aggregate Root (`JXServer.Domain.Characters`)

```csharp
public sealed class Character : AggregateRoot<Guid>
{
    public Guid AccountId { get; private set; } // Foreign key to Account
    public byte SlotIndex { get; private set; } // 0..2 (max 3 characters per account)
    public string RoleName { get; private set; } // Unique across entire server (6..30 TCVN3 bytes)
    public CharacterGender Gender { get; private set; } // Male=0, Female=1
    public ElementSeries Series { get; private set; } // Metal=0, Wood=1, Water=2, Fire=3, Earth=4
    public byte Level { get; private set; } // 1..255
    public long Experience { get; private set; }
    public int LeadExperience { get; private set; }

    // Spatial Placement & Revival
    public ushort MapId { get; private set; }
    public int RevivalMapId { get; private set; }
    public int RevivalPointId { get; private set; }
    public int MapMpsX { get; private set; }
    public int MapMpsY { get; private set; }

    // Vitals & Attributes
    public int CurrentLife { get; private set; }
    public int MaxLife { get; private set; }
    public int CurrentMana { get; private set; }
    public int MaxMana { get; private set; }
    public int CurrentStamina { get; private set; }
    public int MaxStamina { get; private set; }

    public int Strength { get; private set; }
    public int Dexterity { get; private set; }
    public int Vitality { get; private set; }
    public int Energy { get; private set; }
    public int RemainAttributePoints { get; private set; }
    public int RemainSkillPoints { get; private set; }

    public uint MoneyInventory { get; private set; }
    public uint MoneyRepository { get; private set; }

    public IReadOnlyList<CharacterSkillValue> Skills { get; private set; }
}
```

---

### 1.3 `SessionLease` Aggregate Root (`JXServer.Domain.SessionLeases`)

```csharp
public sealed class SessionLease : AggregateRoot<Guid>
{
    public Guid AccountId => Id;
    public string UserName { get; private set; }
    public long LeaseEpoch { get; private set; } // Monotonic fencing token matching Account.CurrentLeaseEpoch
    public SessionLeaseState State { get; private set; } // Offline=0, GatewayAuthenticated=1, HandoffIssued=2, WorldAttaching=3, InWorld=4
    public Guid? ActiveCharacterId { get; private set; }
    public string? ActiveRoleName { get; private set; }
    public Guid? HandoffTicketGuid { get; private set; } // Single-use 16-byte world handoff ticket
    public ushort? AssignedMapId { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset? TicketExpiresAtUtc { get; private set; } // IssuedAtUtc + 30s TTL
    public DateTimeOffset LastHeartbeatAtUtc { get; private set; }

    public void BeginGatewaySession(long epoch);
    public Guid IssueWorldHandoffTicket(Guid characterId, string roleName, ushort mapId, TimeSpan ttl);
    public bool TryRedeemTicket(Guid ticketGuid, long epoch, out Guid characterId, out ushort mapId);
    public void RecordHeartbeat(DateTimeOffset nowUtc);
    public void Release();
}
```

---

## 2. Database Schema (EF Core 10 `JXServerDbContext`)

```sql
-- Accounts Table
CREATE TABLE [Accounts] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [UserName] NVARCHAR(31) NOT NULL,
    [PasswordHashUpperMd5] NVARCHAR(32) NOT NULL,
    [SecondaryPasswordHashUpperMd5] NVARCHAR(32) NULL,
    [Status] INT NOT NULL,
    [RemainingTimeSeconds] BIGINT NOT NULL,
    [CurrentLeaseEpoch] BIGINT NOT NULL,
    CONSTRAINT [UQ_Accounts_UserName] UNIQUE ([UserName])
);

-- Characters Table
CREATE TABLE [Characters] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [AccountId] UNIQUEIDENTIFIER NOT NULL,
    [SlotIndex] TINYINT NOT NULL,
    [RoleName] NVARCHAR(30) NOT NULL,
    [Gender] INT NOT NULL,
    [Series] INT NOT NULL,
    [Level] TINYINT NOT NULL,
    [Experience] BIGINT NOT NULL,
    [LeadExperience] INT NOT NULL,
    [MapId] INT NOT NULL,
    [MapMpsX] INT NOT NULL,
    [MapMpsY] INT NOT NULL,
    [SkillsJson] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [FK_Characters_Accounts] FOREIGN KEY ([AccountId]) REFERENCES [Accounts]([Id]) ON DELETE CASCADE,
    CONSTRAINT [UQ_Characters_RoleName] UNIQUE ([RoleName]),
    CONSTRAINT [UQ_Characters_Account_Slot] UNIQUE ([AccountId], [SlotIndex])
);
```

---

## 3. Data Transfer Objects (`JXServer.Contracts`)

- **`AuthenticateAccountInput`**: `UserName`, `PasswordMd5Hex`, `ProtocolVersion`, `KickOnlineFlag`.
- **`AuthenticateAccountResultDto`**: `ResultCode` (`Success`, `AccountOrPasswordError`, `AccountAlreadyOnline`, etc.), `AccountId`, `CurrentLeaseEpoch`.
- **`CharacterSummaryDto`**: `CharacterId`, `SlotIndex`, `RoleName`, `Gender`, `Series`, `Level`.
- **`CreateCharacterDto`**: `AccountId`, `RoleName`, `Gender`, `Series`, `NativePlaceId`.
- **`WorldHandoffTicketDto`**: `TicketGuid` (`16 bytes`), `CharacterId`, `RoleName`, `TargetMapId`, `ServerIp`, `ServerPort`.
