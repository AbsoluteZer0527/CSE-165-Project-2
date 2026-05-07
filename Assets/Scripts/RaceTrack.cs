using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RaceTrack : MonoBehaviour
{
    public static RaceTrack Instance;

    public int NextCheckpointIndex = 0;
    public List<Checkpoint> Checkpoints = new();
    public float RaceTimer;
    public bool hasTimerStarted;

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

        SetNextCheckpoint();
    }

    private void Update()
    {
        if (hasTimerStarted && NextCheckpointIndex <= Checkpoints.Count)
        {
            RaceTimer += Time.deltaTime;
        }
        TimerTMP.text = $"{NextCheckpointIndex - 1}/{Checkpoints.Count}\n{RaceTimer:F2}";
    }

    public void SetNextCheckpoint()
    {
        if (NextCheckpointIndex >= Checkpoints.Count) return;

        Checkpoints[NextCheckpointIndex].SetAsNextCheckpoint(true);
        NextCheckpointIndex++;
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
