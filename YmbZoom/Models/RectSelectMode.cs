namespace YmbZoom.Models;

/// <summary>ズーム対象の矩形をどうやって指定するか。</summary>
public enum RectSelectMode
{
    /// <summary>ホットキーで全画面オーバーレイを出し、ドラッグで矩形を1回選択する。</summary>
    HotkeyOverlay,

    /// <summary>常駐する矩形枠をドラッグ移動・リサイズしてリアルタイムに反映する。</summary>
    DraggableFrame,

    /// <summary>固定サイズの矩形がマウスカーソルに追従する。</summary>
    CursorFollow,
}
