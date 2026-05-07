using UnityEngine;
using UnityEngine.UI;

// Darkens the screen edges when the drone is moving fast or rotating,
// reducing peripheral-vision vection which is the primary cause of motion sickness.
// Attach to a full-screen UI Image with a radial gradient texture (dark edges, clear center).
public class ComfortVignette : MonoBehaviour
{
    [SerializeField] private Image vignetteImage;
    [SerializeField] private float maxMoveSpeed = 5f;
    [SerializeField] private float maxYawSpeed = 60f;
    [SerializeField] private float maxAlpha = 0.6f;
    [SerializeField] private float fadeSpeed = 4f;

    private float targetAlpha;

    private void Update()
    {
        if (Drone.Instance == null) return;

        // measure how fast the drone is moving and rotating this frame
        float speed = Drone.Instance.CurrentSpeed;
        float yawRate = Drone.Instance.CurrentYawRate;

        float moveFactor = Mathf.Clamp01(speed / maxMoveSpeed);
        float yawFactor  = Mathf.Clamp01(yawRate / maxYawSpeed);
        targetAlpha = Mathf.Max(moveFactor, yawFactor) * maxAlpha;

        // smooth the fade in/out so it doesn't snap
        Color c = vignetteImage.color;
        c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * fadeSpeed);
        vignetteImage.color = c;
    }
}
