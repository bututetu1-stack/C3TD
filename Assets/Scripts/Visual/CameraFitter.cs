using UnityEngine;

/// <summary>
/// マップ全体が、上の HUD と下の編成バーに重ならずに収まるようにカメラを合わせる。
/// 画面サイズが変わったら合わせ直す。揺らす演出もここで行う。
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFitter : MonoBehaviour
{
    // UI が占める高さ（UI の参照解像度 1920x1080 での px）
    public const float TopReserve = 96f;
    public const float BottomReserve = 180f;

    // マップのまわりに空ける余白（マス）
    private const float Margin = 0.6f;

    public static CameraFitter Instance { get; private set; }

    private Camera cam;
    private Rect target;
    private bool hasTarget;
    private int lastWidth;
    private int lastHeight;
    private float lastZoom = 1f;
    private Vector3 basePosition;
    private float shakeTime;
    private float shakeDuration;
    private float shakeStrength;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

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

    /// <summary>画面を揺らす（strength はマス単位）</summary>
    public static void Shake(float strength, float duration)
    {
        if (Instance == null) return;
        Instance.shakeStrength = Mathf.Max(Instance.shakeTime > 0f ? Instance.shakeStrength : 0f, strength);
        Instance.shakeDuration = duration;
        Instance.shakeTime = duration;
    }

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Palette.Background;
        basePosition = transform.position;
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
        if (cam.pixelWidth != lastWidth || cam.pixelHeight != lastHeight || UIFactory.Zoom != lastZoom) Apply();

        if (shakeTime > 0f)
        {
            shakeTime -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(shakeTime / shakeDuration);
            Vector2 offset = Random.insideUnitCircle * shakeStrength * k * k;
            transform.position = basePosition + new Vector3(offset.x, offset.y, 0f);
        }
        else
        {
            transform.position = basePosition;
        }
    }

    private void Apply()
    {
        lastWidth = cam.pixelWidth;
        lastHeight = cam.pixelHeight;
        lastZoom = UIFactory.Zoom;
        if (!hasTarget || lastWidth <= 0 || lastHeight <= 0) return;

        // UI は高さに合わせて拡大縮小する（UIFactory の PanelSettings と同じ。スマホでは UI が Zoom 倍大きい）
        float uiScale = lastHeight / UIFactory.PanelHeight;
        // スマホでは UI が大きいぶんマップが小さくなるので、マップの外側（壁の列）が上下の UI の下に少し入ってよいことにする
        bool mobile = UIFactory.Zoom > 1f;
        float top = TopReserve * uiScale * (mobile ? 0.6f : 1f);
        float bottom = BottomReserve * uiScale * (mobile ? 0.8f : 1f);
        float available = Mathf.Max(1f, lastHeight - top - bottom);

        float margin = mobile ? 0.1f : Margin;
        float w = target.width + margin * 2f;
        float h = target.height + margin * 2f;
        float sizeForHeight = h * lastHeight / (2f * available);
        float sizeForWidth = w * lastHeight / (2f * lastWidth);
        cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);

        // 空いている帯の真ん中にマップの中心が来るようにずらす
        float worldPerPixel = 2f * cam.orthographicSize / lastHeight;
        float bandCenter = bottom + available * 0.5f;
        float offset = (bandCenter - lastHeight * 0.5f) * worldPerPixel;
        Vector2 center = target.center;
        basePosition = new Vector3(center.x, center.y - offset, -10f);
        transform.position = basePosition;
    }
}
