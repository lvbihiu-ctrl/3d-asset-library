using AssetLibrary.Desktop;

namespace AssetLibrary.Desktop.Tests;

public sealed class PngDimensionsTests
{
    [Fact]
    public void Read_UsesTheActualIhdrDimensions()
    {
        var bytes = new byte[24]
        {
            137, 80, 78, 71, 13, 10, 26, 10,
            0, 0, 0, 13, 73, 72, 68, 82,
            0, 0, 7, 128,
            0, 0, 4, 56,
        };

        var dimensions = PngDimensions.Read(bytes);

        Assert.Equal(1920, dimensions.Width);
        Assert.Equal(1080, dimensions.Height);
    }

    [Fact]
    public void Read_RejectsNonPngData()
    {
        Assert.Throws<InvalidDataException>(() => PngDimensions.Read(new byte[24]));
    }
}
