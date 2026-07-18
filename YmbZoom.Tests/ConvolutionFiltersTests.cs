using YmbZoom.Core;

namespace YmbZoom.Tests;

public class ConvolutionFiltersTests
{
    private static byte[] MakeFlatImage(int width, int height, byte b, byte g, byte r, byte a)
    {
        var buffer = new byte[width * height * 4];
        for (int i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = b;
            buffer[i + 1] = g;
            buffer[i + 2] = r;
            buffer[i + 3] = a;
        }
        return buffer;
    }

    [Fact]
    public void ApplySharpen_ZeroAmount_ReturnsSameBuffer()
    {
        var source = MakeFlatImage(4, 4, 10, 20, 30, 255);
        var result = ConvolutionFilters.ApplySharpen(source, 4, 4, 4 * 4, 0f);

        Assert.Same(source, result);
    }

    [Fact]
    public void ApplySharpen_FlatImage_StaysUnchanged()
    {
        var source = MakeFlatImage(5, 5, 100, 150, 200, 255);
        var result = ConvolutionFilters.ApplySharpen(source, 5, 5, 5 * 4, 0.8f);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(source[i], result[i]);
        }
    }

    [Fact]
    public void ApplySharpen_PreservesAlphaChannel()
    {
        var source = MakeFlatImage(3, 3, 10, 10, 10, 128);
        // 中心画素だけ変化させてエッジを作る
        source[(1 * 3 + 1) * 4] = 250;

        var result = ConvolutionFilters.ApplySharpen(source, 3, 3, 3 * 4, 0.5f);

        for (int i = 3; i < result.Length; i += 4)
        {
            Assert.Equal(128, result[i]);
        }
    }

    [Fact]
    public void ApplySharpen_EnhancesEdgeContrast()
    {
        var source = MakeFlatImage(3, 3, 50, 50, 50, 255);
        int centerIndex = (1 * 3 + 1) * 4;
        source[centerIndex] = 200; // 中心だけ明るい画素(エッジ)

        var result = ConvolutionFilters.ApplySharpen(source, 3, 3, 3 * 4, 0.5f);

        Assert.True(result[centerIndex] > source[centerIndex]);
    }
}
