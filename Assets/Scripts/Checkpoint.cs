using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public bool IsNextCheckpoint;
    public Material DefaultMaterial;
    public Material IsNextMaterial;
    public Material TriggerMaterial;
    public Material IsNextTriggerMaterial;

    [Header("Audio")]
    [SerializeField] private AudioClip reachedClip;
    [SerializeField] private AudioClip beaconClip;
    [SerializeField] private float beaconMaxDistance = 150f;
    [SerializeField] private float minPitch      = 0.6f;
    [SerializeField] private float maxPitch      = 2.0f;
    [SerializeField] private float minPulseRate  = 0.8f;   // Hz when far
    [SerializeField] private float maxPulseRate  = 6f;     // Hz when close

    private AudioSource beaconSource;
    private float pulseTime;

    private void Awake()
    {
        beaconSource = gameObject.AddComponent<AudioSource>();
        beaconSource.spatialBlend  = 1f;
        beaconSource.rolloffMode   = AudioRolloffMode.Logarithmic;
        beaconSource.minDistance   = 3f;
        beaconSource.maxDistance   = beaconMaxDistance;
        beaconSource.loop          = true;
        beaconSource.playOnAwake   = false;
        beaconSource.dopplerLevel  = 1.5f;  // extra spatial feel when moving past
    }

    private void Update()
    {
        if (!IsNextCheckpoint || !beaconSource.isPlaying || Drone.Instance == null) return;

        float dist = Vector3.Distance(transform.position, Drone.Instance.transform.position);

        // t = 0 when at maxDistance, t = 1 when right on top of checkpoint
        float t = 1f - Mathf.Clamp01(dist / beaconMaxDistance);
        // square the curve so changes feel more dramatic near the checkpoint
        float tSquared = t * t;

        // pitch rises steeply as you close in
        beaconSource.pitch = Mathf.Lerp(minPitch, maxPitch, tSquared);

        // pulse rate ramps up with distance (sonar ping effect)
        float pulseFreq = Mathf.Lerp(minPulseRate, maxPulseRate, tSquared);
        pulseTime += Time.deltaTime * pulseFreq * Mathf.PI * 2f;
        float pulse = (Mathf.Sin(pulseTime) + 1f) * 0.5f;  // 0..1

        // volume swings between near-silence and full so the pulsing is obvious
        beaconSource.volume = Mathf.Lerp(0.05f, 1f, pulse);
    }

    public void SetAsNextCheckpoint(bool isNext)
    {
        IsNextCheckpoint = isNext;

        if (isNext)
        {
            GetComponent<MeshRenderer>().material = IsNextMaterial;
            transform.GetChild(0).GetComponent<MeshRenderer>().material = IsNextTriggerMaterial;

            if (RaceTrack.Instance.SpatialAudioMode && beaconClip != null)
            {
                pulseTime = 0f;
                beaconSource.clip = beaconClip;
                beaconSource.Play();
            }
        }
        else
        {
            GetComponent<MeshRenderer>().material = DefaultMaterial;
            transform.GetChild(0).GetComponent<MeshRenderer>().material = TriggerMaterial;
            beaconSource.Stop();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (IsNextCheckpoint)
        {
            if (reachedClip != null)
                AudioSource.PlayClipAtPoint(reachedClip, transform.position);
            RaceTrack.Instance.SetNextCheckpoint();
        }
        SetAsNextCheckpoint(false);
    }
}
