using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

public class Drone : MonoBehaviour
{
    public static Drone Instance;
    public bool CanMove;

    public float CurrentSpeed   { get; private set; }
    public float CurrentYawRate { get; private set; }

    [Header("Head Tracking")]
    [SerializeField] private Transform trackingSpace;
    [SerializeField] private bool disableHeadMovement = false;

    [Header("Flight Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 60f;
    [SerializeField] private float altitudeSpeed = 3f;
    [SerializeField] private float deadZone = 0.2f;

    private Rigidbody rb;
    private XRHandSubsystem handSubsystem;
    private Vector3 rightPalmNormal;
    private Vector3 leftPalmNormal;
    private bool rightHandValid;
    private bool leftHandValid;
    private bool rightIsFist;
    private bool leftIsFist;

    private static readonly List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();
    }

    private void OnDisable()
    {
        if (handSubsystem != null)
        {
            handSubsystem.updatedHands -= OnUpdatedHands;
            handSubsystem = null;
        }
    }

    private void TryAcquireSubsystem()
    {
        SubsystemManager.GetSubsystems(s_Subsystems);
        if (s_Subsystems.Count > 0)
        {
            handSubsystem = s_Subsystems[0];
            handSubsystem.updatedHands += OnUpdatedHands;
        }
    }

    private void OnUpdatedHands(XRHandSubsystem subsystem,
        XRHandSubsystem.UpdateSuccessFlags flags,
        XRHandSubsystem.UpdateType updateType)
    {
        rightHandValid = TryGetPalmNormal(subsystem.rightHand, isRightHand: true, out rightPalmNormal);
        leftHandValid  = TryGetPalmNormal(subsystem.leftHand,  isRightHand: false, out leftPalmNormal);
        rightIsFist    = IsFist(subsystem.rightHand);
        leftIsFist     = IsFist(subsystem.leftHand);
    }

    private void Update()
    {
        // keep trying to acquire the subsystem until found (starts up asynchronously)
        if (handSubsystem == null)
            TryAcquireSubsystem();
    }

    // LateUpdate runs after OVR head tracking, eliminating one-frame position lag
    private void LateUpdate()
    {
        if (!CanMove) return;

        // fall back to keyboard when no headset subsystem is available
        if (handSubsystem == null || !handSubsystem.running)
        {
            KeyboardFallback();
            return;
        }

        Vector3 movement = Vector3.zero;
        float yaw = 0f;

        // converts world-space palm normal into drone-local space so movement is
        // always relative to the drone's heading, never the camera's heading
        Quaternion invDroneYaw = Quaternion.Inverse(Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        // Right hand: open palm = fly via tilt, fist = stop lateral movement
        if (rightHandValid && !rightIsFist)
        {
            Vector3 local = invDroneYaw * rightPalmNormal;
            movement += transform.forward * (ApplyDeadZone(-local.z) * moveSpeed);
            movement += transform.right   * (ApplyDeadZone( local.x) * moveSpeed);
        }

        // Left hand: open palm = altitude/yaw via tilt, fist = hold altitude and heading
        if (leftHandValid && !leftIsFist)
        {
            movement.y = ApplyDeadZone(leftPalmNormal.y) * altitudeSpeed;
            Vector3 local = invDroneYaw * leftPalmNormal;
            yaw = ApplyDeadZone(local.x) * rotateSpeed;
        }

        CurrentSpeed   = movement.magnitude;
        CurrentYawRate = Mathf.Abs(yaw);

        transform.position += movement * Time.deltaTime;
        transform.Rotate(Vector3.up, yaw * Time.deltaTime, Space.World);

        if (disableHeadMovement && trackingSpace != null)
        {
            trackingSpace.localPosition = Vector3.zero;
            trackingSpace.localRotation = Quaternion.identity;
        }
    }

    // Derives the palm-facing direction from three joint positions.
    // Returns false if any joint pose is unavailable or the hand is untracked.
    private static bool TryGetPalmNormal(XRHand hand, bool isRightHand, out Vector3 palmNormal)
    {
        palmNormal = Vector3.zero;
        if (!hand.isTracked) return false;

        if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wristPose) ||
            !hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out Pose indexPose) ||
            !hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middlePose))
            return false;

        Vector3 fingerDir  = (middlePose.position - wristPose.position).normalized;
        Vector3 lateralDir = (indexPose.position  - wristPose.position).normalized;

        // cross product direction mirrors between hands because index sits on
        // opposite sides of the middle finger for left vs right hand
        palmNormal = isRightHand
            ? Vector3.Cross(fingerDir, lateralDir).normalized
            : Vector3.Cross(lateralDir, fingerDir).normalized;

        return true;
    }

    // Returns true when all four fingers are curled in (fingertip closer to wrist than proximal joint).
    private static bool IsFist(XRHand hand)
    {
        if (!hand.isTracked) return false;
        return IsFingerCurled(hand, XRHandJointID.IndexTip,  XRHandJointID.IndexProximal)  &&
               IsFingerCurled(hand, XRHandJointID.MiddleTip, XRHandJointID.MiddleProximal) &&
               IsFingerCurled(hand, XRHandJointID.RingTip,   XRHandJointID.RingProximal)   &&
               IsFingerCurled(hand, XRHandJointID.LittleTip, XRHandJointID.LittleProximal);
    }

    private static bool IsFingerCurled(XRHand hand, XRHandJointID tipId, XRHandJointID proximalId)
    {
        if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wrist)  ||
            !hand.GetJoint(tipId).TryGetPose(out Pose tip)                   ||
            !hand.GetJoint(proximalId).TryGetPose(out Pose proximal))
            return false;

        float wristToTip      = (tip.position      - wrist.position).sqrMagnitude;
        float wristToProximal = (proximal.position  - wrist.position).sqrMagnitude;
        return wristToTip < wristToProximal;
    }

    private float ApplyDeadZone(float value)
    {
        float abs = Mathf.Abs(value);
        if (abs < deadZone) return 0f;
        return Mathf.Sign(value) * Mathf.InverseLerp(deadZone, 1f, abs);
    }

    private void KeyboardFallback()
    {
        int elevate = 0;
        elevate += Keyboard.current.shiftKey.IsPressed() ? 1 : 0;
        elevate += Keyboard.current.ctrlKey.IsPressed() ? -1 : 0;
        Vector2 moveInput = InputSystem.actions.FindAction("Move").ReadValue<Vector2>();
        transform.Translate(new Vector3(moveInput.x, elevate, moveInput.y) * 5 * Time.deltaTime, Space.Self);
        int rotate = 0;
        rotate += Keyboard.current.eKey.IsPressed() ? 1 : 0;
        rotate += Keyboard.current.qKey.IsPressed() ? -1 : 0;
        transform.Rotate(Vector3.up, rotate * Time.deltaTime * 10);
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
