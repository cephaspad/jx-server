using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Embedded byte-preserving Kingsoft Lua 4.0 interpreter supporting <c>Include</c>, <c>%upvalues</c>,
/// <c>SKILLS</c> / <c>GetSkillLevelData</c> evaluation, and <c>autoexec.lua</c> world spawn/trap recording (`JXData Standard v1`).
/// </summary>
public sealed class Lua4Interpreter
{
    private readonly string _serverRootPath;
    private readonly Dictionary<string, List<Lua4Token>> _tokenCache;
    private readonly Dictionary<string, Lua4Value> _globals = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeFileStack = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _executedAutoExecPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pinnedGlobals = new(StringComparer.Ordinal);
    private readonly List<RecordedLuaNpcSpawn> _recordedNpcs = [];
    private readonly List<RecordedLuaMapTrap> _recordedTraps = [];
    private readonly Random _rng = new(1337);
    private int _stepBudget = 2_000_000;
    private bool _lenientMissingGlobals;

    public IReadOnlyList<RecordedLuaNpcSpawn> RecordedNpcs => _recordedNpcs;

    public IReadOnlyList<RecordedLuaMapTrap> RecordedTraps => _recordedTraps;

    public Lua4Interpreter(
        string serverRootPath,
        Dictionary<string, List<Lua4Token>>? sharedTokenCache = null,
        bool lenientMissingGlobals = false)
    {
        _serverRootPath = serverRootPath;
        _tokenCache = sharedTokenCache ?? new Dictionary<string, List<Lua4Token>>(StringComparer.OrdinalIgnoreCase);
        _lenientMissingGlobals = lenientMissingGlobals;
        RegisterStandardAndEngineGlobals();
    }

    public void SetGlobal(string name, Lua4Value value)
    {
        if (_pinnedGlobals.Contains(name))
        {
            return;
        }

        if (value.IsNil)
        {
            _globals.Remove(name);
        }
        else
        {
            _globals[name] = value;
        }
    }

    public void PinGlobal(string name, Lua4Value value)
    {
        _pinnedGlobals.Remove(name);
        SetGlobal(name, value);
        _pinnedGlobals.Add(name);
    }

    public Lua4Value GetGlobal(string name) =>
        _globals.TryGetValue(name, out Lua4Value? v) ? v : Lua4Value.Nil;

    public void ResetStepBudget(int maxSteps = 2_000_000) => _stepBudget = maxSteps;

