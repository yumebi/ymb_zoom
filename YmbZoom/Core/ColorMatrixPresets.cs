namespace YmbZoom.Core;

/// <summary>
/// Magnification API (MagSetColorEffect) 用の5x5カラー行列プリセット。
/// 行列は行優先(row-major)25要素で、入力ベクトル [R,G,B,A,1] に対し
/// v * M で出力 [R',G',B',A',1] を得るGDI+互換の規約に従う。
/// </summary>
public static class ColorMatrixPresets
{
    private const int Size = 5;

    public static float[] Identity() => Flatten(IdentityRaw());

    /// <summary>色反転。</summary>
    public static float[] Invert()
    {
        var m = IdentityRaw();
        m[0, 0] = -1f; m[1, 1] = -1f; m[2, 2] = -1f;
        m[4, 0] = 1f; m[4, 1] = 1f; m[4, 2] = 1f;
        return Flatten(m);
    }

    /// <summary>グレースケール(輝度加重: ITU-R BT.601)。</summary>
    public static float[] Grayscale()
    {
        const float rw = 0.299f, gw = 0.587f, bw = 0.114f;
        var m = IdentityRaw();
        for (int col = 0; col < 3; col++)
        {
            m[0, col] = rw;
            m[1, col] = gw;
            m[2, col] = bw;
        }
        return Flatten(m);
    }

    /// <summary>
    /// コントラスト/明度調整。
    /// contrast: 1.0=変化なし、&gt;1で強調、&lt;1で低下。
    /// brightness: -1.0〜1.0 (0=変化なし)。
    /// </summary>
    public static float[] ContrastBrightness(float contrast, float brightness)
    {
        // 0.5を中心にスケールしてからbrightnessを加算することで、
        // コントラストを上げても中間輝度が破綻しないようにする。
        float translate = 0.5f * (1f - contrast) + brightness;

        var m = IdentityRaw();
        m[0, 0] = contrast;
        m[1, 1] = contrast;
        m[2, 2] = contrast;
        m[4, 0] = translate;
        m[4, 1] = translate;
        m[4, 2] = translate;
        return Flatten(m);
    }

    /// <summary>反転+コントラスト強調を合成したハイコントラストモード。</summary>
    public static float[] HighContrast()
    {
        return Multiply(Invert(), ContrastBrightness(1.4f, 0f));
    }

    public static float[] Protanopia() => DaltonizeCorrection(ProtanopiaSimulate, RedGreenShift);

    public static float[] Deuteranopia() => DaltonizeCorrection(DeuteranopiaSimulate, RedGreenShift);

    public static float[] Tritanopia() => DaltonizeCorrection(TritanopiaSimulate, BlueYellowShift);

    /// <summary>5x5行列同士を合成する(a適用後にbを適用した結果と等価な単一行列)。</summary>
    public static float[] Multiply(float[] a, float[] b)
    {
        if (a.Length != 25 || b.Length != 25)
        {
            throw new ArgumentException("カラー行列は25要素(5x5)である必要があります。");
        }

        var result = new float[25];
        for (int row = 0; row < Size; row++)
        {
            for (int col = 0; col < Size; col++)
            {
                float sum = 0f;
                for (int k = 0; k < Size; k++)
                {
                    sum += a[row * Size + k] * b[k * Size + col];
                }
                result[row * Size + col] = sum;
            }
        }
        return result;
    }

    /// <summary>複数の行列を先頭から順に合成する。空の場合は単位行列を返す。</summary>
    public static float[] Compose(IEnumerable<float[]> matrices)
    {
        float[]? result = null;
        foreach (var m in matrices)
        {
            result = result is null ? m : Multiply(result, m);
        }
        return result ?? Identity();
    }

    // --- 色弱対応(Daltonize近似補正) ---
    // 手順: 1) 色弱者が知覚する見え方をシミュレーションする3x3行列で近似
    //       2) 元の色との差(失われる情報)を、知覚可能な他チャンネルへ再配分する
    // 参考にした近似モデルであり臨床的な正確さは保証しない。既定はオフで、必要な人が選択する想定。

    private static readonly float[,] ProtanopiaSimulate =
    {
        { 0.567f, 0.433f, 0.000f },
        { 0.558f, 0.442f, 0.000f },
        { 0.000f, 0.242f, 0.758f },
    };

    private static readonly float[,] DeuteranopiaSimulate =
    {
        { 0.625f, 0.375f, 0.000f },
        { 0.700f, 0.300f, 0.000f },
        { 0.000f, 0.300f, 0.700f },
    };

    private static readonly float[,] TritanopiaSimulate =
    {
        { 0.950f, 0.050f, 0.000f },
        { 0.000f, 0.433f, 0.567f },
        { 0.000f, 0.475f, 0.525f },
    };

    // 赤-緑が弱いタイプ: 失われた誤差を緑・青へ再配分
    private static readonly float[,] RedGreenShift =
    {
        { 1.0f, 0.0f, 0.0f },
        { 0.7f, 1.0f, 0.0f },
        { 0.7f, 0.0f, 1.0f },
    };

    // 青-黄が弱いタイプ: 失われた誤差を赤・緑へ再配分
    private static readonly float[,] BlueYellowShift =
    {
        { 1.0f, 0.0f, 0.7f },
        { 0.0f, 1.0f, 0.7f },
        { 0.0f, 0.0f, 1.0f },
    };

    private static float[] DaltonizeCorrection(float[,] simulate3x3, float[,] shift3x3)
    {
        // correction = I + shift * (I - simulate)
        var identity3 = Identity3x3();
        var diff = Subtract3x3(identity3, simulate3x3);
        var shifted = MultiplyRaw3x3(shift3x3, diff);
        var correction = Add3x3(identity3, shifted);

        var m = IdentityRaw();
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                m[row, col] = correction[row, col];
            }
        }
        return Flatten(m);
    }

    private static float[,] Identity3x3() => new float[,]
    {
        { 1f, 0f, 0f },
        { 0f, 1f, 0f },
        { 0f, 0f, 1f },
    };

    private static float[,] Subtract3x3(float[,] a, float[,] b)
    {
        var r = new float[3, 3];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                r[i, j] = a[i, j] - b[i, j];
            }
        }
        return r;
    }

    private static float[,] Add3x3(float[,] a, float[,] b)
    {
        var r = new float[3, 3];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                r[i, j] = a[i, j] + b[i, j];
            }
        }
        return r;
    }

    private static float[,] MultiplyRaw3x3(float[,] a, float[,] b)
    {
        var r = new float[3, 3];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                float sum = 0f;
                for (int k = 0; k < 3; k++)
                {
                    sum += a[i, k] * b[k, j];
                }
                r[i, j] = sum;
            }
        }
        return r;
    }

    private static float[,] IdentityRaw()
    {
        var m = new float[Size, Size];
        for (int i = 0; i < Size; i++)
        {
            m[i, i] = 1f;
        }
        return m;
    }

    private static float[] Flatten(float[,] m)
    {
        var result = new float[Size * Size];
        for (int row = 0; row < Size; row++)
        {
            for (int col = 0; col < Size; col++)
            {
                result[row * Size + col] = m[row, col];
            }
        }
        return result;
    }

}
