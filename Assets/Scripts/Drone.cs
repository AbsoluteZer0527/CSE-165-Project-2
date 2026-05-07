using UnityEngine;

public class Drone : MonoBehaviour
{
    public void Respawn()
    {
        transform.position = RaceTrack.Instance.CurrentCheckpoint.transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Respawn();
    }
}
