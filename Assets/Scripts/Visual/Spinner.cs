using UnityEngine;

/// <summary>ゆっくり回し続ける（出撃口の渦など）</summary>
public class Spinner : MonoBehaviour
{
    public float degreesPerSecond = 30f;

    void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
    }
}
