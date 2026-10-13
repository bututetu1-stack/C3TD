using UnityEngine;

/// <summary>スプライトを、カメラに映る範囲いっぱいに広げる（背景の絵）</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CoverCamera : MonoBehaviour
{
    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || sr.sprite == null) return;
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        Vector2 size = sr.sprite.bounds.size;
        float scale = Mathf.Max(w / size.x, h / size.y) * 1.04f;
        transform.localScale = new Vector3(scale, scale, 1f);
        Vector3 c = cam.transform.position;
        transform.position = new Vector3(c.x, c.y, transform.position.z);
    }
}
