# Quickstart Guide: Core Legacy Wire Protocol & Ciphers

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  

---

## 1. Prerequisites

- **.NET 10 SDK** installed (`dotnet --version` outputs `10.0.x`)
- Working repository at `d:\Projects\jx-server`

---

## 2. Running Framing & Cipher Tests

Run the complete suite of wire framing, cipher, and encoding unit tests:

```powershell
# From repository root (d:\Projects\jx-server)
dotnet test tests/JXServer.Protocol.Tests/JXServer.Protocol.Tests.csproj --filter FullyQualifiedName~FramingAndCipherTests
```

Expected output:
```text
Passed!  - Failed: 0, Passed: X, Skipped: 0 - JXServer.Protocol.Tests.dll
```

---

## 3. Working with Legacy Wire Framing & Ciphers

### 3.1 Initializing the 44-Byte Handshake Frame
```csharp
using JXServer.Adapter.LegacyJX1.Framing;
using JXServer.Adapter.LegacyJX1.Ciphers;
using System.Buffers;

var processor = new JxFramePipeProcessor();
var bufferWriter = new ArrayBufferWriter<byte>();

// Generates 44-byte unencrypted s2c_accountbegin frame with obfuscated keys
uint clientKey = 0x12345678u;
uint serverKey = 0x9ABCDEF0u;
processor.WriteHandshakeFrame(bufferWriter, clientKey, serverKey);

// Send bufferWriter.WrittenMemory over TCP socket
```

### 3.2 In-Place Payload Encryption & Decryption
```csharp
using JXServer.Adapter.LegacyJX1.Ciphers;

byte[] payload = [ 0x42, 0x01, 0x02, 0x03, 0x04 ];
uint connectionKey = 0x5A6B7C8Du;

// Encrypt payload in-place
KsgStreamCipher.EncodeDecodeByValue(payload, connectionKey);

// Decrypt payload in-place (symmetric)
KsgStreamCipher.EncodeDecodeByValue(payload, connectionKey);
```

### 3.3 Boundary Vietnamese Character Transcoding
```csharp
using JXServer.Primitives.Encodings;

Span<byte> buffer = stackalloc byte[32];
// Transcode UTF-8 string into fixed 32-byte TCVN3 buffer with null termination
int bytesWritten = Tcvn3Encoding.WriteFixedString("Độc Cô Cầu Bại", buffer);

// Decode TCVN3 buffer back to UTF-8 Unicode string
string characterName = Tcvn3Encoding.DecodeNullTerminated(buffer);
// characterName == "Độc Cô Cầu Bại"
```
