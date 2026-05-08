using UnityEngine;

public class HUD : MonoBehaviour
{
    public RectTransform TargetIndicator;

    private void Update()
    {
        bool raceOver = RaceTrack.Instance.NextCheckpointIndex >= RaceTrack.Instance.Checkpoints.Count;

        // hide target indicator entirely when spatial audio mode is the active wayfinding method
        if (RaceTrack.Instance.SpatialAudioMode || raceOver)
        {
            TargetIndicator.gameObject.SetActive(false);
            return;
        }

        TargetIndicator.gameObject.SetActive(true);
        TargetIndicator.transform.position = Camera.main.WorldToScreenPoint(RaceTrack.Instance.NextCheckpoint.transform.position);

        float clampedX = Mathf.Clamp(TargetIndicator.transform.position.x, 0, Screen.width);
        float clampedY = Mathf.Clamp(TargetIndicator.transform.position.y, 0, Screen.height);
        TargetIndicator.transform.position = new Vector2(clampedX, clampedY);
    }
}
