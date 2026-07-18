using System.Windows;

namespace YmbZoom.Core;

/// <summary>
/// 矩形指定モード(ホットキーオーバーレイ/常駐ドラッグ枠/カーソル追従)の出所を問わず、
/// 確定した矩形(仮想デスクトップ座標・物理ピクセル)を受け取る単一の窓口。
/// </summary>
public interface IZoomSource
{
    void SetSourceRect(Int32Rect rect);
}
