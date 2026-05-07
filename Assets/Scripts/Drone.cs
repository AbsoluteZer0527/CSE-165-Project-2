using UnityEngine;

public class Drone : MonoBehaviour
{
    public static Drone Instance;
    public bool CanMove;

    private void Awake()
    {
        Instance = this;
    }

    public void Respawn()
    {
        CanMove = false;

        transform.position = RaceTrack.Instance.CurrentCheckpoint.transform.position;
        transform.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        transform.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

        transform.LookAt(RaceTrack.Instance.NextCheckpoint.transform);
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

        Countdown.Instance.StartCountdown();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Respawn();
    }
}
