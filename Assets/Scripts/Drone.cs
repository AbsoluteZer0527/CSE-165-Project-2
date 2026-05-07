using UnityEngine;
using UnityEngine.InputSystem;

public class Drone : MonoBehaviour
{
    public static Drone Instance;
    public bool CanMove;

    private Rigidbody rb;

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {

        if (CanMove)
        {
            // Debug keyboard controls
            int elevate = 0;
            elevate += Keyboard.current.shiftKey.IsPressed() ? 1 : 0;
            elevate += Keyboard.current.ctrlKey.IsPressed() ? -1 : 0;
            Vector2 moveInput = InputSystem.actions.FindAction("Move").ReadValue<Vector2>();
            transform.Translate(new Vector3(moveInput.x, elevate, moveInput.y) * 5 * Time.fixedDeltaTime, Space.Self);
            int rotate = 0;
            rotate += Keyboard.current.eKey.IsPressed() ? 1 : 0;
            rotate += Keyboard.current.qKey.IsPressed() ? -1 : 0;
            transform.Rotate(Vector3.up, rotate * Time.fixedDeltaTime * 10);
        }
    }

    public void Respawn()
    {
        CanMove = false;

        transform.position = RaceTrack.Instance.CurrentCheckpoint.transform.position;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.LookAt(RaceTrack.Instance.NextCheckpoint.transform);
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

        Countdown.Instance.StartCountdown();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Respawn();
    }
}
