using UnityEngine;

[DisallowMultipleComponent]
public class ObstacleVerticalMover : MonoBehaviour
{
    [Tooltip("Max vertical offset (local units)")]
    public float amplitude = 1f;

    [Tooltip("Oscillation speed (cycles per second)")]
    public float speed = 1f;

    [Tooltip("Phase offset in radians")]
    public float phase = 0f;

    [Tooltip("Tolerance to detect external reset (local space units).")]
    public float resetDetectionTolerance = 0.05f;

    Vector3 initialLocalPos;
    float elapsed = 0f;

    void Awake()
    {
        initialLocalPos = transform.localPosition;
    }

    void OnEnable()
    {
        initialLocalPos = transform.localPosition;
        elapsed = 0f;
    }

    void Update()
    {
        Vector3 offset = Vector3.up * (amplitude * Mathf.Sin((elapsed * Mathf.PI * 2f * speed) + phase));
        Vector3 desired = initialLocalPos + offset;

        if (Vector3.Distance(transform.localPosition, initialLocalPos) <= resetDetectionTolerance &&
            Vector3.Distance(transform.localPosition, desired) > resetDetectionTolerance)
        {
            initialLocalPos = transform.localPosition;
            elapsed = 0f;
            offset = Vector3.up * (amplitude * Mathf.Sin((elapsed * Mathf.PI * 2f * speed) + phase));
            desired = initialLocalPos + offset;
        }

        elapsed += Time.deltaTime;
        offset = Vector3.up * (amplitude * Mathf.Sin((elapsed * Mathf.PI * 2f * speed) + phase));
        desired = initialLocalPos + offset;

        transform.localPosition = desired;
    }

    public void ResetMotion()
    {
        initialLocalPos = transform.localPosition;
        elapsed = 0f;
    }
}
