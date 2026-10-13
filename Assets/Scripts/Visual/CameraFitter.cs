using UnityEngine;

/// <summary>
/// マップ全体が、上の HUD と下の編成バーに重ならずに収まるようにカメラを合わせる。
/// 画面サイズが変わったら合わせ直す。
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFitter : MonoBehaviour
{
    // UI が占める高さ（UI の参照解像度 1920x1080 での px）
    public const float TopReserve = 88f;
    public const float BottomReserve = 180f;

    // マップのまわりに空ける余白（マス）
    private const float Margin = 0.4f;

    private Camera cam;
    private Rect target;
    private bool hasTarget;
    private int lastWidth;
    private int lastHeight;

    public static CameraFitter Ensure()
    {
        Camera main = Camera.main;
        if (main == null)
        {
            var obj = new GameObject("Main Camera");
            obj.tag = "MainCamera";
            main = obj.AddComponent<Camera>();
        }
        var fitter = main.GetComponent<CameraFitter>();
        if (fitter == null) fitter = main.gameObject.AddComponent<CameraFitter>();
        return fitter;
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Palette.Background;
    }

    /// <summary>このワールド範囲が収まるように合わせる</summary>
    public void Fit(Rect worldRect)
    {
        target = worldRect;
        hasTarget = true;
        Apply();
    }

    void LateUpdate()
    {
        if (cam.pixelWidth != lastWidth || cam.pixelHeight != lastHeight) Apply();
    }

    private void Apply()
    {
        lastWidth = cam.pixelWidth;
        lastHeight = cam.pixelHeight;
        if (!hasTarget || lastWidth <= 0 || lastHeight <= 0) return;

        // UI は高さに合わせて拡大縮小する（UIFactory の PanelSettings と同じ）
        float uiScale = lastHeight / UIFactory.ReferenceHeight;
        float top = TopReserve * uiScale;
        float bottom = BottomReserve * uiScale;
        float available = Mathf.Max(1f, lastHeight - top - bottom);

        float w = target.width + Margin * 2f;
        float h = target.height + Margin * 2f;
        float sizeForHeight = h * lastHeight / (2f * available);
        float sizeForWidth = w * lastHeight / (2f * lastWidth);
        cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);

        // 空いている帯の真ん中にマップの中心が来るようにずらす
        float worldPerPixel = 2f * cam.orthographicSize / lastHeight;
        float bandCenter = bottom + available * 0.5f;
        float offset = (bandCenter - lastHeight * 0.5f) * worldPerPixel;
        Vector2 center = target.center;
        transform.position = new Vector3(center.x, center.y - offset, -10f);
    }
}
