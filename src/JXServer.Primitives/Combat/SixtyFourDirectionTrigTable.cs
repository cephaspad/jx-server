using System;

namespace JXServer.Primitives.Combat;

/// <summary>
/// Canonical 64-direction fixed-point trigonometric lookup tables (<c>g_nSinBuffer</c> and <c>g_nCosBuffer</c> scaled by <c>1024</c>)
/// and direction index utilities from <c>jx-win-src/Sources/Core/Src/KMath.cpp</c> and <c>KMath.h</c>.
/// </summary>
public static class SixtyFourDirectionTrigTable
{
    public const int DirectionCount = 64;
    public const int FixedPointScaleShift = 10;
    public const int FixedPointScale = 1024;

    private static readonly int[] SinBuffer =
    [
         1024,  1019,  1004,   979,   946,   903,   851,   791,
          724,   649,   568,   482,   391,   297,   199,   100,
            0,  -100,  -199,  -297,  -391,  -482,  -568,  -649,
         -724,  -791,  -851,  -903,  -946,  -979, -1004, -1019,
        -1024, -1019, -1004,  -979,  -946,  -903,  -851,  -791,
         -724,  -649,  -568,  -482,  -391,  -297,  -199,  -100,
            0,   100,   199,   297,   391,   482,   568,   649,
          724,   791,   851,   903,   946,   979,  1004,  1019,
    ];

    private static readonly int[] CosBuffer =
    [
            0,  -100,  -199,  -297,  -391,  -482,  -568,  -649,
         -724,  -791,  -851,  -903,  -946,  -979, -1004, -1019,
        -1024, -1019, -1004,  -979,  -946,  -903,  -851,  -791,
         -724,  -649,  -568,  -482,  -391,  -297,  -199,  -100,
            0,   100,   199,   297,   391,   482,   568,   649,
          724,   791,   851,   903,   946,   979,  1004,  1019,
         1024,  1019,  1004,   979,   946,   903,   851,   791,
          724,   649,   568,   482,   391,   297,   199,   100,
    ];

    /// <summary>
    /// Gets the 64-element sine table (<c>g_nSin</c> scaled by <c>1024</c>).
    /// </summary>
    public static ReadOnlySpan<int> SinTable => SinBuffer;

    /// <summary>
    /// Gets the 64-element cosine table (<c>g_nCos</c> scaled by <c>1024</c>).
    /// </summary>
    public static ReadOnlySpan<int> CosTable => CosBuffer;

    /// <summary>
    /// Returns the fixed-point sine (<c>g_DirSin</c>) for <paramref name="dir"/> in <c>0..maxDir-1</c>.
    /// </summary>
    public static int DirSin(int dir, int maxDir = DirectionCount)
    {
        if (dir < 0 || dir >= maxDir || maxDir <= 0)
        {
            return -1;
        }

        int index = (dir << 6) / maxDir;
        return SinBuffer[index & 63];
    }

    /// <summary>
    /// Returns the fixed-point cosine (<c>g_DirCos</c>) for <paramref name="dir"/> in <c>0..maxDir-1</c>.
    /// </summary>
    public static int DirCos(int dir, int maxDir = DirectionCount)
    {
        if (dir < 0 || dir >= maxDir || maxDir <= 0)
        {
            return -1;
        }

        int index = (dir << 6) / maxDir;
        return CosBuffer[index & 63];
    }

    /// <summary>
    /// Computes the Euclidean pixel distance (<c>g_GetDistance</c>) between two Mps coordinates.
    /// </summary>
    public static int GetDistance(int srcMpsX, int srcMpsY, int destMpsX, int destMpsY)
    {
        long dx = (long)srcMpsX - destMpsX;
        long dy = (long)srcMpsY - destMpsY;
        return (int)Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// Computes the 64-direction index (<c>0..63</c>) from <c>(srcMpsX, srcMpsY)</c> toward <c>(destMpsX, destMpsY)</c>
    /// matching <c>g_GetDirIndex</c> in <c>KMath.h</c>. Returns <c>-1</c> if the coordinates are identical.
    /// </summary>
    public static int GetDirectionIndex(int srcMpsX, int srcMpsY, int destMpsX, int destMpsY)
    {
        if (srcMpsX == destMpsX && srcMpsY == destMpsY)
        {
            return -1;
        }

        int distance = GetDistance(srcMpsX, srcMpsY, destMpsX, destMpsY);
        if (distance == 0)
        {
            return -1;
        }

        int yLength = destMpsY - srcMpsY;
        int nSin = (yLength << FixedPointScaleShift) / distance;
        int ret = -1;

        for (int i = 0; i <= 32; i++)
        {
            if (nSin > SinBuffer[i])
            {
                break;
            }

            ret = i;
        }

        if (ret < 0)
        {
            ret = 0;
        }

        if ((destMpsX - srcMpsX) > 0)
        {
            ret = (64 - ret) & 63;
        }

        return ret;
    }

    /// <summary>
    /// Converts a direction in <c>0..maxDir-1</c> to a 64-direction index (<c>g_Dir2DirIndex</c>).
    /// </summary>
    public static int DirToDirIndex(int dir, int maxDir)
    {
        if (maxDir <= 0)
        {
            return -1;
        }

        return (dir << 6) / maxDir;
    }

    /// <summary>
    /// Converts a 64-direction index (<c>0..63</c>) to <c>0..maxDir-1</c> (<c>g_DirIndex2Dir</c>).
    /// </summary>
    public static int DirIndexToDir(int dirIndex, int maxDir)
    {
        if (maxDir <= 0)
        {
            return -1;
        }

        return (maxDir * dirIndex) >> 6;
    }
}
