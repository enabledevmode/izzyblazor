// A port of Project Nayuki's QR Code generator library (https://www.nayuki.io/page/qr-code-generator-library),
// trimmed to the numeric, alphanumeric and byte modes.
//
// Copyright (c) Project Nayuki. (MIT License)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// - The above copyright notice and this permission notice shall be included in all copies or
//   substantial portions of the Software.
// - The Software is provided "as is", without warranty of any kind, express or implied, including but
//   not limited to the warranties of merchantability, fitness for a particular purpose and
//   noninfringement. In no event shall the authors or copyright holders be liable for any claim,
//   damages or other liability, whether in an action of contract, tort or otherwise, arising from,
//   out of or in connection with the Software or the use or other dealings in the Software.

using System.Text;

namespace IzzyBlazor;

/// <summary>How much of a QR code can be damaged and still read. Higher levels make a larger code.</summary>
public enum QrErrorCorrection
{
    /// <summary>About 7% of the code can be lost.</summary>
    Low,

    /// <summary>About 15% (the default).</summary>
    Medium,

    /// <summary>About 25%.</summary>
    Quartile,

    /// <summary>About 30%.</summary>
    High,
}

/// <summary>A QR code's modules: <c>true</c> is dark. Row-major, <see cref="Size"/> by <see cref="Size"/>.</summary>
internal sealed class QrMatrix(int version, QrErrorCorrection level, int mask, bool[] modules)
{
    public int Version { get; } = version;

    public int Size { get; } = version * 4 + 17;

    public QrErrorCorrection Level { get; } = level;

    public int Mask { get; } = mask;

    public bool this[int x, int y] => modules[y * Size + x];
}

/// <summary>Encodes text as a QR code (model 2, versions 1 to 40).</summary>
internal static class QrEncoder
{
    private const string Alphanumeric = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

