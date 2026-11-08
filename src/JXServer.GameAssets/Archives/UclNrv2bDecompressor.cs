namespace JXServer.GameAssets.Archives;

/// <summary>
/// Bit-exact implementation of <c>ucl_nrv2b_decompress_8</c> (<c>0x0822ab70</c>) for decompressing Kingsoft <c>.pak</c> entries.
/// </summary>
public static class UclNrv2bDecompressor
{
    /// <summary>
    /// Decompresses a UCL NRV2B_8 compressed byte span into <paramref name="dst"/> and returns the number of bytes written.
    /// </summary>
    public static int Decompress8(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        uint bb = 0;
        int ilen = 0;
        int olen = 0;
        uint lastMOff = 1;

        while (true)
        {
            int bit = GetBit(src, ref ilen, ref bb);
            while (bit == 1)
            {
                if (ilen >= src.Length || olen >= dst.Length)
                {
                    return olen;
                }

                dst[olen++] = src[ilen++];
                bit = GetBit(src, ref ilen, ref bb);
            }

            uint mOff = 1;
            do
            {
                mOff = (mOff * 2u) + (uint)GetBit(src, ref ilen, ref bb);
            }
            while (GetBit(src, ref ilen, ref bb) == 0);

            if (mOff == 2)
            {
                mOff = lastMOff;
            }
            else
            {
                if (ilen >= src.Length)
                {
                    break;
                }

                mOff = ((mOff - 3u) * 256u) + src[ilen++];
                if (mOff == 0xFFFFFFFFu)
                {
                    break;
                }

                lastMOff = ++mOff;
            }

            uint mLen = (uint)GetBit(src, ref ilen, ref bb);
            mLen = (mLen * 2u) + (uint)GetBit(src, ref ilen, ref bb);
            if (mLen == 0)
            {
                mLen++;
                do
                {
                    mLen = (mLen * 2u) + (uint)GetBit(src, ref ilen, ref bb);
                }
                while (GetBit(src, ref ilen, ref bb) == 0);
                mLen += 2u;
            }

            if (mOff > 0xD00u)
            {
                mLen++;
            }

            int mPos = olen - (int)mOff;
            if (mPos < 0 || olen >= dst.Length)
            {
                break;
            }

            dst[olen++] = dst[mPos++];
            do
            {
                if (olen >= dst.Length || mPos >= dst.Length)
                {
                    break;
                }

                dst[olen++] = dst[mPos++];
            }
            while (--mLen > 0);
        }

        return olen;
    }

    private static int GetBit(ReadOnlySpan<byte> src, ref int ilen, ref uint bb)
    {
        if ((bb & 0x7Fu) != 0)
        {
            bb = (bb * 2u) & 0xFFFFFFFFu;
        }
        else
        {
            byte next = ilen < src.Length ? src[ilen++] : (byte)0;
            bb = ((uint)next * 2u) + 1u;
        }

        return (int)((bb >> 8) & 1u);
    }
}
