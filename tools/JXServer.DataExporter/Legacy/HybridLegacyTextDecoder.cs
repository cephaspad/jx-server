using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JXServer.Primitives.Encodings;

namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Hybrid per-cell byte heuristic decoder (<c>TCVN3</c> Vietnamese vs <c>GBK</c> Chinese) and deterministic
/// ASCII <c>kebab-case</c> slugifier + symmetric <c>code</c> disambiguator (`JXData Standard v1`).
/// </summary>
public static class HybridLegacyTextDecoder
{
    private static readonly bool[] ValidTcvn3HighBytes = BuildValidTcvn3HighByteMask();
    private static readonly (ushort Code, string Pinyin)[] Gb2312PinyinBounds = BuildGb2312PinyinBounds();
    private static readonly FrozenDictionary<char, string> ExtraHanziPinyin = BuildExtraHanziPinyin();

    /// <summary>
    /// Decodes a raw byte span using per-cell byte-range heuristics to preserve both Vietnamese TCVN3 text
    /// and untranslated Chinese GBK text without mojibake.
    /// </summary>
    public static string DecodeSmart(ReadOnlySpan<byte> rawBytes, bool preferVietnamese = true)
    {
        int nullIdx = rawBytes.IndexOf((byte)0);
        if (nullIdx >= 0)
        {
            rawBytes = rawBytes[..nullIdx];
        }

        if (rawBytes.IsEmpty)
        {
            return string.Empty;
        }

        bool hasHighBytes = false;
        for (int i = 0; i < rawBytes.Length; i++)
        {
            if (rawBytes[i] >= 0x80)
            {
                hasHighBytes = true;
                break;
            }
        }

        if (!hasHighBytes)
        {
            return Encoding.ASCII.GetString(rawBytes);
        }

        string decoded;
        if (!preferVietnamese)
        {
            decoded = IsLikelyVietnameseTcvn3(rawBytes, strictAsciiAdjacency: true)
                ? DecodeTcvn3WithPunctuation(rawBytes)
                : GbkEncoding.DecodeNullTerminated(rawBytes);
        }
        else
        {
            decoded = IsLikelyVietnameseTcvn3(rawBytes, strictAsciiAdjacency: false)
                ? DecodeTcvn3WithPunctuation(rawBytes)
                : GbkEncoding.DecodeNullTerminated(rawBytes);
        }

        return SanitizeNonStandardCodePoints(decoded);
    }

    /// <summary>
    /// Unescapes Lua 4.0 decimal byte escapes (<c>\ddd</c>, <c>\n</c>, <c>\t</c>, <c>\\</c>, <c>\"</c>) before decoding.
    /// </summary>
    public static byte[] UnescapeLuaStringBytes(ReadOnlySpan<byte> raw)
    {
        if (raw.IsEmpty)
        {
            return [];
        }

        byte[] output = new byte[raw.Length];
        int outIdx = 0;
        int i = 0;
        while (i < raw.Length)
        {
            if (raw[i] == (byte)'\\' && i + 1 < raw.Length)
            {
                byte next = raw[i + 1];
                if (next is >= (byte)'0' and <= (byte)'9')
                {
                    int val = next - '0';
                    int consumed = 2;
                    if (i + 2 < raw.Length && raw[i + 2] is >= (byte)'0' and <= (byte)'9')
                    {
                        val = (val * 10) + (raw[i + 2] - '0');
                        consumed++;
                        if (i + 3 < raw.Length && raw[i + 3] is >= (byte)'0' and <= (byte)'9')
                        {
                            int candidate = (val * 10) + (raw[i + 3] - '0');
                            if (candidate <= 255)
                            {
                                val = candidate;
                                consumed++;
                            }
                        }
                    }

                    output[outIdx++] = (byte)(val & 0xFF);
                    i += consumed;
                    continue;
                }

                output[outIdx++] = next switch
                {
                    (byte)'n' => (byte)'\n',
                    (byte)'r' => (byte)'\r',
                    (byte)'t' => (byte)'\t',
                    (byte)'\\' => (byte)'\\',
                    (byte)'"' => (byte)'"',
                    (byte)'\'' => (byte)'\'',
                    _ => next,
                };
                i += 2;
                continue;
            }

            output[outIdx++] = raw[i++];
        }

        return output[..outIdx];
    }