    // Indexed by level (Low, Medium, Quartile, High), then version; index 0 is unused.
    private static readonly sbyte[][] EccPerBlock =
    [
        [-1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
        [-1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [-1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    ];

    private static readonly sbyte[][] BlockCount =
    [
        [-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
        [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
        [-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
        [-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
    ];

    // The format information's two bits per level.
    private static readonly int[] FormatBits = [1, 0, 3, 2];

    /// <summary>
    /// The smallest code that holds <paramref name="text"/> at <paramref name="level"/> or better: a
    /// higher level is used when it fits in the same version. <paramref name="mask"/> forces a mask
    /// (0 to 7) instead of the one that scores best.
    /// </summary>
    /// <exception cref="ArgumentException">The text is too long for a version 40 code at that level.</exception>
    public static QrMatrix Encode(string text, QrErrorCorrection level, int mask = -1)
    {
        var (mode, count, bits) = Segment(text);
        int version;
        int used;
        for (version = 1; ; version++)
        {
            used = 4 + CountBits(mode, version) + bits.Count;
            if (count < 1 << CountBits(mode, version) && used <= DataCodewords(version, level) * 8)
            {
                break;
            }

            if (version == 40)
            {
                throw new ArgumentException($"Text of {text.Length} characters is too long for a QR code at {level} error correction.", nameof(text));
            }
        }

        foreach (var higher in new[] { QrErrorCorrection.Medium, QrErrorCorrection.Quartile, QrErrorCorrection.High })
        {
            if (higher > level && used <= DataCodewords(version, higher) * 8)
            {
                level = higher;
            }
        }

        var buffer = new Bits();
        buffer.Append(mode, 4);
        buffer.Append(count, CountBits(mode, version));
        buffer.AddRange(bits);
        var capacity = DataCodewords(version, level) * 8;
        buffer.Append(0, Math.Min(4, capacity - buffer.Count));
        buffer.Append(0, (8 - buffer.Count % 8) % 8);
        for (var pad = 0xEC; buffer.Count < capacity; pad ^= 0xEC ^ 0x11)
        {
            buffer.Append(pad, 8);
        }

        var data = new byte[buffer.Count / 8];
        for (var i = 0; i < buffer.Count; i++)
        {
            if (buffer[i])
            {
                data[i >> 3] |= (byte)(1 << (7 - (i & 7)));
            }
        }

        return new Builder(version, level).Build(data, mask);
    }

    // The one segment the text is: numeric, alphanumeric, or its UTF-8 bytes.
    private static (int Mode, int Count, Bits Bits) Segment(string text)
    {
        var bits = new Bits();
        if (text.All(char.IsAsciiDigit))
        {
            for (var i = 0; i < text.Length; i += 3)
            {
                var n = Math.Min(3, text.Length - i);
                bits.Append(int.Parse(text.AsSpan(i, n), provider: System.Globalization.CultureInfo.InvariantCulture), n * 3 + 1);
            }

            return (0x1, text.Length, bits);
        }

        if (text.All(c => Alphanumeric.Contains(c)))
        {
            int i;
            for (i = 0; i + 2 <= text.Length; i += 2)
            {
                bits.Append(Alphanumeric.IndexOf(text[i]) * 45 + Alphanumeric.IndexOf(text[i + 1]), 11);
            }

            if (i < text.Length)
            {
                bits.Append(Alphanumeric.IndexOf(text[i]), 6);
            }

            return (0x2, text.Length, bits);
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        foreach (var b in bytes)
        {
            bits.Append(b, 8);
        }

        return (0x4, bytes.Length, bits);
    }

    private static int CountBits(int mode, int version)
    {
        var band = version < 10 ? 0 : version < 27 ? 1 : 2;
        return mode switch
        {
            0x1 => new[] { 10, 12, 14 }[band],
            0x2 => new[] { 9, 11, 13 }[band],
            _ => new[] { 8, 16, 16 }[band],
        };
    }

    private static int RawModules(int version)
    {
        var result = (16 * version + 128) * version + 64;
        if (version >= 2)
        {
            var align = version / 7 + 2;
            result -= (25 * align - 10) * align - 55;
            if (version >= 7)
            {
                result -= 36;
            }
        }

        return result;
    }

    private static int DataCodewords(int version, QrErrorCorrection level) =>
        RawModules(version) / 8 - EccPerBlock[(int)level][version] * BlockCount[(int)level][version];

    private sealed class Bits : List<bool>
    {
        public void Append(int value, int length)
        {
            for (var i = length - 1; i >= 0; i--)
            {
                Add(((value >> i) & 1) != 0);
            }
        }
    }

    private sealed class Builder(int version, QrErrorCorrection level)
    {
        private readonly int _size = version * 4 + 17;
        private readonly bool[] _modules = new bool[(version * 4 + 17) * (version * 4 + 17)];
        private readonly bool[] _function = new bool[(version * 4 + 17) * (version * 4 + 17)];

        public QrMatrix Build(byte[] data, int mask)
        {
            DrawFunctionPatterns();
            DrawCodewords(AddEccAndInterleave(data));
            if (mask < 0)
            {
                var best = int.MaxValue;
                for (var i = 0; i < 8; i++)
                {
                    ApplyMask(i);
                    DrawFormatBits(i);
                    var penalty = Penalty();
                    if (penalty < best)
                    {
                        mask = i;
                        best = penalty;
                    }

                    ApplyMask(i);
                }
            }

            ApplyMask(mask);
            DrawFormatBits(mask);
            return new QrMatrix(version, level, mask, _modules);
        }

        private bool Get(int x, int y) => _modules[y * _size + x];

        private void SetFunction(int x, int y, bool dark)
        {
            _modules[y * _size + x] = dark;
            _function[y * _size + x] = true;
        }

        private void DrawFunctionPatterns()
        {
            for (var i = 0; i < _size; i++)
            {
                SetFunction(6, i, i % 2 == 0);
                SetFunction(i, 6, i % 2 == 0);
            }

            DrawFinder(3, 3);
            DrawFinder(_size - 4, 3);
            DrawFinder(3, _size - 4);

            var positions = AlignmentPositions();
            var n = positions.Length;
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    // Not over the three finder patterns.
                    if (!(i == 0 && j == 0 || i == 0 && j == n - 1 || i == n - 1 && j == 0))
                    {
                        DrawAlignment(positions[i], positions[j]);
                    }
                }
            }

            DrawFormatBits(0);
            DrawVersion();
        }

        private void DrawFormatBits(int mask)
        {
            var data = FormatBits[(int)level] << 3 | mask;
            var rem = data;
            for (var i = 0; i < 10; i++)
            {
                rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            }

            var bits = (data << 10 | rem) ^ 0x5412;
            for (var i = 0; i <= 5; i++)
            {
                SetFunction(8, i, Bit(bits, i));
            }

            SetFunction(8, 7, Bit(bits, 6));
            SetFunction(8, 8, Bit(bits, 7));
            SetFunction(7, 8, Bit(bits, 8));
            for (var i = 9; i < 15; i++)
            {
                SetFunction(14 - i, 8, Bit(bits, i));
            }

            for (var i = 0; i < 8; i++)
            {
                SetFunction(_size - 1 - i, 8, Bit(bits, i));
            }

            for (var i = 8; i < 15; i++)
            {
                SetFunction(8, _size - 15 + i, Bit(bits, i));
            }

            SetFunction(8, _size - 8, true);
        }

        private void DrawVersion()
        {
            if (version < 7)
            {
                return;
            }

            var rem = version;
            for (var i = 0; i < 12; i++)
            {
                rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            }

            var bits = version << 12 | rem;
            for (var i = 0; i < 18; i++)
            {
                var bit = Bit(bits, i);
                var a = _size - 11 + i % 3;
                var b = i / 3;
                SetFunction(a, b, bit);
                SetFunction(b, a, bit);
            }
        }

        private void DrawFinder(int x, int y)
        {
            for (var dy = -4; dy <= 4; dy++)
            {
                for (var dx = -4; dx <= 4; dx++)
                {
                    var dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && xx < _size && yy >= 0 && yy < _size)
                    {
                        SetFunction(xx, yy, dist != 2 && dist != 4);
                    }
                }
            }
        }

        private void DrawAlignment(int x, int y)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                for (var dx = -2; dx <= 2; dx++)
                {
                    SetFunction(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                }
            }
        }

        private int[] AlignmentPositions()
        {
            if (version == 1)
            {
                return [];
            }

            var n = version / 7 + 2;
            var step = (version * 8 + n * 3 + 5) / (n * 4 - 4) * 2;
            var result = new int[n];
            result[0] = 6;
            for (int i = n - 1, pos = _size - 7; i >= 1; i--, pos -= step)
            {
                result[i] = pos;
            }

            return result;
        }

        private byte[] AddEccAndInterleave(byte[] data)
        {
            int blocks = BlockCount[(int)level][version], eccLength = EccPerBlock[(int)level][version];
            var raw = RawModules(version) / 8;
            var shortBlocks = blocks - raw % blocks;
            var shortLength = raw / blocks;
            var divisor = Divisor(eccLength);
            var all = new byte[blocks][];
            for (int i = 0, k = 0; i < blocks; i++)
            {
                var length = shortLength - eccLength + (i < shortBlocks ? 0 : 1);
                var block = new byte[shortLength + 1];
                Array.Copy(data, k, block, 0, length);
                var ecc = Remainder(data.AsSpan(k, length), divisor);
                k += length;
                ecc.CopyTo(block, block.Length - eccLength);
                all[i] = block;
            }

            var result = new byte[raw];
            for (int i = 0, k = 0; i < all[0].Length; i++)
            {
                for (var j = 0; j < all.Length; j++)
                {
                    // A short block has no byte at the padding position.
                    if (i != shortLength - eccLength || j >= shortBlocks)
                    {
                        result[k++] = all[j][i];
                    }
                }
            }

            return result;
        }

        private void DrawCodewords(byte[] data)
        {
            var i = 0;
            for (var right = _size - 1; right >= 1; right -= 2)
            {
                if (right == 6)
                {
                    right = 5;
                }

                for (var vert = 0; vert < _size; vert++)
                {
                    for (var j = 0; j < 2; j++)
                    {
                        var x = right - j;
                        var upward = ((right + 1) & 2) == 0;
                        var y = upward ? _size - 1 - vert : vert;
                        if (!_function[y * _size + x] && i < data.Length * 8)
                        {
                            _modules[y * _size + x] = Bit(data[i >> 3], 7 - (i & 7));
                            i++;
                        }
                    }
                }
            }
        }

        private void ApplyMask(int mask)
        {
            for (var y = 0; y < _size; y++)
            {
                for (var x = 0; x < _size; x++)
                {
                    var invert = mask switch
                    {
                        0 => (x + y) % 2 == 0,
                        1 => y % 2 == 0,
                        2 => x % 3 == 0,
                        3 => (x + y) % 3 == 0,
                        4 => (x / 3 + y / 2) % 2 == 0,
                        5 => x * y % 2 + x * y % 3 == 0,
                        6 => (x * y % 2 + x * y % 3) % 2 == 0,
                        _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                    };
                    if (invert && !_function[y * _size + x])
                    {
                        _modules[y * _size + x] ^= true;
                    }
                }
            }
        }

        private int Penalty()
        {
            const int n1 = 3, n2 = 3, n3 = 40, n4 = 10;
            var result = 0;
            Span<int> history = stackalloc int[7];

            for (var pass = 0; pass < 2; pass++)
            {
                for (var a = 0; a < _size; a++)
                {
                    var color = false;
                    var run = 0;
                    history.Clear();
                    for (var b = 0; b < _size; b++)
                    {
                        var dark = pass == 0 ? Get(b, a) : Get(a, b);
                        if (dark == color)
                        {
                            run++;
                            if (run == 5)
                            {
                                result += n1;
                            }
                            else if (run > 5)
                            {
                                result++;
                            }
                        }
                        else
                        {
                            AddHistory(run, history);
                            if (!color)
                            {
                                result += CountFinderLike(history) * n3;
                            }

                            color = dark;
                            run = 1;
                        }
                    }

                    // The end of the line, with the light border after it.
                    if (color)
                    {
                        AddHistory(run, history);
                        run = 0;
                    }

                    AddHistory(run + _size, history);
                    result += CountFinderLike(history) * n3;
                }
            }

            for (var y = 0; y < _size - 1; y++)
            {
                for (var x = 0; x < _size - 1; x++)
                {
                    var c = Get(x, y);
                    if (c == Get(x + 1, y) && c == Get(x, y + 1) && c == Get(x + 1, y + 1))
                    {
                        result += n2;
                    }
                }
            }

            var darkCount = _modules.Count(m => m);
            var total = _size * _size;
            var k = (Math.Abs(darkCount * 20 - total * 10) + total - 1) / total - 1;
            return result + k * n4;
        }

        private void AddHistory(int run, Span<int> history)
        {
            // The first run of a line has the light border before it.
            if (history[0] == 0)
            {
                run += _size;
            }

            history[..^1].CopyTo(history[1..]);
            history[0] = run;
        }

        private static int CountFinderLike(ReadOnlySpan<int> h)
        {
            var n = h[1];
            var core = n > 0 && h[2] == n && h[3] == n * 3 && h[4] == n && h[5] == n;
            return (core && h[0] >= n * 4 && h[6] >= n ? 1 : 0) + (core && h[6] >= n * 4 && h[0] >= n ? 1 : 0);
        }

        private static byte[] Divisor(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1;
            var root = 1;
            for (var i = 0; i < degree; i++)
            {
                for (var j = 0; j < result.Length; j++)
                {
                    result[j] = (byte)Multiply(result[j], root);
                    if (j + 1 < result.Length)
                    {
                        result[j] ^= result[j + 1];
                    }
                }

                root = Multiply(root, 0x02);
            }

            return result;
        }

        private static byte[] Remainder(ReadOnlySpan<byte> data, byte[] divisor)
        {
            var result = new byte[divisor.Length];
            foreach (var b in data)
            {
                var factor = b ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[^1] = 0;
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] ^= (byte)Multiply(divisor[i], factor);
                }
            }

            return result;
        }

        // In GF(2^8) modulo x^8 + x^4 + x^3 + x^2 + 1.
        private static int Multiply(int x, int y)
        {
            var z = 0;
            for (var i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }

            return z;
        }

        private static bool Bit(int value, int i) => ((value >> i) & 1) != 0;
    }
}
