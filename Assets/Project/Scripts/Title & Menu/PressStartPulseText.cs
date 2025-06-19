using UnityEngine;
using UnityEngine.UI;

public class PressStartPulseText : MonoBehaviour
{
    public float pulseSpeed = 1f;
    public float pulseSize = 0.1f;
    private Text text;
    private Vector3 originalScale;

    void Start()
    {
        text = GetComponent<Text>();
        originalScale = transform.localScale;
    }

    void Update()
    {
        float scaleFactor = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseSize;
        transform.localScale = originalScale * scaleFactor;
    }
}