    /// <summary>
    /// Converts any Vietnamese, ASCII, or CJK display string into a strict <c>^[a-z0-9]+(-[a-z0-9]+)*$</c> slug.
    /// </summary>
    public static string Slugify(string? text, string fallback = "entry")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        string decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length * 2);
        bool prevDash = true;

        for (int i = 0; i < decomposed.Length; i++)
        {
            char ch = decomposed[i];
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch is 'đ' or 'Đ')
            {
                sb.Append('d');
                prevDash = false;
                continue;
            }

            if (ch is >= 'a' and <= 'z')
            {
                sb.Append(ch);
                prevDash = false;
                continue;
            }

            if (ch is >= 'A' and <= 'Z')
            {
                sb.Append((char)(ch + 32));
                prevDash = false;
                continue;
            }

            if (ch is >= '0' and <= '9')
            {
                sb.Append(ch);
                prevDash = false;
                continue;
            }

            if (IsCjkIdeograph(ch))
            {
                if (!prevDash && sb.Length > 0)
                {
                    sb.Append('-');
                }

                sb.Append(TransliterateHanziToPinyin(ch));
                sb.Append('-');
                prevDash = true;
                continue;
            }

            if (!prevDash && sb.Length > 0)
            {
                sb.Append('-');
                prevDash = true;
            }
        }

        while (sb.Length > 0 && sb[^1] == '-')
        {
            sb.Length--;
        }

        return sb.Length > 0 ? sb.ToString() : fallback;
    }

    /// <summary>
    /// Applies the <c>JXData Standard v1</c> Symmetric Disambiguation rule for ID-based catalogs:
    /// unique slug -&gt; <c>&lt;slug&gt;</c>; if 2+ records share <c>&lt;slug&gt;</c>, ALL colliding records receive <c>&lt;slug&gt;-&lt;id&gt;</c>.
    /// </summary>
    public static string[] AssignSymmetricCodes<T>(
        IReadOnlyList<T> items,
        Func<T, string> baseSlugSelector,
        Func<T, int> idSelector)
    {
        var slugs = new string[items.Count];
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < items.Count; i++)
        {
            string s = Slugify(baseSlugSelector(items[i]));
            slugs[i] = s;
            counts[s] = counts.GetValueOrDefault(s) + 1;
        }

        var codes = new string[items.Count];
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Count; i++)
        {
            string baseSlug = slugs[i];
            int id = idSelector(items[i]);
            string candidate = counts[baseSlug] > 1 ? $"{baseSlug}-{id}" : baseSlug;
            if (!used.Add(candidate))
            {
                candidate = $"{baseSlug}-{id}";
                used.Add(candidate);
            }

            codes[i] = candidate;
        }

        return codes;
    }

    private static bool IsLikelyVietnameseTcvn3(ReadOnlySpan<byte> raw, bool strictAsciiAdjacency)
    {
        int highCount = 0;
        int invalidTcvnCount = 0;
        int asciiLetterCount = 0;
        int adjacentAsciiHighPairs = 0;
        int consecutiveHighRun = 0;
        int maxConsecutiveHighRun = 0;

        for (int i = 0; i < raw.Length; i++)
        {
            byte b = raw[i];
            if (b >= 0x80)
            {
                if (IsWin1252Punctuation(b))
                {
                    consecutiveHighRun = 0;
                    continue;
                }

                if (!ValidTcvn3HighBytes[b])
                {
                    invalidTcvnCount++;
                    consecutiveHighRun = 0;
                    continue;
                }

                highCount++;
                consecutiveHighRun++;
                if (consecutiveHighRun > maxConsecutiveHighRun)
                {
                    maxConsecutiveHighRun = consecutiveHighRun;
                }

                bool prevAsciiLetter = i > 0 && IsAsciiLetter(raw[i - 1]);
                bool nextAsciiLetter = i + 1 < raw.Length && IsAsciiLetter(raw[i + 1]);
                if (prevAsciiLetter || nextAsciiLetter)
                {
                    adjacentAsciiHighPairs++;
                }
            }
            else
            {
                consecutiveHighRun = 0;
                if (IsAsciiLetter(b))
                {
                    asciiLetterCount++;
                }
            }
        }

        if (invalidTcvnCount > 0)
        {
            // Only tolerate up to 2 stray non-TCVN3 bytes if the text is overwhelmingly Vietnamese Latin+TCVN3
            if (invalidTcvnCount > 2 || adjacentAsciiHighPairs < 4 || maxConsecutiveHighRun >= 4)
            {
                return false;
            }

            return true;
        }

        // No Vietnamese word has >= 4 consecutive non-ASCII letters without an ASCII letter or space
        if (maxConsecutiveHighRun >= 4)
        {
            return false;
        }

        if (strictAsciiAdjacency)
        {
            return adjacentAsciiHighPairs > 0;
        }

        if (adjacentAsciiHighPairs > 0)
        {
            return true;
        }

        // Zero ASCII letters: only valid in Vietnamese for 1-byte interjections or 2-byte words starting with Đ/đ (0xAE/0xA7)
        if (asciiLetterCount == 0 && highCount >= 2)
        {
            return highCount == 2 && (raw[0] == 0xAE || raw[0] == 0xA7);
        }

        return true;
    }

    private static bool IsWin1252Punctuation(byte b) =>
        b is 0x85 or 0x91 or 0x92 or 0x93 or 0x94 or 0x95 or 0x96 or 0x97 or 0x99 or 0xA0 or 0xB0 or 0xB1;

    private static string DecodeTcvn3WithPunctuation(ReadOnlySpan<byte> raw)
    {
        var sb = new StringBuilder(raw.Length);
        int segStart = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            byte b = raw[i];
            if (IsWin1252Punctuation(b))
            {
                if (i > segStart)
                {
                    sb.Append(Tcvn3Encoding.DecodeNullTerminated(raw[segStart..i]));
                }

                sb.Append(b switch
                {
                    0x85 => '…',
                    0x91 => '‘',
                    0x92 => '’',
                    0x93 => '“',
                    0x94 => '”',
                    0x95 => '•',
                    0x96 => '–',
                    0x97 => '—',
                    0x99 => '™',
                    0xA0 => ' ',
                    0xB0 => '°',
                    0xB1 => '±',
                    _ => ' ',
                });
                segStart = i + 1;
            }
        }

        if (segStart < raw.Length)
        {
            sb.Append(Tcvn3Encoding.DecodeNullTerminated(raw[segStart..]));
        }

        return sb.ToString();
    }

    private static string SanitizeNonStandardCodePoints(string text)
    {
        bool needsClean = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if ((c >= '\u0080' && c <= '\u009F') || (c >= '\uE000' && c <= '\uF8FF'))
            {
                needsClean = true;
                break;
            }
        }

        if (!needsClean)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c >= '\u0080' && c <= '\u009F')
            {
                continue;
            }

            sb.Append(c >= '\uE000' && c <= '\uF8FF' ? '?' : c);
        }

        return sb.ToString();
    }

    private static bool IsAsciiLetter(byte b) =>
        (b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'a' && b <= (byte)'z');

    private static bool IsCjkIdeograph(char ch) =>
        (ch >= '\u4E00' && ch <= '\u9FFF') || (ch >= '\u3400' && ch <= '\u4DBF');

    private static string TransliterateHanziToPinyin(char ch)
    {
        if (ExtraHanziPinyin.TryGetValue(ch, out string? custom))
        {
            return custom;
        }

        Span<char> oneChar = [ch];
        byte[] gbk = GbkEncoding.Instance.GetBytes(oneChar.ToString());
        if (gbk.Length == 2)
        {
            ushort code = (ushort)((gbk[0] << 8) | gbk[1]);
            if (code is >= 0xB0A1 and <= 0xD7F9)
            {
                int lo = 0;
                int hi = Gb2312PinyinBounds.Length - 1;
                int best = 0;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (Gb2312PinyinBounds[mid].Code <= code)
                    {
                        best = mid;
                        lo = mid + 1;
                    }
                    else
                    {
                        hi = mid - 1;
                    }
                }

                return Gb2312PinyinBounds[best].Pinyin;
            }
        }

        return $"u{((int)ch):x4}";
    }

    private static bool[] BuildValidTcvn3HighByteMask()
    {
        bool[] mask = new bool[256];
        ReadOnlySpan<(byte Start, byte End)> ranges =
        [
            (0xA1, 0xAE),
            (0xB5, 0xB9),
            (0xBB, 0xBE),
            (0xC6, 0xCC),
            (0xCE, 0xDF),
            (0xE1, 0xEF),
            (0xF1, 0xFE),
        ];

        foreach ((byte s, byte e) in ranges)
        {
            for (int b = s; b <= e; b++)
            {
                mask[b] = true;
            }
        }

        return mask;
    }

    private static FrozenDictionary<char, string> BuildExtraHanziPinyin() =>
        new Dictionary<char, string>
        {
            ['侠'] = "xia", ['丐'] = "gai", ['逍'] = "xiao", ['遥'] = "yao",
            ['峨'] = "e", ['嵋'] = "mei", ['嵋'] = "mei", ['仑'] = "lun", ['崙'] = "lun",
            ['镖'] = "biao", ['麒'] = "qi", ['麟'] = "lin", ['凰'] = "huang",
            ['玄'] = "xuan", ['辕'] = "yuan", ['雀'] = "que", ['诀'] = "jue",
            ['罡'] = "gang", ['煞'] = "sha", ['魅'] = "mei", ['魍'] = "wang", ['魉'] = "liang",
            ['乾'] = "qian", ['坤'] = "kun", ['坎'] = "kan", ['离'] = "li", ['震'] = "zhen", ['巽'] = "xun", ['艮'] = "gen", ['兑'] = "dui",
            ['鎏'] = "liu", ['金'] = "jin", ['翡'] = "fei", ['翠'] = "cui", ['琉'] = "liu", ['璃'] = "li",
            ['琥'] = "hu", ['珀'] = "po", ['玛'] = "ma", ['瑙'] = "nao", ['珊'] = "shan", ['瑚'] = "hu",
            ['璎'] = "ying", ['珞'] = "luo", ['簪'] = "zan", ['钗'] = "chai", ['镯'] = "zhuo", ['佩'] = "pei",
            ['靴'] = "xue", ['铠'] = "kai", ['胄'] = "zhou", ['袍'] = "pao", ['衫'] = "shan", ['裳'] = "shang",
            ['剑'] = "jian", ['刀'] = "dao", ['枪'] = "qiang", ['棍'] = "gun", ['锤'] = "chui", ['镖'] = "biao", ['弩'] = "nu",
        }.ToFrozenDictionary();

    private static (ushort Code, string Pinyin)[] BuildGb2312PinyinBounds()
    {
        // Standard 396-entry GB2312 Level-1 Pinyin boundary table (signed int16 cast to uint16: -20319 = 0xB0A1 .. -10256 = 0xD7F0)
        (int SignedCode, string Pinyin)[] raw =
        [
            (-20319, "a"), (-20317, "ai"), (-20304, "an"), (-20295, "ang"), (-20292, "ao"),
            (-20283, "ba"), (-20265, "bai"), (-20257, "ban"), (-20242, "bang"), (-20230, "bao"),
            (-20051, "bei"), (-20036, "ben"), (-20032, "beng"), (-20026, "bi"), (-20002, "bian"),
            (-19990, "biao"), (-19986, "bie"), (-19982, "bin"), (-19976, "bing"), (-19805, "bo"),
            (-19784, "bu"), (-19775, "ca"), (-19774, "cai"), (-19763, "can"), (-19756, "cang"),
            (-19751, "cao"), (-19746, "ce"), (-19741, "ceng"), (-19739, "cha"), (-19728, "chai"),
            (-19725, "chan"), (-19715, "chang"), (-19540, "chao"), (-19531, "che"), (-19525, "chen"),
            (-19515, "cheng"), (-19500, "chi"), (-19484, "chong"), (-19479, "chou"), (-19467, "chu"),
            (-19289, "chuai"), (-19288, "chuan"), (-19281, "chuang"), (-19275, "chui"), (-19270, "chun"),
            (-19263, "chuo"), (-19261, "ci"), (-19249, "cong"), (-19243, "cou"), (-19242, "cu"),
            (-19238, "cuan"), (-19235, "cui"), (-19227, "cun"), (-19224, "cuo"), (-19218, "da"),
            (-19212, "dai"), (-19038, "dan"), (-19023, "dang"), (-19018, "dao"), (-19006, "de"),
            (-19003, "deng"), (-18996, "di"), (-18977, "dian"), (-18961, "diao"), (-18952, "die"),
            (-18783, "ding"), (-18774, "diu"), (-18773, "dong"), (-18763, "dou"), (-18756, "du"),
            (-18741, "duan"), (-18735, "dui"), (-18731, "dun"), (-18722, "duo"), (-18710, "e"),
            (-18697, "en"), (-18696, "er"), (-18526, "fa"), (-18518, "fan"), (-18501, "fang"),
            (-18490, "fei"), (-18478, "fen"), (-18463, "feng"), (-18448, "fo"), (-18447, "fou"),
            (-18446, "fu"), (-18239, "ga"), (-18237, "gai"), (-18231, "gan"), (-18220, "gang"),
            (-18211, "gao"), (-18201, "ge"), (-18184, "gei"), (-18183, "gen"), (-18181, "geng"),
            (-18012, "gong"), (-17997, "gou"), (-17988, "gu"), (-17970, "gua"), (-17964, "guai"),
            (-17961, "guan"), (-17950, "guang"), (-17947, "gui"), (-17931, "gun"), (-17928, "guo"),
            (-17922, "ha"), (-17759, "hai"), (-17752, "han"), (-17733, "hang"), (-17730, "hao"),
            (-17721, "he"), (-17703, "hei"), (-17701, "hen"), (-17697, "heng"), (-17692, "hong"),
            (-17683, "hou"), (-17676, "hu"), (-17496, "hua"), (-17487, "huai"), (-17482, "huan"),
            (-17468, "huang"), (-17454, "hui"), (-17433, "hun"), (-17427, "huo"), (-17417, "ji"),
            (-17202, "jia"), (-17185, "jian"), (-16983, "jiang"), (-16970, "jiao"), (-16942, "jie"),
            (-16915, "jin"), (-16733, "jing"), (-16708, "jiong"), (-16706, "jiu"), (-16689, "ju"),
            (-16664, "juan"), (-16657, "jue"), (-16647, "jun"), (-16474, "ka"), (-16470, "kai"),
            (-16465, "kan"), (-16459, "kang"), (-16452, "kao"), (-16448, "ke"), (-16433, "ken"),
            (-16429, "keng"), (-16427, "kong"), (-16423, "kou"), (-16419, "ku"), (-16412, "kua"),
            (-16407, "kuai"), (-16403, "kuan"), (-16401, "kuang"), (-16393, "kui"), (-16220, "kun"),
            (-16216, "kuo"), (-16212, "la"), (-16205, "lai"), (-16202, "lan"), (-16187, "lang"),
            (-16180, "lao"), (-16171, "le"), (-16169, "lei"), (-16158, "leng"), (-16155, "li"),
            (-15959, "lia"), (-15958, "lian"), (-15944, "liang"), (-15933, "liao"), (-15920, "lie"),
            (-15915, "lin"), (-15903, "ling"), (-15889, "liu"), (-15878, "long"), (-15707, "lou"),
            (-15701, "lu"), (-15681, "lv"), (-15667, "luan"), (-15661, "lue"), (-15659, "lun"),
            (-15652, "luo"), (-15640, "ma"), (-15631, "mai"), (-15625, "man"), (-15454, "mang"),
            (-15448, "mao"), (-15436, "me"), (-15435, "mei"), (-15419, "men"), (-15416, "meng"),
            (-15408, "mi"), (-15394, "mian"), (-15385, "miao"), (-15377, "mie"), (-15375, "min"),
            (-15369, "ming"), (-15363, "miu"), (-15362, "mo"), (-15183, "mou"), (-15180, "mu"),
            (-15165, "na"), (-15158, "nai"), (-15153, "nan"), (-15150, "nang"), (-15149, "nao"),
            (-15144, "ne"), (-15143, "nei"), (-15141, "nen"), (-15140, "neng"), (-15139, "ni"),
            (-15128, "nian"), (-15121, "niang"), (-15119, "niao"), (-15117, "nie"), (-15110, "nin"),
            (-15109, "ning"), (-14941, "niu"), (-14937, "nong"), (-14933, "nu"), (-14930, "nv"),
            (-14929, "nuan"), (-14928, "nue"), (-14926, "nuo"), (-14922, "o"), (-14921, "ou"),
            (-14914, "pa"), (-14908, "pai"), (-14902, "pan"), (-14894, "pang"), (-14889, "pao"),
            (-14882, "pei"), (-14873, "pen"), (-14871, "peng"), (-14857, "pi"), (-14678, "pian"),
            (-14674, "piao"), (-14670, "pie"), (-14668, "pin"), (-14663, "ping"), (-14654, "po"),
            (-14645, "pu"), (-14630, "qi"), (-14594, "qia"), (-14429, "qian"), (-14407, "qiang"),
            (-14399, "qiao"), (-14384, "qie"), (-14379, "qin"), (-14368, "qing"), (-14355, "qiong"),
            (-14353, "qiu"), (-14345, "qu"), (-14170, "quan"), (-14159, "que"), (-14151, "qun"),
            (-14149, "ran"), (-14145, "rang"), (-14140, "rao"), (-14137, "re"), (-14135, "ren"),
            (-14125, "reng"), (-14123, "ri"), (-14122, "rong"), (-14112, "rou"), (-14109, "ru"),
            (-14099, "ruan"), (-14097, "rui"), (-14094, "run"), (-14092, "ruo"), (-14090, "sa"),
            (-14087, "sai"), (-14083, "san"), (-13917, "sang"), (-13914, "sao"), (-13910, "se"),
            (-13907, "sen"), (-13906, "seng"), (-13905, "sha"), (-13896, "shai"), (-13894, "shan"),
            (-13878, "shang"), (-13870, "shao"), (-13859, "she"), (-13847, "shen"), (-13831, "sheng"),
            (-13658, "shi"), (-13611, "shou"), (-13601, "shu"), (-13406, "shua"), (-13404, "shuai"),
            (-13400, "shuan"), (-13398, "shuang"), (-13395, "shui"), (-13391, "shun"), (-13387, "shuo"),
            (-13383, "si"), (-13367, "song"), (-13359, "sou"), (-13356, "su"), (-13343, "suan"),
            (-13340, "sui"), (-13329, "sun"), (-13326, "suo"), (-13318, "ta"), (-13147, "tai"),
            (-13138, "tan"), (-13120, "tang"), (-13107, "tao"), (-13096, "te"), (-13095, "teng"),
            (-13091, "ti"), (-13076, "tian"), (-13068, "tiao"), (-13063, "tie"), (-13060, "ting"),
            (-12888, "tong"), (-12875, "tou"), (-12871, "tu"), (-12860, "tuan"), (-12858, "tui"),
            (-12852, "tun"), (-12849, "tuo"), (-12838, "wa"), (-12831, "wai"), (-12829, "wan"),
            (-12812, "wang"), (-12802, "wei"), (-12607, "wen"), (-12597, "weng"), (-12594, "wo"),
            (-12585, "wu"), (-12556, "xi"), (-12359, "xia"), (-12346, "xian"), (-12320, "xiang"),
            (-12300, "xiao"), (-12120, "xie"), (-12099, "xin"), (-12089, "xing"), (-12074, "xiong"),
            (-12067, "xiu"), (-12058, "xu"), (-12039, "xuan"), (-11867, "xue"), (-11861, "xun"),
            (-11847, "ya"), (-11831, "yan"), (-11798, "yang"), (-11781, "yao"), (-11604, "ye"),
            (-11589, "yi"), (-11536, "yin"), (-11358, "ying"), (-11340, "yo"), (-11339, "yong"),
            (-11324, "you"), (-11303, "yu"), (-11097, "yuan"), (-11077, "yue"), (-11067, "yun"),
            (-11055, "za"), (-11052, "zai"), (-11045, "zan"), (-11041, "zang"), (-11038, "zao"),
            (-11024, "ze"), (-11020, "zei"), (-11019, "zen"), (-11018, "zeng"), (-11014, "zha"),
            (-10838, "zhai"), (-10832, "zhan"), (-10815, "zhang"), (-10800, "zhao"), (-10790, "zhe"),
            (-10780, "zhen"), (-10764, "zheng"), (-10587, "zhi"), (-10544, "zhong"), (-10533, "zhou"),
            (-10519, "zhu"), (-10331, "zhua"), (-10329, "zhuai"), (-10328, "zhuan"), (-10322, "zhuang"),
            (-10315, "zhui"), (-10309, "zhun"), (-10307, "zhuo"), (-10296, "zi"), (-10281, "zong"),
            (-10274, "zou"), (-10270, "zu"), (-10262, "zuan"), (-10260, "zui"), (-10256, "zun"),
            (-10254, "zuo"),
        ];

        var table = new (ushort Code, string Pinyin)[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            table[i] = (unchecked((ushort)(short)raw[i].SignedCode), raw[i].Pinyin);
        }

        return table;
    }
}
