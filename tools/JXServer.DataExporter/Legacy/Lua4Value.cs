using System;
using System.Collections.Generic;
using System.Globalization;

namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Runtime dynamic value (<c>nil</c>, <c>number</c>, <c>string</c>, <c>table</c>, or <c>function</c>)
/// inside <see cref="Lua4Interpreter"/>.
/// </summary>
public sealed class Lua4Value : IEquatable<Lua4Value>
{
    public static Lua4Value Nil { get; } = new(0, 0.0, null, null, null);

    public int Kind { get; } // 0 = nil, 1 = number, 2 = string, 3 = table, 4 = function

    public double Number { get; }

    public string? Text { get; }

    public Dictionary<Lua4Value, Lua4Value>? Table { get; }

    public Func<Lua4Value[], Lua4Value[]>? Function { get; }

    public bool IsNil => Kind == 0;

    public bool IsNumber => Kind == 1;

    public bool IsString => Kind == 2;

    public bool IsTable => Kind == 3;

    public bool IsFunction => Kind == 4;

    /// <summary>
    /// In Lua 4.0, ONLY <c>nil</c> is falsy (<c>0</c> and <c>""</c> are truthy).
    /// </summary>
    public bool IsTruthy => Kind != 0;

    private Lua4Value(
        int kind,
        double number,
        string? text,
        Dictionary<Lua4Value, Lua4Value>? table,
        Func<Lua4Value[], Lua4Value[]>? function)
    {
        Kind = kind;
        Number = number;
        Text = text;
        Table = table;
        Function = function;
    }

    public static Lua4Value FromNumber(double value) =>
        double.IsNaN(value) ? new Lua4Value(1, 0.0, null, null, null) : new Lua4Value(1, value, null, null, null);

    public static Lua4Value FromString(string? value) =>
        value is null ? Nil : new Lua4Value(2, 0.0, value, null, null);

    public static Lua4Value CreateTable() =>
        new(3, 0.0, null, new Dictionary<Lua4Value, Lua4Value>(), null);

    public static Lua4Value FromFunction(Func<Lua4Value[], Lua4Value[]> fn) =>
        new(4, 0.0, null, null, fn);

    public bool TryToNumber(out double num)
    {
        if (Kind == 1)
        {
            num = Number;
            return true;
        }

        if (Kind == 2 && Text is not null)
        {
            string trimmed = Text.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hv))
            {
                num = hv;
                return true;
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out num))
            {
                return true;
            }
        }

        num = 0.0;
        return false;
    }

    public double AsNumberOrDefault(double fallback = 0.0) =>
        TryToNumber(out double n) ? n : fallback;

    public string AsLuaString()
    {
        if (Kind == 2)
        {
            return Text ?? string.Empty;
        }

        if (Kind == 1)
        {
            if (Math.Abs(Number - Math.Truncate(Number)) < 1e-9 &&
                Number is >= long.MinValue and <= long.MaxValue)
            {
                return ((long)Math.Truncate(Number)).ToString(CultureInfo.InvariantCulture);
            }

            return Number.ToString("G15", CultureInfo.InvariantCulture);
        }

        if (Kind == 0)
        {
            return string.Empty;
        }

        return Kind == 3 ? "table" : "function";
    }

    public Lua4Value GetTableEntry(Lua4Value key)
    {
        if (Table is null || key.IsNil)
        {
            return Nil;
        }

        return Table.TryGetValue(key, out Lua4Value? val) ? val : Nil;
    }

    public void SetTableEntry(Lua4Value key, Lua4Value value)
    {
        if (Table is null || key.IsNil)
        {
            return;
        }

        if (value.IsNil)
        {
            Table.Remove(key);
        }
        else
        {
            Table[key] = value;
        }
    }

    public int GetTableN()
    {
        if (Table is null)
        {
            return 0;
        }

        if (Table.TryGetValue(FromString("n"), out Lua4Value? explicitN) && explicitN.TryToNumber(out double nd))
        {
            return Math.Max(0, (int)nd);
        }

        int maxIdx = 0;
        foreach (Lua4Value k in Table.Keys)
        {
            if (k.Kind == 1 && k.Number >= 1 && Math.Abs(k.Number - Math.Truncate(k.Number)) < 1e-9)
            {
                int idx = (int)k.Number;
                if (idx > maxIdx)
                {
                    maxIdx = idx;
                }
            }
        }

        return maxIdx;
    }

    public bool Equals(Lua4Value? other)
    {
        if (other is null || Kind != other.Kind)
        {
            return false;
        }

        return Kind switch
        {
            0 => true,
            1 => Number == other.Number,
            2 => string.Equals(Text, other.Text, StringComparison.Ordinal),
            3 => ReferenceEquals(Table, other.Table),
            4 => ReferenceEquals(Function, other.Function),
            _ => false,
        };
    }

    public override bool Equals(object? obj) => Equals(obj as Lua4Value);

    public override int GetHashCode() =>
        Kind switch
        {
            0 => 0,
            1 => HashCode.Combine(1, Number),
            2 => HashCode.Combine(2, StringComparer.Ordinal.GetHashCode(Text ?? string.Empty)),
            3 => HashCode.Combine(3, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Table)),
            4 => HashCode.Combine(4, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Function)),
            _ => 0,
        };
}
