# Contract 02: Boundary Encodings & Password Digest Contracts

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)  
**Standard**: [docs/data-architecture-standard.md](../../../docs/data-architecture-standard.md)  

---

## 1. TCVN3 (ABC Standard) Transcoding Contract

`Tcvn3Encoding` provides bidirectional mapping between 8-bit TCVN3 encoded bytes and standard .NET UTF-16/UTF-8 strings.

### 1.1 Character Mapping Principles
- Standard ASCII characters (`0x20` to `0x7E`) map 1:1 to their Unicode counterparts.
- Vietnamese accented characters occupy the high byte range (`0x80` to `0xFE`).
- Capital accented characters in TCVN3 often share lower-case glyph bytes preceded or followed by styling flags, or occupy specific high code points (e.g., `0xB5`, `0xB6`).
- Unmapped byte values default to `'?'`.

### 1.2 Fixed Buffer Serialization Contract
```csharp
public static int WriteFixedString(string? text, Span<byte> destination)
```
- Transcodes `text` into `destination` buffer up to `destination.Length - 1`.
- Always appends at least one null terminator byte (`0x00`).
- Pads any remaining buffer space with `0x00`.
- Returns the count of bytes written up to and including the first null byte.

### 1.3 Null-Terminated Buffer Decoding Contract
```csharp
public static string DecodeNullTerminated(ReadOnlySpan<byte> source)
```
- Scans `source` for the first null byte (`0x00`).
- Decodes only the prefix bytes prior to the first null byte.
- If no null byte is present, decodes the entire span.

---

## 2. GBK (CP936) Transcoding Contract

`GbkEncoding` handles Chinese character sequences used in legacy asset paths, NPC headers, and mixed text files:
- Backed by .NET's `CodePagesEncodingProvider` registered CP936 encoding.
- Supports decoding multi-byte CJK ideographs and encoding standard Unicode text into valid GBK byte streams.
- Provides fallback handling for unmapped characters.

---

## 3. KSG Password Digest Contract

`KsgPasswordHasher` implements Kingsoft's standard password digest format:
- Input: User plaintext ASCII/UTF-8 password string.
- Hash Algorithm: Standard MD5 (128-bit).
- Output: Exact 32-character uppercase hexadecimal ASCII string:
  $$\text{Digest} = \text{MD5}(password).\text{ToString}(\text{"X2"})$$
- Verification: Constant-time comparison between stored 32-character digest and submitted digest.
