using YmbZoom.Core;

namespace YmbZoom.Tests;

public class ColorMatrixPresetsTests
{
    private static (float r, float g, float b) Apply(float[] matrix25, float r, float g, float b)
    {
        // v=[r,g,b,a,1] * M (行優先、GDI+互換規約)
        float a = 1f;
        float outR = r * matrix25[0] + g * matrix25[5] + b * matrix25[10] + a * matrix25[15] + matrix25[20];
        float outG = r * matrix25[1] + g * matrix25[6] + b * matrix25[11] + a * matrix25[16] + matrix25[21];
        float outB = r * matrix25[2] + g * matrix25[7] + b * matrix25[12] + a * matrix25[17] + matrix25[22];
        return (outR, outG, outB);
    }

    [Fact]
    public void Identity_LeavesColorUnchanged()
    {
        var (r, g, b) = Apply(ColorMatrixPresets.Identity(), 0.3f, 0.6f, 0.9f);
        Assert.Equal(0.3f, r, 3);
        Assert.Equal(0.6f, g, 3);
        Assert.Equal(0.9f, b, 3);
    }

    [Fact]
    public void Invert_FlipsColor()
    {
        var (r, g, b) = Apply(ColorMatrixPresets.Invert(), 0.2f, 0.5f, 0.8f);
        Assert.Equal(0.8f, r, 3);
        Assert.Equal(0.5f, g, 3);
        Assert.Equal(0.2f, b, 3);
    }

    [Fact]
    public void Grayscale_ProducesEqualChannels()
    {
        var (r, g, b) = Apply(ColorMatrixPresets.Grayscale(), 1f, 0f, 0f);
        Assert.Equal(r, g, 3);
        Assert.Equal(g, b, 3);
    }

    [Fact]
    public void ContrastBrightness_Identity_WhenNeutral()
    {
        var (r, g, b) = Apply(ColorMatrixPresets.ContrastBrightness(1f, 0f), 0.4f, 0.4f, 0.4f);
        Assert.Equal(0.4f, r, 3);
        Assert.Equal(0.4f, g, 3);
        Assert.Equal(0.4f, b, 3);
    }

    [Fact]
    public void Multiply_WithIdentity_ReturnsOriginal()
    {
        var invert = ColorMatrixPresets.Invert();
        var composed = ColorMatrixPresets.Multiply(ColorMatrixPresets.Identity(), invert);

        for (int i = 0; i < 25; i++)
        {
            Assert.Equal(invert[i], composed[i], 4);
        }
    }

    [Fact]
    public void Compose_EmptyList_ReturnsIdentity()
    {
        var result = ColorMatrixPresets.Compose([]);
        var identity = ColorMatrixPresets.Identity();

        for (int i = 0; i < 25; i++)
        {
            Assert.Equal(identity[i], result[i], 4);
        }
    }

    [Fact]
    public void ColorblindPresets_KeepAlphaAndConstantRowUnchanged()
    {
        foreach (var matrix in new[]
        {
            ColorMatrixPresets.Protanopia(),
            ColorMatrixPresets.Deuteranopia(),
            ColorMatrixPresets.Tritanopia(),
        })
        {
            Assert.Equal(1f, matrix[18], 4); // Alphaは変化させない
            Assert.Equal(1f, matrix[24], 4); // 定数行の1は維持
        }
    }
}
