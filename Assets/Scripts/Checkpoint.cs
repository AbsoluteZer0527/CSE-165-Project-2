using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public bool IsNextCheckpoint;
    public Material DefaultMaterial;
    public Material IsNextMaterial;
    public Material TriggerMaterial;
    public Material IsNextTriggerMaterial;

    public void SetAsNextCheckpoint(bool isNext)
    {
        IsNextCheckpoint = isNext;
        if (isNext)
        {
            GetComponent<MeshRenderer>().material = IsNextMaterial;
            transform.GetChild(0).GetComponent<MeshRenderer>().material = IsNextTriggerMaterial;
        }
        else
        {
            GetComponent<MeshRenderer>().material = DefaultMaterial;
            transform.GetChild(0).GetComponent<MeshRenderer>().material = TriggerMaterial;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (IsNextCheckpoint)
        {
            RaceTrack.Instance.SetNextCheckpoint();
        }
        SetAsNextCheckpoint(false);
    }
}
