using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // プレイヤーなど追従対象
    public float smoothSpeed = 0.125f; // カメラの追従速度
    public Vector3 offset; // プレイヤーとの距離

    [Header("カメラの移動制限")]
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -5f;
    public float maxY = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        // 追従したい位置
        Vector3 desiredPosition = target.position + offset;

        // スムーズに移動
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // 移動制限をかける
        smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minX, maxX);
        smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minY, maxY);

        // Z（奥行き）は固定
        smoothedPosition.z = transform.position.z;

        transform.position = smoothedPosition;
    }
}
