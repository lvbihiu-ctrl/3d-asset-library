using System.Buffers.Binary;

namespace AssetLibrary.Desktop;

public readonly record struct PngSize(int Width, int Height);

public static class PngDimensions
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static PngSize Read(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[24];
        stream.ReadExactly(header);
        return Read(header);
    }

    public static PngSize Read(ReadOnlySpan<byte> header)
    {
        if (header.Length < 24
            || !header[..8].SequenceEqual(Signature)
            || !header.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException("Screenshot is not a valid PNG with an IHDR header.");
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Screenshot PNG dimensions must be positive.");
        }

        return new PngSize(width, height);
    }
}
