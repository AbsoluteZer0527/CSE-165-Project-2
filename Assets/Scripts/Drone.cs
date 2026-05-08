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

    [Header("Audio")]
    [SerializeField] private AudioSource motorSource;
    [SerializeField] private AudioSource crashSource;
    [SerializeField] private AudioSource accelerateSource;
    [SerializeField] private AudioClip crashClip;
    [SerializeField] private AudioClip accelerateClip;
    [SerializeField] private float minPitch = 0.5f;
    [SerializeField] private float maxPitch = 2.0f;

    [Header("Flight Settings")]
    [SerializeField] private float moveSpeed     = 5f;
    [SerializeField] private float rotateSpeed   = 60f;
    [SerializeField] private float altitudeSpeed = 3f;
    [SerializeField] private float deadZone      = 0.2f;

    [Header("Acceleration")]
    [SerializeField] private float accelerationRate = 8f;
    [SerializeField] private float decelerationRate = 20f;

    private Rigidbody rb;
    private XRHandSubsystem handSubsystem;

    // cached per-frame hand state (written by OnUpdatedHands, read by LateUpdate)
    private Vector3 rightPalmNormal;   // right hand orientation for forward/strafe
    private Vector3 leftFingerDir;     // left hand middle finger direction for altitude
    private bool rightHandValid;
    private bool leftHandValid;
    private bool rightIsFist;
    private float leftYawInput;     // derived from index/pinky finger tilt (see TryGetYawInput)

    // smooth velocity
    private Vector3 currentHoriz;
    private float   currentAlt;
    private float   currentYaw;
    private float   previousSpeed;

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
        rightHandValid = TryGetHandVectors(subsystem.rightHand, isRightHand: true,
                             out rightPalmNormal, out _);
        leftHandValid  = TryGetHandVectors(subsystem.leftHand, isRightHand: false,
                             out _, out leftFingerDir);
        rightIsFist    = IsFist(subsystem.rightHand);
        TryGetYawInput(subsystem.leftHand, out leftYawInput);
    }

    private void Update()
    {
        if (handSubsystem == null)
            TryAcquireSubsystem();
    }

    // LateUpdate runs after OVR head tracking, eliminating one-frame position lag
    private void LateUpdate()
    {
        if (!CanMove) return;

        if (handSubsystem == null || !handSubsystem.running)
        {
            KeyboardFallback();
            return;
        }

        // Movement follows the camera's visual facing direction.
        // Dot the palm normal against camera axes directly — no rotations needed,
        // so respawn (which rotates the drone/tracking-space) can't flip directions.
        Camera cam        = Camera.main;
        float  camYawAng  = cam != null ? cam.transform.eulerAngles.y : transform.eulerAngles.y;
        Quaternion camYaw = Quaternion.Euler(0f, camYawAng, 0f);
        Vector3 camForward = camYaw * Vector3.forward;
        Vector3 camRight   = camYaw * Vector3.right;

        // ---------------------------------------------------------------
        // RIGHT HAND — lateral movement only (like right joystick)
        //   Hold palm flat facing down (neutral = no movement)
        //   Tilt fingers forward / backward  →  fly forward / backward
        //   Roll thumb up / pinky up          →  strafe right / left
        //   Fist                              →  brake
        // ---------------------------------------------------------------
        Vector3 targetHoriz = Vector3.zero;

        if (rightHandValid && !rightIsFist)
        {
            // Palm normal tilts away from camForward when hand tilts forward, so negate.
            float fwdInput    = -Vector3.Dot(rightPalmNormal, camForward);
            float strafeInput =  Vector3.Dot(rightPalmNormal, camRight);
            targetHoriz += camForward * (ApplyDeadZone(fwdInput)    * moveSpeed);
            targetHoriz += camRight   * (ApplyDeadZone(strafeInput) * moveSpeed);
        }
        // fist → targetHoriz stays zero → decelerates to stop

        // ---------------------------------------------------------------
        // LEFT HAND — altitude + yaw (like left joystick)
        //   Tilt fingers UP   →  ascend   (fingerDir.y > 0)
        //   Tilt fingers DOWN →  descend  (fingerDir.y < 0)
        //   Roll wrist so pinky goes down →  yaw left  (palmNormal.x < 0)
        //   Roll wrist so thumb goes down →  yaw right (palmNormal.x > 0)
        //
        //   These two motions are nearly orthogonal so they don't interfere.
        //   Left hand neutral (palm down, fingers horizontal) = dead zone on both axes.
        // ---------------------------------------------------------------
        float targetAlt = 0f;
        float targetYaw = 0f;

        // right fist = full brake: skip left-hand altitude/yaw so everything decelerates to stop
        if (leftHandValid && !rightIsFist)
        {
            targetAlt = ApplyDeadZone(leftFingerDir.y) * altitudeSpeed;
            targetYaw = ApplyDeadZone(leftYawInput) * rotateSpeed;
        }

        // ---------------------------------------------------------------
        // Smooth acceleration / deceleration
        // ---------------------------------------------------------------
        float horizRate = targetHoriz.magnitude >= currentHoriz.magnitude
            ? accelerationRate : decelerationRate;
        currentHoriz = Vector3.MoveTowards(currentHoriz, targetHoriz, horizRate * Time.deltaTime);

        float altRate = Mathf.Abs(targetAlt) >= Mathf.Abs(currentAlt)
            ? accelerationRate : decelerationRate;
        currentAlt = Mathf.MoveTowards(currentAlt, targetAlt, altRate * Time.deltaTime);

        float yawRate = Mathf.Abs(targetYaw) >= Mathf.Abs(currentYaw)
            ? accelerationRate : decelerationRate;
        currentYaw = Mathf.MoveTowards(currentYaw, targetYaw, yawRate * Time.deltaTime);

        // ---------------------------------------------------------------
        // Apply
        // ---------------------------------------------------------------
        Vector3 velocity = currentHoriz + Vector3.up * currentAlt;
        CurrentSpeed   = velocity.magnitude;
        CurrentYawRate = Mathf.Abs(currentYaw);

        if (motorSource != null)
            motorSource.pitch = Mathf.Lerp(minPitch, maxPitch, CurrentSpeed / moveSpeed);

        if (accelerateSource != null && accelerateClip != null)
        {
            bool speeding = CurrentSpeed > previousSpeed + 0.05f;
            if (speeding  && !accelerateSource.isPlaying) accelerateSource.PlayOneShot(accelerateClip);
            if (!speeding &&  accelerateSource.isPlaying) accelerateSource.Stop();
        }
        previousSpeed = CurrentSpeed;

        transform.position += velocity * Time.deltaTime;
        transform.Rotate(Vector3.up, currentYaw * Time.deltaTime, Space.World);

        if (disableHeadMovement && trackingSpace != null)
        {
            trackingSpace.localPosition = Vector3.zero;
            trackingSpace.localRotation = Quaternion.identity;
        }
    }

    private static bool TryGetHandVectors(XRHand hand, bool isRightHand,
        out Vector3 palmNormal, out Vector3 fingerDir)
    {
        palmNormal = Vector3.zero;
        fingerDir  = Vector3.zero;
        if (!hand.isTracked) return false;

        if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wristPose)         ||
            !hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out Pose indexPose) ||
            !hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middlePose))
            return false;

        fingerDir          = (middlePose.position - wristPose.position).normalized;
        Vector3 lateralDir = (indexPose.position  - wristPose.position).normalized;

        palmNormal = isRightHand
            ? Vector3.Cross(fingerDir, lateralDir).normalized
            : Vector3.Cross(lateralDir, fingerDir).normalized;

        return true;
    }

    // Yaw from left hand index finger pointing direction.
    // Point index finger right = turn right, left = turn left, straight ahead = neutral.
    private static bool TryGetYawInput(XRHand hand, out float yawInput)
    {
        yawInput = 0f;
        if (!hand.isTracked) return false;
        if (!hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out Pose proximal) ||
            !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose tip))
            return false;

        // World-space X of the finger direction: negative = left, positive = right
        yawInput = (tip.position - proximal.position).normalized.x;
        return true;
    }

    // 3 of 4 fingers curled = fist (tolerates one occluded joint)
    private static bool IsFist(XRHand hand)
    {
        if (!hand.isTracked) return false;
        int curled = 0;
        if (IsFingerCurled(hand, XRHandJointID.IndexTip,  XRHandJointID.IndexProximal))  curled++;
        if (IsFingerCurled(hand, XRHandJointID.MiddleTip, XRHandJointID.MiddleProximal)) curled++;
        if (IsFingerCurled(hand, XRHandJointID.RingTip,   XRHandJointID.RingProximal))   curled++;
        if (IsFingerCurled(hand, XRHandJointID.LittleTip, XRHandJointID.LittleProximal)) curled++;
        return curled >= 3;
    }

    private static bool IsFingerCurled(XRHand hand, XRHandJointID tipId, XRHandJointID proximalId)
    {
        if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wrist) ||
            !hand.GetJoint(tipId).TryGetPose(out Pose tip)                  ||
            !hand.GetJoint(proximalId).TryGetPose(out Pose proximal))
            return false;

        Vector3 knuckleDir = (proximal.position - wrist.position).normalized;
        Vector3 fingerDir  = (tip.position - proximal.position).normalized;
        return Vector3.Dot(knuckleDir, fingerDir) < 0.5f;
    }

    private float ApplyDeadZone(float value)
    {
        float abs = Mathf.Abs(value);
        if (abs < deadZone) return 0f;
        return Mathf.Sign(value) * Mathf.InverseLerp(deadZone, 1f, abs);
    }

    private void KeyboardFallback()
    {
        Vector2 moveInput   = InputSystem.actions.FindAction("Move").ReadValue<Vector2>();
        float enterThrust   = Keyboard.current.enterKey.IsPressed() ? 1f : 0f;
        float elevate       = (Keyboard.current.shiftKey.IsPressed() ? 1f : 0f)
                            + (Keyboard.current.ctrlKey.IsPressed()  ? -1f : 0f);
        float yawInput      = (Keyboard.current.eKey.IsPressed() ? 1f : 0f)
                            + (Keyboard.current.qKey.IsPressed() ? -1f : 0f);

        float forwardInput  = Mathf.Clamp(moveInput.y + enterThrust, -1f, 1f);
        Camera kbCam        = Camera.main;
        float  kbCamYaw     = kbCam != null ? kbCam.transform.eulerAngles.y : transform.eulerAngles.y;
        Quaternion kbCamRot = Quaternion.Euler(0f, kbCamYaw, 0f);
        Vector3 targetHoriz = (kbCamRot * Vector3.right)   * moveInput.x  * moveSpeed
                            + (kbCamRot * Vector3.forward) * forwardInput * moveSpeed;
        float targetAlt = elevate  * altitudeSpeed;
        float targetYaw = yawInput * rotateSpeed;

        float horizRate = targetHoriz.magnitude >= currentHoriz.magnitude ? accelerationRate : decelerationRate;
        currentHoriz = Vector3.MoveTowards(currentHoriz, targetHoriz, horizRate * Time.deltaTime);
        float altRate = Mathf.Abs(targetAlt) >= Mathf.Abs(currentAlt) ? accelerationRate : decelerationRate;
        currentAlt = Mathf.MoveTowards(currentAlt, targetAlt, altRate * Time.deltaTime);
        float yawRate = Mathf.Abs(targetYaw) >= Mathf.Abs(currentYaw) ? accelerationRate : decelerationRate;
        currentYaw = Mathf.MoveTowards(currentYaw, targetYaw, yawRate * Time.deltaTime);

        Vector3 velocity = currentHoriz + Vector3.up * currentAlt;
        CurrentSpeed   = velocity.magnitude;
        CurrentYawRate = Mathf.Abs(currentYaw);

        if (motorSource != null)
            motorSource.pitch = Mathf.Lerp(minPitch, maxPitch, CurrentSpeed / moveSpeed);

        transform.position += velocity * Time.deltaTime;
        transform.Rotate(Vector3.up, currentYaw * Time.deltaTime, Space.World);
    }

    public void Respawn()
    {
        CanMove = false;
        currentHoriz = Vector3.zero;
        currentAlt   = 0f;
        currentYaw   = 0f;

        transform.position = RaceTrack.Instance.CurrentCheckpoint.transform.position;
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.LookAt(RaceTrack.Instance.NextCheckpoint.transform);
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

        Countdown.Instance.StartCountdown();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (crashSource != null && crashClip != null)
            crashSource.PlayOneShot(crashClip);
        Respawn();
    }
}