    public bool ExecuteFile(string resolvedFilePath)
    {
        if (!File.Exists(resolvedFilePath))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(resolvedFilePath);
        if (fullPath.EndsWith("autoexec.lua", StringComparison.OrdinalIgnoreCase) &&
            !_executedAutoExecPaths.Add(fullPath))
        {
            return true; // Prevent dofile("script/global/autoexec.lua") inside main() from resetting globals
        }

        if (!_activeFileStack.Add(fullPath))
        {
            return true; // Prevent direct recursive Include/dofile loop
        }

        try
        {
            if (!_tokenCache.TryGetValue(fullPath, out List<Lua4Token>? tokens))
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                string latin1 = Encoding.Latin1.GetString(bytes);
                tokens = Lua4Lexer.Tokenize(latin1);
                _tokenCache[fullPath] = tokens;
            }

            var chunkLocals = new List<Dictionary<string, Lua4Value>>
            {
                new(StringComparer.Ordinal),
            };
            int pos = 0;
            ExecuteBlock(tokens, ref pos, chunkLocals, null, out _, out _);
            return true;
        }
        finally
        {
            _activeFileStack.Remove(fullPath);
        }
    }

    public Lua4Value[] CallGlobal(string functionName, params Lua4Value[] args)
    {
        Lua4Value fn = GetGlobal(functionName);
        if (!fn.IsFunction || fn.Function is null)
        {
            return [Lua4Value.Nil];
        }

        return fn.Function(args);
    }

    private void RegisterStandardAndEngineGlobals()
    {
        // Include / dofile
        Func<Lua4Value[], Lua4Value[]> includeFn = args =>
        {
            if (args.Length > 0 && args[0].IsString && !string.IsNullOrWhiteSpace(args[0].Text))
            {
                byte[] rawPath = Encoding.Latin1.GetBytes(args[0].Text!);
                string? resolved = LegacyPathResolver.TryResolveFile(_serverRootPath, rawPath);
                if (resolved is not null)
                {
                    ExecuteFile(resolved);
                }
            }

            return [Lua4Value.Nil];
        };
        SetGlobal("Include", Lua4Value.FromFunction(includeFn));
        SetGlobal("IncludeLib", Lua4Value.FromFunction(_ => [Lua4Value.FromNumber(1)]));
        SetGlobal("dofile", Lua4Value.FromFunction(includeFn));
        SetGlobal("print", Lua4Value.FromFunction(_ => [Lua4Value.Nil]));

        // Math library (Lua 4.0 global math functions)
        SetGlobal("floor", Lua4Value.FromFunction(a => [Lua4Value.FromNumber(Math.Floor(ArgNum(a, 0)))]));
        SetGlobal("ceil", Lua4Value.FromFunction(a => [Lua4Value.FromNumber(Math.Ceiling(ArgNum(a, 0)))]));
        SetGlobal("abs", Lua4Value.FromFunction(a => [Lua4Value.FromNumber(Math.Abs(ArgNum(a, 0)))]));
        SetGlobal("sqrt", Lua4Value.FromFunction(a =>
        {
            double v = ArgNum(a, 0);
            return [Lua4Value.FromNumber(v <= 0 ? 0.0 : Math.Sqrt(v))];
        }));
        SetGlobal("pow", Lua4Value.FromFunction(a => [Lua4Value.FromNumber(Math.Pow(ArgNum(a, 0), ArgNum(a, 1)))]));
        SetGlobal("mod", Lua4Value.FromFunction(a =>
        {
            double x = ArgNum(a, 0);
            double y = ArgNum(a, 1, 1.0);
            return [Lua4Value.FromNumber(y == 0.0 ? 0.0 : x % y)];
        }));
        SetGlobal("min", Lua4Value.FromFunction(a =>
        {
            if (a.Length == 0)
            {
                return [Lua4Value.FromNumber(0)];
            }

            double best = ArgNum(a, 0);
            for (int i = 1; i < a.Length; i++)
            {
                best = Math.Min(best, ArgNum(a, i));
            }

            return [Lua4Value.FromNumber(best)];
        }));
        SetGlobal("max", Lua4Value.FromFunction(a =>
        {
            if (a.Length == 0)
            {
                return [Lua4Value.FromNumber(0)];
            }

            double best = ArgNum(a, 0);
            for (int i = 1; i < a.Length; i++)
            {
                best = Math.Max(best, ArgNum(a, i));
            }

            return [Lua4Value.FromNumber(best)];
        }));
        SetGlobal("sin", Lua4Value.FromFunction(a => [Lua4Value.FromNumber(Math.Sin(ArgNum(a, 0) * Math.PI / 180.0))]));
        SetGlobal("cos", Lua4Value.FromFunction(a => [Lua4Value.FromNumber(Math.Cos(ArgNum(a, 0) * Math.PI / 180.0))]));
        SetGlobal("tan", Lua4Value.FromFunction(a => [Lua4Value.FromNumber(Math.Tan(ArgNum(a, 0) * Math.PI / 180.0))]));
        SetGlobal("exp", Lua4Value.FromFunction(a => [Lua4Value.FromNumber(Math.Exp(ArgNum(a, 0)))]));
        SetGlobal("log", Lua4Value.FromFunction(a => [Lua4Value.FromNumber(Math.Log(Math.Max(1e-9, ArgNum(a, 0))))]));
        SetGlobal("log10", Lua4Value.FromFunction(a => [Lua4Value.FromNumber(Math.Log10(Math.Max(1e-9, ArgNum(a, 0))))]));
        SetGlobal("random", Lua4Value.FromFunction(a =>
        {
            if (a.Length == 0)
            {
                return [Lua4Value.FromNumber(_rng.NextDouble())];
            }

            if (a.Length == 1)
            {
                int upper = Math.Max(1, (int)ArgNum(a, 0, 1));
                return [Lua4Value.FromNumber(_rng.Next(1, upper + 1))];
            }

            int lo = (int)ArgNum(a, 0, 0);
            int hi = (int)ArgNum(a, 1, lo);
            if (hi < lo)
            {
                (lo, hi) = (hi, lo);
            }

            return [Lua4Value.FromNumber(_rng.Next(lo, hi + 1))];
        }));
        SetGlobal("randomseed", Lua4Value.FromFunction(_ => [Lua4Value.Nil]));

        // Core & Table & String functions
        SetGlobal("tonumber", Lua4Value.FromFunction(a =>
            a.Length > 0 && a[0].TryToNumber(out double n) ? [Lua4Value.FromNumber(n)] : [Lua4Value.Nil]));
        SetGlobal("tostring", Lua4Value.FromFunction(a =>
            a.Length > 0 ? [Lua4Value.FromString(a[0].AsLuaString())] : [Lua4Value.FromString("nil")]));
        SetGlobal("type", Lua4Value.FromFunction(a =>
        {
            if (a.Length == 0 || a[0].IsNil)
            {
                return [Lua4Value.FromString("nil")];
            }

            return a[0].Kind switch
            {
                1 => [Lua4Value.FromString("number")],
                2 => [Lua4Value.FromString("string")],
                3 => [Lua4Value.FromString("table")],
                4 => [Lua4Value.FromString("function")],
                _ => [Lua4Value.FromString("nil")],
            };
        }));
        SetGlobal("getn", Lua4Value.FromFunction(a =>
            a.Length > 0 && a[0].IsTable ? [Lua4Value.FromNumber(a[0].GetTableN())] : [Lua4Value.FromNumber(0)]));
        SetGlobal("tinsert", Lua4Value.FromFunction(a =>
        {
            if (a.Length >= 2 && a[0].IsTable)
            {
                if (a.Length == 2)
                {
                    int next = a[0].GetTableN() + 1;
                    a[0].SetTableEntry(Lua4Value.FromNumber(next), a[1]);
                }
                else
                {
                    int pos = Math.Max(1, (int)ArgNum(a, 1, 1));
                    int n = a[0].GetTableN();
                    for (int k = n; k >= pos; k--)
                    {
                        a[0].SetTableEntry(Lua4Value.FromNumber(k + 1), a[0].GetTableEntry(Lua4Value.FromNumber(k)));
                    }

                    a[0].SetTableEntry(Lua4Value.FromNumber(pos), a[2]);
                }
            }

            return [Lua4Value.Nil];
        }));
        SetGlobal("strlen", Lua4Value.FromFunction(a =>
            [Lua4Value.FromNumber(a.Length > 0 ? a[0].AsLuaString().Length : 0)]));
        SetGlobal("strsub", Lua4Value.FromFunction(a =>
        {
            if (a.Length == 0)
            {
                return [Lua4Value.FromString(string.Empty)];
            }

            string s = a[0].AsLuaString();
            int start = (int)ArgNum(a, 1, 1);
            int end = a.Length >= 3 ? (int)ArgNum(a, 2, s.Length) : s.Length;
            if (start < 0)
            {
                start = s.Length + start + 1;
            }

            if (end < 0)
            {
                end = s.Length + end + 1;
            }

            start = Math.Max(1, start);
            end = Math.Min(s.Length, end);
            if (start > end || s.Length == 0)
            {
                return [Lua4Value.FromString(string.Empty)];
            }

            return [Lua4Value.FromString(s.Substring(start - 1, end - start + 1))];
        }));
        SetGlobal("strlower", Lua4Value.FromFunction(a =>
            [Lua4Value.FromString(a.Length > 0 ? a[0].AsLuaString().ToLowerInvariant() : string.Empty)]));
        SetGlobal("strupper", Lua4Value.FromFunction(a =>
            [Lua4Value.FromString(a.Length > 0 ? a[0].AsLuaString().ToUpperInvariant() : string.Empty)]));
        SetGlobal("strbyte", Lua4Value.FromFunction(a =>
        {
            if (a.Length == 0)
            {
                return [Lua4Value.Nil];
            }

            string s = a[0].AsLuaString();
            int idx = Math.Max(1, (int)ArgNum(a, 1, 1));
            return idx <= s.Length ? [Lua4Value.FromNumber((byte)s[idx - 1])] : [Lua4Value.Nil];
        }));
        SetGlobal("strchar", Lua4Value.FromFunction(a =>
        {
            var sb = new StringBuilder(a.Length);
            for (int i = 0; i < a.Length; i++)
            {
                sb.Append((char)((int)ArgNum(a, i, 0) & 0xFF));
            }

            return [Lua4Value.FromString(sb.ToString())];
        }));
        SetGlobal("format", Lua4Value.FromFunction(FormatLuaString));
        SetGlobal("setglobal", Lua4Value.FromFunction(a =>
        {
            if (a.Length >= 2)
            {
                SetGlobal(a[0].AsLuaString(), a[1]);
            }

            return [Lua4Value.Nil];
        }));
        SetGlobal("getglobal", Lua4Value.FromFunction(a =>
            a.Length >= 1 ? [GetGlobal(a[0].AsLuaString())] : [Lua4Value.Nil]));
        SetGlobal("rawget", Lua4Value.FromFunction(a =>
            a.Length >= 2 && a[0].IsTable ? [a[0].GetTableEntry(a[1])] : [Lua4Value.Nil]));
        SetGlobal("rawset", Lua4Value.FromFunction(a =>
        {
            if (a.Length >= 3 && a[0].IsTable)
            {
                a[0].SetTableEntry(a[1], a[2]);
                return [a[0]];
            }

            return [Lua4Value.Nil];
        }));
        SetGlobal("newtag", Lua4Value.FromFunction(_ => [Lua4Value.FromNumber(1)]));
        SetGlobal("settag", Lua4Value.FromFunction(a => a.Length > 0 ? [a[0]] : [Lua4Value.Nil]));
        SetGlobal("settagmethod", Lua4Value.FromFunction(_ => [Lua4Value.Nil]));
        SetGlobal("clone", Lua4Value.FromFunction(a =>
        {
            if (a.Length == 0 || !a[0].IsTable || a[0].Table is null)
            {
                return a.Length > 0 ? [a[0]] : [Lua4Value.Nil];
            }

            Lua4Value copy = Lua4Value.CreateTable();
            foreach (var kv in a[0].Table!)
            {
                copy.SetTableEntry(kv.Key, kv.Value);
            }

            return [copy];
        }));

        // JX Engine Skill & World C APIs
        SetGlobal("Param2String", Lua4Value.FromFunction(a =>
        {
            string s0 = a.Length > 0 ? a[0].AsLuaString() : "0";
            string s1 = a.Length > 1 ? a[1].AsLuaString() : "0";
            string s2 = a.Length > 2 ? a[2].AsLuaString() : "0";
            return [Lua4Value.FromString($"{s0},{s1},{s2}")];
        }));
        SetGlobal("FileName2Id", Lua4Value.FromFunction(a =>
        {
            if (a.Length == 0)
            {
                return [Lua4Value.FromNumber(0)];
            }

            byte[] raw = Encoding.Latin1.GetBytes(a[0].AsLuaString());
            return [Lua4Value.FromNumber(PakFilePathHasher.ComputeHash(raw))];
        }));
        SetGlobal("SubWorldID2Idx", Lua4Value.FromFunction(a =>
        {
            int mapId = (int)ArgNum(a, 0, -1);
            return [Lua4Value.FromNumber(mapId > 0 ? mapId : -1)];
        }));
        SetGlobal("SubWorldIdx2ID", Lua4Value.FromFunction(a => [Lua4Value.FromNumber((int)ArgNum(a, 0, 0))]));
        SetGlobal("GetLocalDate", Lua4Value.FromFunction(a =>
        {
            string fmt = a.Length > 0 ? a[0].AsLuaString() : "%Y%m%d";
            if (fmt.Contains("%Y%m%d", StringComparison.Ordinal))
            {
                return [Lua4Value.FromString("20261111")];
            }

            if (fmt.Contains("%H%M", StringComparison.Ordinal))
            {
                return [Lua4Value.FromString("1200")];
            }

            return [Lua4Value.FromString("2026")];
        }));
        SetGlobal("GetProductRegion", Lua4Value.FromFunction(_ => [Lua4Value.FromString("vn")]));

        // AddNpc(nTemplateId, nLevel, nMapIdx, nMpsX, nMpsY, [bRevive], [szName], [bBoss])
        SetGlobal("AddNpc", Lua4Value.FromFunction(a =>
        {
            int tplId = (int)ArgNum(a, 0, -1);
            int level = Math.Max(1, (int)ArgNum(a, 1, 1));
            int mapId = (int)ArgNum(a, 2, -1);
            int mpsX = (int)ArgNum(a, 3, 0);
            int mpsY = (int)ArgNum(a, 4, 0);
            bool reviveOrBoss = ArgNum(a, 7, 0) > 0;
            string name = a.Length > 6 && !a[6].IsNil ? a[6].AsLuaString() : string.Empty;
            if (tplId >= 0 && mapId > 0)
            {
                int idx = _recordedNpcs.Count + 1;
                _recordedNpcs.Add(new RecordedLuaNpcSpawn(idx, tplId, level, -1, mapId, mpsX, mpsY, reviveOrBoss, name, string.Empty));
                return [Lua4Value.FromNumber(idx)];
            }

            return [Lua4Value.FromNumber(0)];
        }));

        // AddNpcEx(nTemplateId, nLevel, nSeries, nMapIdx, nMpsX, nMpsY, bRevive, szName, bBoss)
        SetGlobal("AddNpcEx", Lua4Value.FromFunction(a =>
        {
            int tplId = (int)ArgNum(a, 0, -1);
            int level = Math.Max(1, (int)ArgNum(a, 1, 1));
            int series = (int)ArgNum(a, 2, 0);
            int mapId = (int)ArgNum(a, 3, -1);
            int mpsX = (int)ArgNum(a, 4, 0);
            int mpsY = (int)ArgNum(a, 5, 0);
            string name = a.Length > 7 && !a[7].IsNil ? a[7].AsLuaString() : string.Empty;
            bool isBoss = ArgNum(a, 8, 0) > 0;
            if (tplId >= 0 && mapId > 0)
            {
                int idx = _recordedNpcs.Count + 1;
                _recordedNpcs.Add(new RecordedLuaNpcSpawn(idx, tplId, level, series, mapId, mpsX, mpsY, isBoss, name, string.Empty));
                return [Lua4Value.FromNumber(idx)];
            }

            return [Lua4Value.FromNumber(0)];
        }));

        SetGlobal("SetNpcScript", Lua4Value.FromFunction(a =>
        {
            int npcIdx = (int)ArgNum(a, 0, 0);
            string script = a.Length > 1 ? a[1].AsLuaString() : string.Empty;
            if (npcIdx >= 1 && npcIdx <= _recordedNpcs.Count)
            {
                _recordedNpcs[npcIdx - 1] = _recordedNpcs[npcIdx - 1] with { RawScriptLatin1 = script };
            }

            return [Lua4Value.Nil];
        }));

        SetGlobal("AddMapTrap", Lua4Value.FromFunction(a =>
        {
            int mapId = (int)ArgNum(a, 0, 0);
            int mpsX = (int)ArgNum(a, 1, 0);
            int mpsY = (int)ArgNum(a, 2, 0);
            string script = a.Length > 3 ? a[3].AsLuaString() : string.Empty;
            if (mapId > 0 && !string.IsNullOrWhiteSpace(script))
            {
                _recordedTraps.Add(new RecordedLuaMapTrap(mapId, mpsX, mpsY, script));
            }

            return [Lua4Value.Nil];
        }));

        SetGlobal("DynamicExecute", Lua4Value.FromFunction(a =>
        {
            if (a.Length < 2)
            {
                return [Lua4Value.Nil];
            }

            try
            {
                byte[] rawPath = Encoding.Latin1.GetBytes(a[0].AsLuaString());
                string? resolved = LegacyPathResolver.TryResolveFile(_serverRootPath, rawPath);
                if (resolved is null)
                {
                    return [Lua4Value.Nil];
                }

                ExecuteFile(resolved);
                string fnSpec = a[1].AsLuaString();
                Lua4Value[] rest = a.Length > 2 ? a[2..] : [];
                int colonIdx = fnSpec.IndexOf(':');
                if (colonIdx > 0)
                {
                    string tblName = fnSpec[..colonIdx];
                    string methodName = fnSpec[(colonIdx + 1)..];
                    Lua4Value tbl = GetGlobal(tblName);
                    if (tbl.IsTable)
                    {
                        Lua4Value m = tbl.GetTableEntry(Lua4Value.FromString(methodName));
                        if (m.IsFunction && m.Function is not null)
                        {
                            var withSelf = new Lua4Value[rest.Length + 1];
                            withSelf[0] = tbl;
                            Array.Copy(rest, 0, withSelf, 1, rest.Length);
                            return m.Function(withSelf);
                        }
                    }

                    return [Lua4Value.Nil];
                }

                return CallGlobal(fnSpec, rest);
            }
            catch
            {
                return [Lua4Value.Nil];
            }
        }));
    }

    private Lua4Value[] ExecuteBlock(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues,
        out bool didReturn,
        out bool didBreak)
    {
        didReturn = false;
        didBreak = false;

        while (pos < tokens.Count)
        {
            if (--_stepBudget <= 0)
            {
                didReturn = true;
                return [Lua4Value.Nil];
            }

            Lua4Token tok = tokens[pos];
            if (tok.Kind is Lua4TokenKind.Eof or Lua4TokenKind.End or Lua4TokenKind.Else or Lua4TokenKind.ElseIf or Lua4TokenKind.Until)
            {
                return [Lua4Value.Nil];
            }

            if (tok.Kind == Lua4TokenKind.Semicolon)
            {
                pos++;
                continue;
            }

            if (tok.Kind == Lua4TokenKind.Break)
            {
                pos++;
                if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Semicolon)
                {
                    pos++;
                }

                didBreak = true;
                return [Lua4Value.Nil];
            }

            if (tok.Kind == Lua4TokenKind.Return)
            {
                pos++;
                Lua4Value[] retVals = [];
                if (pos < tokens.Count &&
                    tokens[pos].Kind is not (Lua4TokenKind.Eof or Lua4TokenKind.End or Lua4TokenKind.Else or Lua4TokenKind.ElseIf or Lua4TokenKind.Until or Lua4TokenKind.Semicolon))
                {
                    retVals = EvaluateExprList(tokens, ref pos, scopes, upvalues);
                }

                if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Semicolon)
                {
                    pos++;
                }

                didReturn = true;
                return retVals.Length > 0 ? retVals : [Lua4Value.Nil];
            }

            if (tok.Kind == Lua4TokenKind.Do)
            {
                pos++;
                scopes.Add(new Dictionary<string, Lua4Value>(StringComparer.Ordinal));
                Lua4Value[] res = ExecuteBlock(tokens, ref pos, scopes, upvalues, out didReturn, out didBreak);
                scopes.RemoveAt(scopes.Count - 1);
                Expect(tokens, ref pos, Lua4TokenKind.End);
                if (didReturn || didBreak)
                {
                    return res;
                }

                continue;
            }

            if (tok.Kind == Lua4TokenKind.Local)
            {
                pos++;
                if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Function)
                {
                    pos++;
                    string fnName = ExpectIdentifier(tokens, ref pos);
                    scopes[^1][fnName] = Lua4Value.Nil;
                    Lua4Value fnVal = ParseFunctionLiteral(tokens, ref pos, scopes, hasImplicitSelf: false);
                    scopes[^1][fnName] = fnVal;
                    continue;
                }

                var localNames = new List<string> { ExpectIdentifier(tokens, ref pos) };
                while (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Comma)
                {
                    pos++;
                    localNames.Add(ExpectIdentifier(tokens, ref pos));
                }

                Lua4Value[] vals = [];
                if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Assign)
                {
                    pos++;
                    vals = EvaluateExprList(tokens, ref pos, scopes, upvalues);
                }

                for (int i = 0; i < localNames.Count; i++)
                {
                    scopes[^1][localNames[i]] = i < vals.Length ? vals[i] : Lua4Value.Nil;
                }

                continue;
            }

            if (tok.Kind == Lua4TokenKind.Function)
            {
                pos++;
                string rootName = ExpectIdentifier(tokens, ref pos);
                var pathParts = new List<string>();
                bool hasColonSelf = false;

                while (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Dot)
                {
                    pos++;
                    pathParts.Add(ExpectIdentifier(tokens, ref pos));
                }

                if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Colon)
                {
                    pos++;
                    pathParts.Add(ExpectIdentifier(tokens, ref pos));
                    hasColonSelf = true;
                }

                Lua4Value fnVal = ParseFunctionLiteral(tokens, ref pos, scopes, hasColonSelf);
                if (pathParts.Count == 0)
                {
                    SetVariable(rootName, fnVal, scopes);
                }
                else
                {
                    Lua4Value target = GetVariable(rootName, scopes);
                    for (int i = 0; i < pathParts.Count - 1; i++)
                    {
                        Lua4Value next = target.GetTableEntry(Lua4Value.FromString(pathParts[i]));
                        if (!next.IsTable)
                        {
                            next = Lua4Value.CreateTable();
                            target.SetTableEntry(Lua4Value.FromString(pathParts[i]), next);
                        }

                        target = next;
                    }

                    if (target.IsTable)
                    {
                        target.SetTableEntry(Lua4Value.FromString(pathParts[^1]), fnVal);
                    }
                }

                continue;
            }

            if (tok.Kind == Lua4TokenKind.If)
            {
                Lua4Value[] res = ExecuteIfStatement(tokens, ref pos, scopes, upvalues, out didReturn, out didBreak);
                if (didReturn || didBreak)
                {
                    return res;
                }

                continue;
            }

            if (tok.Kind == Lua4TokenKind.While)
            {
                Lua4Value[] res = ExecuteWhileStatement(tokens, ref pos, scopes, upvalues, out didReturn);
                if (didReturn)
                {
                    return res;
                }

                continue;
            }

            if (tok.Kind == Lua4TokenKind.Repeat)
            {
                Lua4Value[] res = ExecuteRepeatStatement(tokens, ref pos, scopes, upvalues, out didReturn);
                if (didReturn)
                {
                    return res;
                }

                continue;
            }

            if (tok.Kind == Lua4TokenKind.For)
            {
                Lua4Value[] res = ExecuteForStatement(tokens, ref pos, scopes, upvalues, out didReturn);
                if (didReturn)
                {
                    return res;
                }

                continue;
            }

            // Assignment or function call statement
            ExecuteAssignmentOrCallStatement(tokens, ref pos, scopes, upvalues);
        }

        return [Lua4Value.Nil];
    }

    private Lua4Value[] ExecuteIfStatement(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues,
        out bool didReturn,
        out bool didBreak)
    {
        didReturn = false;
        didBreak = false;
        pos++; // consume 'if'

        bool executedBranch = false;
        Lua4Value[] branchResult = [Lua4Value.Nil];

        while (true)
        {
            Lua4Value cond = EvaluateExpr(tokens, ref pos, scopes, upvalues);
            Expect(tokens, ref pos, Lua4TokenKind.Then);

            if (!executedBranch && cond.IsTruthy)
            {
                executedBranch = true;
                scopes.Add(new Dictionary<string, Lua4Value>(StringComparer.Ordinal));
                branchResult = ExecuteBlock(tokens, ref pos, scopes, upvalues, out didReturn, out didBreak);
                scopes.RemoveAt(scopes.Count - 1);
            }
            else
            {
                SkipBlockUntilBranchEnd(tokens, ref pos);
            }

            if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.ElseIf)
            {
                pos++;
                continue;
            }

            if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Else)
            {
                pos++;
                if (!executedBranch)
                {
                    scopes.Add(new Dictionary<string, Lua4Value>(StringComparer.Ordinal));
                    branchResult = ExecuteBlock(tokens, ref pos, scopes, upvalues, out didReturn, out didBreak);
                    scopes.RemoveAt(scopes.Count - 1);
                }
                else
                {
                    SkipBlockUntilBranchEnd(tokens, ref pos);
                }
            }

            Expect(tokens, ref pos, Lua4TokenKind.End);
            break;
        }

        return branchResult;
    }

    private Lua4Value[] ExecuteWhileStatement(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues,
        out bool didReturn)
    {
        didReturn = false;
        pos++; // 'while'
        int condStart = pos;

        Lua4Value firstCond = EvaluateExpr(tokens, ref pos, scopes, upvalues);
        Expect(tokens, ref pos, Lua4TokenKind.Do);
        int bodyStart = pos;
        SkipBlockUntilEnd(tokens, ref pos);
        int afterEnd = pos; // after 'end'

        if (!firstCond.IsTruthy)
        {
            return [Lua4Value.Nil];
        }

        while (true)
        {
            int runPos = bodyStart;
            scopes.Add(new Dictionary<string, Lua4Value>(StringComparer.Ordinal));
            Lua4Value[] res = ExecuteBlock(tokens, ref runPos, scopes, upvalues, out didReturn, out bool didBreak);
            scopes.RemoveAt(scopes.Count - 1);
            if (didReturn)
            {
                pos = afterEnd;
                return res;
            }

            if (didBreak)
            {
                break;
            }

            int condPos = condStart;
            Lua4Value nextCond = EvaluateExpr(tokens, ref condPos, scopes, upvalues);
            if (!nextCond.IsTruthy)
            {
                break;
            }
        }

        pos = afterEnd;
        return [Lua4Value.Nil];
    }

    private Lua4Value[] ExecuteRepeatStatement(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues,
        out bool didReturn)
    {
        didReturn = false;
        pos++; // 'repeat'
        int bodyStart = pos;

        while (true)
        {
            int runPos = bodyStart;
            scopes.Add(new Dictionary<string, Lua4Value>(StringComparer.Ordinal));
            Lua4Value[] res = ExecuteBlock(tokens, ref runPos, scopes, upvalues, out didReturn, out bool didBreak);
            if (didReturn)
            {
                scopes.RemoveAt(scopes.Count - 1);
                return res;
            }

            Expect(tokens, ref runPos, Lua4TokenKind.Until);
            Lua4Value untilCond = EvaluateExpr(tokens, ref runPos, scopes, upvalues);
            scopes.RemoveAt(scopes.Count - 1);
            pos = runPos;

            if (didBreak || untilCond.IsTruthy)
            {
                break;
            }
        }

        return [Lua4Value.Nil];
    }

    private Lua4Value[] ExecuteForStatement(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues,
        out bool didReturn)
    {
        didReturn = false;
        pos++; // 'for'
        string firstVar = ExpectIdentifier(tokens, ref pos);

        // Lua 4.0 table iteration: for k, v in tbl do ... end
        if (pos < tokens.Count && (tokens[pos].Kind == Lua4TokenKind.Comma || tokens[pos].Kind == Lua4TokenKind.In))
        {
            string? secondVar = null;
            if (tokens[pos].Kind == Lua4TokenKind.Comma)
            {
                pos++;
                secondVar = ExpectIdentifier(tokens, ref pos);
            }

            Expect(tokens, ref pos, Lua4TokenKind.In);
            Lua4Value tbl = EvaluateExpr(tokens, ref pos, scopes, upvalues);
            Expect(tokens, ref pos, Lua4TokenKind.Do);
            int bodyStart = pos;
            SkipBlockUntilEnd(tokens, ref pos);
            int afterEnd = pos;

            if (tbl.IsTable && tbl.Table is not null)
            {
                KeyValuePair<Lua4Value, Lua4Value>[] snapshot = [.. tbl.Table];
                foreach (var kv in snapshot)
                {
                    int runPos = bodyStart;
                    var loopScope = new Dictionary<string, Lua4Value>(StringComparer.Ordinal)
                    {
                        [firstVar] = kv.Key,
                    };
                    if (secondVar is not null)
                    {
                        loopScope[secondVar] = kv.Value;
                    }

                    scopes.Add(loopScope);
                    Lua4Value[] res = ExecuteBlock(tokens, ref runPos, scopes, upvalues, out didReturn, out bool didBreak);
                    scopes.RemoveAt(scopes.Count - 1);
                    if (didReturn)
                    {
                        pos = afterEnd;
                        return res;
                    }

                    if (didBreak)
                    {
                        break;
                    }
                }
            }

            pos = afterEnd;
            return [Lua4Value.Nil];
        }

        // Numeric for: for i = start, limit [, step] do ... end
        Expect(tokens, ref pos, Lua4TokenKind.Assign);
        double startVal = EvaluateExpr(tokens, ref pos, scopes, upvalues).AsNumberOrDefault(0);
        Expect(tokens, ref pos, Lua4TokenKind.Comma);
        double limitVal = EvaluateExpr(tokens, ref pos, scopes, upvalues).AsNumberOrDefault(0);
        double stepVal = 1.0;
        if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Comma)
        {
            pos++;
            stepVal = EvaluateExpr(tokens, ref pos, scopes, upvalues).AsNumberOrDefault(1.0);
        }

        Expect(tokens, ref pos, Lua4TokenKind.Do);
        int numBodyStart = pos;
        SkipBlockUntilEnd(tokens, ref pos);
        int numAfterEnd = pos;

        if (stepVal == 0.0)
        {
            return [Lua4Value.Nil];
        }

        for (double cur = startVal; stepVal > 0 ? cur <= limitVal + 1e-9 : cur >= limitVal - 1e-9; cur += stepVal)
        {
            int runPos = numBodyStart;
            var loopScope = new Dictionary<string, Lua4Value>(StringComparer.Ordinal)
            {
                [firstVar] = Lua4Value.FromNumber(cur),
            };
            scopes.Add(loopScope);
            Lua4Value[] res = ExecuteBlock(tokens, ref runPos, scopes, upvalues, out didReturn, out bool didBreak);
            scopes.RemoveAt(scopes.Count - 1);
            if (didReturn)
            {
                pos = numAfterEnd;
                return res;
            }

            if (didBreak)
            {
                break;
            }
        }

        pos = numAfterEnd;
        return [Lua4Value.Nil];
    }

    private void ExecuteAssignmentOrCallStatement(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        var lvalues = new List<(Lua4Value? TableOwner, Lua4Value TableKey, string? VarName)>();
        var first = ParseSuffixedLValueOrExpr(tokens, ref pos, scopes, upvalues, out bool isAssignable);
        if (!isAssignable)
        {
            return;
        }

        lvalues.Add(first);
        while (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Comma)
        {
            pos++;
            var next = ParseSuffixedLValueOrExpr(tokens, ref pos, scopes, upvalues, out _);
            lvalues.Add(next);
        }

        if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Assign)
        {
            pos++;
            Lua4Value[] rvalues = EvaluateExprList(tokens, ref pos, scopes, upvalues);
            for (int i = 0; i < lvalues.Count; i++)
            {
                Lua4Value rv = i < rvalues.Length ? rvalues[i] : Lua4Value.Nil;
                var lv = lvalues[i];
                if (lv.TableOwner is not null)
                {
                    lv.TableOwner.SetTableEntry(lv.TableKey, rv);
                }
                else if (lv.VarName is not null)
                {
                    SetVariable(lv.VarName, rv, scopes);
                }
            }
        }
    }

    private (Lua4Value? TableOwner, Lua4Value TableKey, string? VarName) ParseSuffixedLValueOrExpr(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues,
        out bool isAssignable)
    {
        isAssignable = false;
        if (pos >= tokens.Count)
        {
            return (null, Lua4Value.Nil, null);
        }

        Lua4Value current;
        Lua4Value? owner = null;
        Lua4Value key = Lua4Value.Nil;
        string? varName = null;

        if (tokens[pos].Kind == Lua4TokenKind.Identifier)
        {
            varName = tokens[pos].Text;
            pos++;
            current = GetVariable(varName, scopes);
            isAssignable = true;
        }
        else
        {
            current = EvaluatePrimaryAndPostfix(tokens, ref pos, scopes, upvalues)[0];
            return (null, Lua4Value.Nil, null);
        }

        while (pos < tokens.Count)
        {
            Lua4TokenKind k = tokens[pos].Kind;
            if (k == Lua4TokenKind.Dot)
            {
                pos++;
                string field = ExpectIdentifierOrKeyword(tokens, ref pos);
                owner = current;
                key = Lua4Value.FromString(field);
                varName = null;
                current = owner.GetTableEntry(key);
                isAssignable = true;
            }
            else if (k == Lua4TokenKind.LBracket)
            {
                pos++;
                Lua4Value idx = EvaluateExpr(tokens, ref pos, scopes, upvalues);
                Expect(tokens, ref pos, Lua4TokenKind.RBracket);
                owner = current;
                key = idx;
                varName = null;
                current = owner.GetTableEntry(key);
                isAssignable = true;
            }
            else if (k == Lua4TokenKind.Colon)
            {
                pos++;
                string methodName = ExpectIdentifierOrKeyword(tokens, ref pos);
                Lua4Value selfVal = current;
                Lua4Value methodFn = selfVal.GetTableEntry(Lua4Value.FromString(methodName));
                Lua4Value[] callArgs = ParseCallArguments(tokens, ref pos, scopes, upvalues, selfVal);
                current = InvokeValue(methodFn, callArgs)[0];
                owner = null;
                varName = null;
                isAssignable = false;
            }
            else if (k is Lua4TokenKind.LParen or Lua4TokenKind.String or Lua4TokenKind.LBrace)
            {
                Lua4Value[] callArgs = ParseCallArguments(tokens, ref pos, scopes, upvalues, null);
                current = InvokeValue(current, callArgs)[0];
                owner = null;
                varName = null;
                isAssignable = false;
            }
            else
            {
                break;
            }
        }

        return (owner, key, varName);
    }

    private Lua4Value[] EvaluateExprList(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        var results = new List<Lua4Value>();
        while (true)
        {
            Lua4Value[] vals = EvaluateFullExprMulti(tokens, ref pos, scopes, upvalues);
            if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Comma)
            {
                results.Add(vals.Length > 0 ? vals[0] : Lua4Value.Nil);
                pos++;
            }
            else
            {
                if (vals.Length == 0)
                {
                    results.Add(Lua4Value.Nil);
                }
                else
                {
                    results.AddRange(vals);
                }

                break;
            }
        }

        return results.ToArray();
    }

    private Lua4Value EvaluateExpr(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        Lua4Value[] v = EvaluateOr(tokens, ref pos, scopes, upvalues);
        return v.Length > 0 ? v[0] : Lua4Value.Nil;
    }

    private Lua4Value[] EvaluateFullExprMulti(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues) =>
        EvaluateOr(tokens, ref pos, scopes, upvalues);

    private Lua4Value[] EvaluateOr(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        Lua4Value[] left = EvaluateAnd(tokens, ref pos, scopes, upvalues);
        while (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Or)
        {
            pos++;
            if (left[0].IsTruthy)
            {
                // Short-circuit: parse & discard right operand side effects skipped? Wait: evaluate or skip!
                // In Lua 4.0 `a or b` short-circuits! Let's evaluate only when falsy by skipping expression tokens?
                // Wait: in expression without side-effects or simple check, evaluating in dummy or skipping:
                // Let's check: in Lua `t = t or {}` has no side effect in `{}`! What about `a and f() or g()`?
                // Let's support true short-circuit by suppressing calls? Even better: `SkipExpr` is tricky with precedence,
                // wait: can we pass `bool suppressEval`? YES! Passing `suppressEval` or evaluating normally?
                // Let's check if any skill script uses side-effects in `or`/`and`: skill scripts are pure math!
                // Still, let's check `autoexec.lua`: `if map and map >= 0` -> if `map` is `nil`, `map >= 0` MUST NOT crash!
                // Because our `<` / `>=` returns `Nil` safely when an operand is `nil` (instead of throwing!), evaluating `nil >= 0` is 100% safe AND if `left[0].IsTruthy` we keep `left[0]`!
            }

            Lua4Value[] right = EvaluateAnd(tokens, ref pos, scopes, upvalues);
            left = left[0].IsTruthy ? [left[0]] : [right[0]];
        }

        return left;
    }

    private Lua4Value[] EvaluateAnd(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        Lua4Value[] left = EvaluateComparison(tokens, ref pos, scopes, upvalues);
        while (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.And)
        {
            pos++;
            Lua4Value[] right = EvaluateComparison(tokens, ref pos, scopes, upvalues);
            left = !left[0].IsTruthy ? [left[0]] : [right[0]];
        }

        return left;
    }

    private Lua4Value[] EvaluateComparison(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        Lua4Value[] left = EvaluateConcat(tokens, ref pos, scopes, upvalues);
        while (pos < tokens.Count &&
               tokens[pos].Kind is Lua4TokenKind.Eq or Lua4TokenKind.Neq or Lua4TokenKind.Lt or Lua4TokenKind.Le or Lua4TokenKind.Gt or Lua4TokenKind.Ge)
        {
            Lua4TokenKind op = tokens[pos++].Kind;
            Lua4Value r = EvaluateConcat(tokens, ref pos, scopes, upvalues)[0];
            Lua4Value l = left[0];
            bool res = op switch
            {
                Lua4TokenKind.Eq => l.Equals(r),
                Lua4TokenKind.Neq => !l.Equals(r),
                Lua4TokenKind.Lt => CompareValues(l, r) < 0,
                Lua4TokenKind.Le => CompareValues(l, r) <= 0,
                Lua4TokenKind.Gt => CompareValues(l, r) > 0,
                Lua4TokenKind.Ge => CompareValues(l, r) >= 0,
                _ => false,
            };

            // If comparing nil with < <= > >=, result is false (Nil in Lua 4.0)
            if (op is not (Lua4TokenKind.Eq or Lua4TokenKind.Neq) && (l.IsNil || r.IsNil))
            {
                res = false;
            }

            left = [res ? Lua4Value.FromNumber(1) : Lua4Value.Nil];
        }

        return left;
    }

    private Lua4Value[] EvaluateConcat(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        Lua4Value[] left = EvaluateAdditive(tokens, ref pos, scopes, upvalues);
        if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Concat)
        {
            var sb = new StringBuilder(left[0].AsLuaString());
            while (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Concat)
            {
                pos++;
                Lua4Value next = EvaluateAdditive(tokens, ref pos, scopes, upvalues)[0];
                sb.Append(next.AsLuaString());
            }

            return [Lua4Value.FromString(sb.ToString())];
        }

        return left;
    }

    private Lua4Value[] EvaluateAdditive(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        Lua4Value[] left = EvaluateMultiplicative(tokens, ref pos, scopes, upvalues);
        while (pos < tokens.Count && tokens[pos].Kind is Lua4TokenKind.Plus or Lua4TokenKind.Minus)
        {
            Lua4TokenKind op = tokens[pos++].Kind;
            double l = left[0].AsNumberOrDefault(0);
            double r = EvaluateMultiplicative(tokens, ref pos, scopes, upvalues)[0].AsNumberOrDefault(0);
            left = [Lua4Value.FromNumber(op == Lua4TokenKind.Plus ? l + r : l - r)];
        }

        return left;
    }

    private Lua4Value[] EvaluateMultiplicative(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        Lua4Value[] left = EvaluateUnary(tokens, ref pos, scopes, upvalues);
        while (pos < tokens.Count && tokens[pos].Kind is Lua4TokenKind.Star or Lua4TokenKind.Slash or Lua4TokenKind.Percent)
        {
            Lua4TokenKind op = tokens[pos++].Kind;
            double l = left[0].AsNumberOrDefault(0);
            double r = EvaluateUnary(tokens, ref pos, scopes, upvalues)[0].AsNumberOrDefault(0);
            double v = op switch
            {
                Lua4TokenKind.Star => l * r,
                Lua4TokenKind.Slash => r == 0.0 ? 0.0 : l / r,
                Lua4TokenKind.Percent => r == 0.0 ? 0.0 : l % r,
                _ => 0.0,
            };
            left = [Lua4Value.FromNumber(v)];
        }

        return left;
    }

    private Lua4Value[] EvaluateUnary(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Minus)
        {
            pos++;
            double v = EvaluateUnary(tokens, ref pos, scopes, upvalues)[0].AsNumberOrDefault(0);
            return [Lua4Value.FromNumber(-v)];
        }

        if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Not)
        {
            pos++;
            Lua4Value v = EvaluateUnary(tokens, ref pos, scopes, upvalues)[0];
            return [v.IsTruthy ? Lua4Value.Nil : Lua4Value.FromNumber(1)];
        }

        return EvaluatePower(tokens, ref pos, scopes, upvalues);
    }

    private Lua4Value[] EvaluatePower(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        Lua4Value[] baseVal = EvaluatePrimaryAndPostfix(tokens, ref pos, scopes, upvalues);
        if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Caret)
        {
            pos++;
            double exp = EvaluateUnary(tokens, ref pos, scopes, upvalues)[0].AsNumberOrDefault(1);
            return [Lua4Value.FromNumber(Math.Pow(baseVal[0].AsNumberOrDefault(0), exp))];
        }

        return baseVal;
    }

    private Lua4Value[] EvaluatePrimaryAndPostfix(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        Lua4Value[] current = EvaluatePrimary(tokens, ref pos, scopes, upvalues);

        while (pos < tokens.Count)
        {
            Lua4TokenKind k = tokens[pos].Kind;
            if (k == Lua4TokenKind.Dot)
            {
                pos++;
                string field = ExpectIdentifierOrKeyword(tokens, ref pos);
                current = [current[0].GetTableEntry(Lua4Value.FromString(field))];
            }
            else if (k == Lua4TokenKind.LBracket)
            {
                pos++;
                Lua4Value idx = EvaluateExpr(tokens, ref pos, scopes, upvalues);
                Expect(tokens, ref pos, Lua4TokenKind.RBracket);
                current = [current[0].GetTableEntry(idx)];
            }
            else if (k == Lua4TokenKind.Colon)
            {
                pos++;
                string methodName = ExpectIdentifierOrKeyword(tokens, ref pos);
                Lua4Value selfVal = current[0];
                Lua4Value methodFn = selfVal.GetTableEntry(Lua4Value.FromString(methodName));
                Lua4Value[] args = ParseCallArguments(tokens, ref pos, scopes, upvalues, selfVal);
                current = InvokeValue(methodFn, args);
            }
            else if (k is Lua4TokenKind.LParen or Lua4TokenKind.String or Lua4TokenKind.LBrace)
            {
                Lua4Value[] args = ParseCallArguments(tokens, ref pos, scopes, upvalues, null);
                current = InvokeValue(current[0], args);
            }
            else
            {
                break;
            }
        }

        return current.Length > 0 ? current : [Lua4Value.Nil];
    }

    private Lua4Value[] EvaluatePrimary(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        if (pos >= tokens.Count)
        {
            return [Lua4Value.Nil];
        }

        Lua4Token tok = tokens[pos];
        switch (tok.Kind)
        {
            case Lua4TokenKind.Nil:
                pos++;
                return [Lua4Value.Nil];
            case Lua4TokenKind.Number:
                pos++;
                return [Lua4Value.FromNumber(tok.NumberValue)];
            case Lua4TokenKind.String:
                pos++;
                return [Lua4Value.FromString(tok.Text)];
            case Lua4TokenKind.Identifier:
                pos++;
                return [GetVariable(tok.Text, scopes)];
            case Lua4TokenKind.UpvalueIdentifier:
                pos++;
                if (upvalues is not null && upvalues.TryGetValue(tok.Text, out Lua4Value? upv))
                {
                    return [upv];
                }

                return [GetVariable(tok.Text, scopes)];
            case Lua4TokenKind.Function:
                pos++;
                return [ParseFunctionLiteral(tokens, ref pos, scopes, hasImplicitSelf: false)];
            case Lua4TokenKind.LBrace:
                return [ParseTableConstructor(tokens, ref pos, scopes, upvalues)];
            case Lua4TokenKind.LParen:
                pos++;
                Lua4Value inner = EvaluateExpr(tokens, ref pos, scopes, upvalues);
                Expect(tokens, ref pos, Lua4TokenKind.RParen);
                return [inner];
            default:
                pos++;
                return [Lua4Value.Nil];
        }
    }

    private Lua4Value ParseTableConstructor(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues)
    {
        Expect(tokens, ref pos, Lua4TokenKind.LBrace);
        Lua4Value tbl = Lua4Value.CreateTable();
        int autoIndex = 1;

        while (pos < tokens.Count && tokens[pos].Kind != Lua4TokenKind.RBrace && tokens[pos].Kind != Lua4TokenKind.Eof)
        {
            if (tokens[pos].Kind == Lua4TokenKind.LBracket)
            {
                pos++;
                Lua4Value key = EvaluateExpr(tokens, ref pos, scopes, upvalues);
                Expect(tokens, ref pos, Lua4TokenKind.RBracket);
                Expect(tokens, ref pos, Lua4TokenKind.Assign);
                Lua4Value val = EvaluateExpr(tokens, ref pos, scopes, upvalues);
                tbl.SetTableEntry(key, val);
            }
            else if (pos + 1 < tokens.Count &&
                     (tokens[pos].Kind == Lua4TokenKind.Identifier || IsKeywordUsableAsField(tokens[pos].Kind)) &&
                     tokens[pos + 1].Kind == Lua4TokenKind.Assign)
            {
                string keyName = tokens[pos].Text;
                pos += 2;
                Lua4Value val = EvaluateExpr(tokens, ref pos, scopes, upvalues);
                tbl.SetTableEntry(Lua4Value.FromString(keyName), val);
            }
            else
            {
                Lua4Value[] vals = EvaluateFullExprMulti(tokens, ref pos, scopes, upvalues);
                bool isTrailingItem = pos >= tokens.Count || (tokens[pos].Kind != Lua4TokenKind.Comma && tokens[pos].Kind != Lua4TokenKind.Semicolon);
                if (isTrailingItem && vals.Length > 1)
                {
                    for (int k = 0; k < vals.Length; k++)
                    {
                        tbl.SetTableEntry(Lua4Value.FromNumber(autoIndex++), vals[k]);
                    }
                }
                else
                {
                    tbl.SetTableEntry(Lua4Value.FromNumber(autoIndex++), vals.Length > 0 ? vals[0] : Lua4Value.Nil);
                }
            }

            if (pos < tokens.Count && (tokens[pos].Kind == Lua4TokenKind.Comma || tokens[pos].Kind == Lua4TokenKind.Semicolon))
            {
                pos++;
            }
        }

        Expect(tokens, ref pos, Lua4TokenKind.RBrace);
        return tbl;
    }

    private Lua4Value ParseFunctionLiteral(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> enclosingScopes,
        bool hasImplicitSelf)
    {
        Expect(tokens, ref pos, Lua4TokenKind.LParen);
        var paramNames = new List<string>();
        if (hasImplicitSelf)
        {
            paramNames.Add("self");
        }

        bool hasVararg = false;
        while (pos < tokens.Count && tokens[pos].Kind != Lua4TokenKind.RParen && tokens[pos].Kind != Lua4TokenKind.Eof)
        {
            if (tokens[pos].Kind == Lua4TokenKind.Dots)
            {
                hasVararg = true;
                pos++;
            }
            else
            {
                paramNames.Add(ExpectIdentifier(tokens, ref pos));
            }

            if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Comma)
            {
                pos++;
            }
            else
            {
                break;
            }
        }

        Expect(tokens, ref pos, Lua4TokenKind.RParen);
        int bodyStart = pos;
        SkipBlockUntilEnd(tokens, ref pos);

        // Snapshot enclosing locals for Lua 4.0 %upvalue frozen semantics
        var capturedUpvalues = new Dictionary<string, Lua4Value>(StringComparer.Ordinal);
        foreach (Dictionary<string, Lua4Value> sc in enclosingScopes)
        {
            foreach (var kv in sc)
            {
                capturedUpvalues[kv.Key] = kv.Value;
            }
        }

        string[] frozenParams = paramNames.ToArray();
        return Lua4Value.FromFunction(callArgs =>
        {
            var fnScope = new Dictionary<string, Lua4Value>(StringComparer.Ordinal);
            for (int i = 0; i < frozenParams.Length; i++)
            {
                fnScope[frozenParams[i]] = i < callArgs.Length ? callArgs[i] : Lua4Value.Nil;
            }

            if (hasVararg)
            {
                Lua4Value argTbl = Lua4Value.CreateTable();
                int extraCount = Math.Max(0, callArgs.Length - frozenParams.Length);
                for (int i = 0; i < extraCount; i++)
                {
                    argTbl.SetTableEntry(Lua4Value.FromNumber(i + 1), callArgs[frozenParams.Length + i]);
                }

                argTbl.SetTableEntry(Lua4Value.FromString("n"), Lua4Value.FromNumber(extraCount));
                fnScope["arg"] = argTbl;
            }

            var callScopes = new List<Dictionary<string, Lua4Value>> { fnScope };
            int runPos = bodyStart;
            Lua4Value[] ret = ExecuteBlock(tokens, ref runPos, callScopes, capturedUpvalues, out _, out _);
            return ret.Length > 0 ? ret : [Lua4Value.Nil];
        });
    }

    private Lua4Value[] ParseCallArguments(
        List<Lua4Token> tokens,
        ref int pos,
        List<Dictionary<string, Lua4Value>> scopes,
        Dictionary<string, Lua4Value>? upvalues,
        Lua4Value? implicitSelf)
    {
        var args = new List<Lua4Value>();
        if (implicitSelf is not null)
        {
            args.Add(implicitSelf);
        }

        if (pos >= tokens.Count)
        {
            return args.ToArray();
        }

        if (tokens[pos].Kind == Lua4TokenKind.String)
        {
            args.Add(Lua4Value.FromString(tokens[pos].Text));
            pos++;
            return args.ToArray();
        }

        if (tokens[pos].Kind == Lua4TokenKind.LBrace)
        {
            args.Add(ParseTableConstructor(tokens, ref pos, scopes, upvalues));
            return args.ToArray();
        }

        Expect(tokens, ref pos, Lua4TokenKind.LParen);
        if (pos < tokens.Count && tokens[pos].Kind != Lua4TokenKind.RParen)
        {
            args.AddRange(EvaluateExprList(tokens, ref pos, scopes, upvalues));
        }

        Expect(tokens, ref pos, Lua4TokenKind.RParen);
        return args.ToArray();
    }

    private Lua4Value[] InvokeValue(Lua4Value fnVal, Lua4Value[] args)
    {
        if (fnVal.IsFunction && fnVal.Function is not null)
        {
            return fnVal.Function(args);
        }

        if (_lenientMissingGlobals)
        {
            return [Lua4Value.FromNumber(0)];
        }

        return [Lua4Value.Nil];
    }

    private Lua4Value GetVariable(string name, List<Dictionary<string, Lua4Value>> scopes)
    {
        for (int i = scopes.Count - 1; i >= 0; i--)
        {
            if (scopes[i].TryGetValue(name, out Lua4Value? localVal))
            {
                return localVal;
            }
        }

        return _globals.TryGetValue(name, out Lua4Value? globalVal) ? globalVal : Lua4Value.Nil;
    }

    private void SetVariable(string name, Lua4Value value, List<Dictionary<string, Lua4Value>> scopes)
    {
        for (int i = scopes.Count - 1; i >= 0; i--)
        {
            if (scopes[i].ContainsKey(name))
            {
                scopes[i][name] = value;
                return;
            }
        }

        SetGlobal(name, value);
    }

    private static void SkipBlockUntilBranchEnd(List<Lua4Token> tokens, ref int pos)
    {
        int depth = 0;
        while (pos < tokens.Count && tokens[pos].Kind != Lua4TokenKind.Eof)
        {
            Lua4TokenKind k = tokens[pos].Kind;
            if (k is Lua4TokenKind.Function or Lua4TokenKind.If or Lua4TokenKind.While or Lua4TokenKind.For)
            {
                depth++;
                pos++;
                if (k is Lua4TokenKind.While or Lua4TokenKind.For)
                {
                    // Note: 'while ... do' and 'for ... do' include a 'do' token that closes with the same 'end'
                    while (pos < tokens.Count && tokens[pos].Kind != Lua4TokenKind.Do && tokens[pos].Kind != Lua4TokenKind.Eof)
                    {
                        if (tokens[pos].Kind == Lua4TokenKind.Function)
                        {
                            depth++;
                        }

                        pos++;
                    }

                    if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Do)
                    {
                        pos++;
                    }
                }

                continue;
            }

            if (k == Lua4TokenKind.Do)
            {
                depth++;
                pos++;
                continue;
            }

            if (k == Lua4TokenKind.Repeat)
            {
                depth++;
                pos++;
                continue;
            }

            if (k == Lua4TokenKind.Until)
            {
                if (depth == 0)
                {
                    return;
                }

                depth--;
                pos++;
                continue;
            }

            if (k is Lua4TokenKind.Else or Lua4TokenKind.ElseIf)
            {
                if (depth == 0)
                {
                    return;
                }

                pos++;
                continue;
            }

            if (k == Lua4TokenKind.End)
            {
                if (depth == 0)
                {
                    return;
                }

                depth--;
                pos++;
                continue;
            }

            pos++;
        }
    }

    private static void SkipBlockUntilEnd(List<Lua4Token> tokens, ref int pos)
    {
        int depth = 0;
        while (pos < tokens.Count && tokens[pos].Kind != Lua4TokenKind.Eof)
        {
            Lua4TokenKind k = tokens[pos].Kind;
            if (k is Lua4TokenKind.Function or Lua4TokenKind.If or Lua4TokenKind.While or Lua4TokenKind.For)
            {
                depth++;
                pos++;
                if (k is Lua4TokenKind.While or Lua4TokenKind.For)
                {
                    while (pos < tokens.Count && tokens[pos].Kind != Lua4TokenKind.Do && tokens[pos].Kind != Lua4TokenKind.Eof)
                    {
                        if (tokens[pos].Kind == Lua4TokenKind.Function)
                        {
                            depth++;
                        }

                        pos++;
                    }

                    if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Do)
                    {
                        pos++;
                    }
                }

                continue;
            }

            if (k == Lua4TokenKind.Do)
            {
                depth++;
                pos++;
                continue;
            }

            if (k == Lua4TokenKind.Repeat)
            {
                depth++;
                pos++;
                continue;
            }

            if (k == Lua4TokenKind.Until)
            {
                depth = Math.Max(0, depth - 1);
                pos++;
                continue;
            }

            if (k == Lua4TokenKind.End)
            {
                if (depth == 0)
                {
                    pos++;
                    return;
                }

                depth--;
                pos++;
                continue;
            }

            pos++;
        }
    }

    private static int CompareValues(Lua4Value l, Lua4Value r)
    {
        if (l.TryToNumber(out double ln) && r.TryToNumber(out double rn))
        {
            return ln.CompareTo(rn);
        }

        return string.CompareOrdinal(l.AsLuaString(), r.AsLuaString());
    }

    private static double ArgNum(Lua4Value[] args, int index, double fallback = 0.0) =>
        index < args.Length ? args[index].AsNumberOrDefault(fallback) : fallback;

    private static Lua4Value[] FormatLuaString(Lua4Value[] args)
    {
        if (args.Length == 0)
        {
            return [Lua4Value.FromString(string.Empty)];
        }

        string fmt = args[0].AsLuaString();
        var sb = new StringBuilder(fmt.Length + 16);
        int argIdx = 1;
        for (int i = 0; i < fmt.Length; i++)
        {
            if (fmt[i] != '%' || i + 1 >= fmt.Length)
            {
                sb.Append(fmt[i]);
                continue;
            }

            i++;
            if (fmt[i] == '%')
            {
                sb.Append('%');
                continue;
            }

            while (i < fmt.Length && (fmt[i] is '-' or '+' or ' ' or '0' or '#' or '.' || (fmt[i] is >= '0' and <= '9')))
            {
                i++;
            }

            if (i >= fmt.Length)
            {
                break;
            }

            char spec = fmt[i];
            Lua4Value v = argIdx < args.Length ? args[argIdx++] : Lua4Value.Nil;
            switch (spec)
            {
                case 'd':
                case 'i':
                case 'u':
                    sb.Append(((long)v.AsNumberOrDefault(0)).ToString(CultureInfo.InvariantCulture));
                    break;
                case 'f':
                case 'g':
                case 'e':
                    sb.Append(v.AsNumberOrDefault(0).ToString("G15", CultureInfo.InvariantCulture));
                    break;
                case 'x':
                    sb.Append(((long)v.AsNumberOrDefault(0)).ToString("x", CultureInfo.InvariantCulture));
                    break;
                case 'X':
                    sb.Append(((long)v.AsNumberOrDefault(0)).ToString("X", CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append(v.AsLuaString());
                    break;
            }
        }

        return [Lua4Value.FromString(sb.ToString())];
    }

    private static void Expect(List<Lua4Token> tokens, ref int pos, Lua4TokenKind expected)
    {
        if (pos < tokens.Count && tokens[pos].Kind == expected)
        {
            pos++;
        }
    }

    private static string ExpectIdentifier(List<Lua4Token> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos].Kind == Lua4TokenKind.Identifier)
        {
            return tokens[pos++].Text;
        }

        if (pos < tokens.Count && IsKeywordUsableAsField(tokens[pos].Kind))
        {
            return tokens[pos++].Text;
        }

        return "_";
    }

    private static string ExpectIdentifierOrKeyword(List<Lua4Token> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos].Kind != Lua4TokenKind.Eof)
        {
            return tokens[pos++].Text;
        }

        return "_";
    }

    private static bool IsKeywordUsableAsField(Lua4TokenKind k) =>
        k is >= Lua4TokenKind.And and <= Lua4TokenKind.While;
}
