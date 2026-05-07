using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RaceTrack : MonoBehaviour
{
    public static RaceTrack Instance;

    public int NextCheckpointIndex = 1;
    public List<Checkpoint> Checkpoints = new();
    public float RaceTimer;
    public bool HasTimerStarted;

    [Header("Refs")]
    public TextAsset file;
    public GameObject CheckpointPrefab;
    public TextMeshProUGUI TimerTMP;
    public Checkpoint CurrentCheckpoint => Checkpoints[NextCheckpointIndex - 1];
    public Checkpoint NextCheckpoint => Checkpoints[NextCheckpointIndex];

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        foreach (Vector3 pos in ParseFile())
        {
            GameObject checkpointObject = Instantiate(CheckpointPrefab, transform);
            checkpointObject.transform.position = pos;

            Checkpoints.Add(checkpointObject.GetComponent<Checkpoint>());
        }

        for (int i = 0; i < Checkpoints.Count - 1; i++)
        {
            Checkpoints[i].transform.LookAt(Checkpoints[i + 1].transform);
        }

        Checkpoints[1].SetAsNextCheckpoint(true);
        Drone.Instance.Respawn();
    }

    private void Update()
    {
        if (HasTimerStarted && NextCheckpointIndex <= Checkpoints.Count)
        {
            RaceTimer += Time.deltaTime;
        }
        TimerTMP.text = $"{NextCheckpointIndex}/{Checkpoints.Count}\n{RaceTimer:F2}";
    }

    public void SetNextCheckpoint()
    {
        NextCheckpointIndex++;
        if (NextCheckpointIndex >= Checkpoints.Count)
        {
            HasTimerStarted = false;
            return;
        }

        Checkpoints[NextCheckpointIndex].SetAsNextCheckpoint(true);
    }

    List<Vector3> ParseFile()
    {
        float ScaleFactor = 1.0f / 39.37f;
        List<Vector3> positions = new List<Vector3>();
        string content = file.ToString();
        string[] lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string[] coords = lines[i].Split(' ');
            Vector3 pos = new Vector3(float.Parse(coords[0]), float.Parse(coords[1]), float.Parse(coords[2]));
            positions.Add(pos * ScaleFactor);
        }
        return positions;
    }
}
