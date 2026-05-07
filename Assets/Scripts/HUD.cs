using UnityEngine;

public class HUD : MonoBehaviour
{
    public RectTransform TargetIndicator;

    private void Update()
    {
        if (RaceTrack.Instance.NextCheckpointIndex < RaceTrack.Instance.Checkpoints.Count)
        {
            TargetIndicator.transform.position = Camera.main.WorldToScreenPoint(RaceTrack.Instance.NextCheckpoint.transform.position);

            float clampedX = Mathf.Clamp(TargetIndicator.transform.position.x, 0, Screen.width);
            float clampedY = Mathf.Clamp(TargetIndicator.transform.position.y, 0, Screen.height);
            TargetIndicator.transform.position = new Vector2(clampedX, clampedY);
        }
        else
        {
            TargetIndicator.transform.position = Vector3.zero;
        }
    }
}
