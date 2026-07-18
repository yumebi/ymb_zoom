namespace YmbZoom.Core;

/// <summary>
/// ソフトウェアズーム経路(BitBltキャプチャ)向けの畳み込みフィルタ。
/// Magnification APIのカラー行列では表現できないシャープ化(輪郭強調)専用。
/// </summary>
public static class ConvolutionFilters
{
    /// <summary>
    /// 32bpp BGRA バッファに十字型アンシャープマスクを適用した新しいバッファを返す。
    /// amount: 0で変化なし、大きいほど輪郭強調が強くなる(目安 0.0〜1.5)。
    /// </summary>
    public static byte[] ApplySharpen(byte[] source, int width, int height, int stride, float amount)
    {
        if (amount <= 0f || width <= 0 || height <= 0)
        {
            return source;
        }

        var result = new byte[source.Length];
        float center = 1f + 4f * amount;

        for (int y = 0; y < height; y++)
        {
            int yUp = Math.Max(0, y - 1);
            int yDown = Math.Min(height - 1, y + 1);

            for (int x = 0; x < width; x++)
            {
                int xLeft = Math.Max(0, x - 1);
                int xRight = Math.Min(width - 1, x + 1);

                int baseIndex = y * stride + x * 4;

                for (int c = 0; c < 3; c++) // B,G,R のみ処理し、Aは元値をそのまま維持
                {
                    float sum = center * source[baseIndex + c]
                                - amount * source[y * stride + xLeft * 4 + c]
                                - amount * source[y * stride + xRight * 4 + c]
                                - amount * source[yUp * stride + x * 4 + c]
                                - amount * source[yDown * stride + x * 4 + c];

                    result[baseIndex + c] = (byte)Math.Clamp(MathF.Round(sum), 0f, 255f);
                }

                result[baseIndex + 3] = source[baseIndex + 3];
            }
        }

        return result;
    }
}
