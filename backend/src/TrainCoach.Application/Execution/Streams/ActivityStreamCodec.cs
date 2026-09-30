using System.IO.Compression;

namespace TrainCoach.Application.Execution.Streams;

/// <summary>
/// Compact binary encoding of <see cref="ActivityStreamData"/> for <c>ActivityStream.Payload</c>.
/// Format v1, Brotli-compressed:
/// <code>
/// varint count
/// varint arrayMask            — bit per array in <see cref="Arrays"/> order (time is always present)
/// time:   zigzag-varint deltas (seconds)
/// per present array:
///   presence bitmap           — ceil(count/8) bytes, bit set = value present
///   zigzag-varint deltas of the scaled integer values, present values only
/// </code>
/// Values are stored as scaled integers (e.g. GPS in 1e-6 degrees ≈ 0.1 m, altitude and distance
/// in decimeters), which makes consecutive deltas tiny and Brotli very effective.
/// </summary>
public static class ActivityStreamCodec
{
    public const byte CurrentFormatVersion = 1;

    private sealed record ArrayDef(
        string Name, double Scale, Func<ActivityStreamData, double?[]?> Get);

    // Order is part of the format — append only.
    private static readonly ArrayDef[] Arrays =
    [
        new("heartRate", 1, d => ToDouble(d.HeartRateBpm)),
        new("power", 1, d => ToDouble(d.PowerWatts)),
        new("cadence", 1, d => ToDouble(d.CadenceRpm)),
        new("distance", 10, d => d.DistanceMeters),
        new("altitude", 10, d => d.AltitudeMeters),
        new("speed", 1000, d => d.SpeedMetersPerSecond),
        new("latitude", 1_000_000, d => d.Latitude),
        new("longitude", 1_000_000, d => d.Longitude),
        new("temperature", 10, d => d.TemperatureC),
    ];

    public static byte[] Encode(ActivityStreamData data)
    {
        using var raw = new MemoryStream();
        var n = data.Count;
        WriteVarint(raw, (ulong)n);

        var arrays = Arrays.Select(a => a.Get(data)).ToArray();
        ulong mask = 0;
        for (var a = 0; a < arrays.Length; a++)
        {
            if (arrays[a] is not null)
            {
                mask |= 1UL << a;
            }
        }
        WriteVarint(raw, mask);

        long previous = 0;
        foreach (var t in data.TimeOffsetsSeconds)
        {
            WriteSigned(raw, t - previous);
            previous = t;
        }

        for (var a = 0; a < arrays.Length; a++)
        {
            if (arrays[a] is not { } values)
            {
                continue;
            }
            var bitmap = new byte[(n + 7) / 8];
            for (var i = 0; i < n; i++)
            {
                if (values[i].HasValue)
                {
                    bitmap[i / 8] |= (byte)(1 << (i % 8));
                }
            }
            raw.Write(bitmap);

            previous = 0;
            for (var i = 0; i < n; i++)
            {
                if (values[i] is { } v)
                {
                    var scaled = (long)Math.Round(v * Arrays[a].Scale);
                    WriteSigned(raw, scaled - previous);
                    previous = scaled;
                }
            }
        }

        using var compressed = new MemoryStream();
        using (var brotli = new BrotliStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(brotli);
        }
        return compressed.ToArray();
    }

    public static ActivityStreamData Decode(byte[] payload, byte formatVersion, int originalSampleCount)
    {
        if (formatVersion != CurrentFormatVersion)
        {
            throw new NotSupportedException($"Neznámá verze formátu streamu {formatVersion}.");
        }

        using var input = new BrotliStream(new MemoryStream(payload), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        input.CopyTo(raw);
        raw.Position = 0;

        var n = checked((int)ReadVarint(raw));
        var mask = ReadVarint(raw);

        var time = new int[n];
        long previous = 0;
        for (var i = 0; i < n; i++)
        {
            previous += ReadSigned(raw);
            time[i] = (int)previous;
        }

        var arrays = new double?[]?[Arrays.Length];
        for (var a = 0; a < Arrays.Length; a++)
        {
            if ((mask & (1UL << a)) == 0)
            {
                continue;
            }
            var bitmap = new byte[(n + 7) / 8];
            raw.ReadExactly(bitmap);
            var values = new double?[n];
            previous = 0;
            for (var i = 0; i < n; i++)
            {
                if ((bitmap[i / 8] & (1 << (i % 8))) != 0)
                {
                    previous += ReadSigned(raw);
                    values[i] = previous / Arrays[a].Scale;
                }
            }
            arrays[a] = values;
        }

        return new ActivityStreamData
        {
            TimeOffsetsSeconds = time,
            HeartRateBpm = ToInt(arrays[0]),
            PowerWatts = ToInt(arrays[1]),
            CadenceRpm = ToInt(arrays[2]),
            DistanceMeters = arrays[3],
            AltitudeMeters = arrays[4],
            SpeedMetersPerSecond = arrays[5],
            Latitude = arrays[6],
            Longitude = arrays[7],
            TemperatureC = arrays[8],
            OriginalSampleCount = Math.Max(originalSampleCount, n),
        };
    }

    private static double?[]? ToDouble(int?[]? values) => values?.Select(v => (double?)v).ToArray();

    private static int?[]? ToInt(double?[]? values) => values?.Select(v => v is { } d ? (int?)(int)Math.Round(d) : null).ToArray();

    private static void WriteSigned(Stream s, long value) => WriteVarint(s, (ulong)((value << 1) ^ (value >> 63)));

    private static long ReadSigned(Stream s)
    {
        var v = ReadVarint(s);
        return (long)(v >> 1) ^ -(long)(v & 1);
    }

    private static void WriteVarint(Stream s, ulong value)
    {
        while (value >= 0x80)
        {
            s.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        s.WriteByte((byte)value);
    }

    private static ulong ReadVarint(Stream s)
    {
        ulong result = 0;
        var shift = 0;
        while (true)
        {
            var b = s.ReadByte();
            if (b < 0)
            {
                throw new InvalidDataException("Poškozený stream aktivity.");
            }
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return result;
            }
            shift += 7;
            if (shift > 63)
            {
                throw new InvalidDataException("Poškozený stream aktivity.");
            }
        }
    }
}
