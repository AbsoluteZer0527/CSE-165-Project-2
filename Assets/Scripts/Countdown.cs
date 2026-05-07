using System.Collections;
using TMPro;
using UnityEngine;

public class Countdown : MonoBehaviour
{
    public static Countdown Instance;

    public TextMeshProUGUI CountdownTMP;

    private void Awake()
    {
        Instance = this;
    }

    public void StartCountdown()
    {
        StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        CountdownTMP.text = "3";
        yield return new WaitForSeconds(1);
        CountdownTMP.text = "2";
        yield return new WaitForSeconds(1);
        CountdownTMP.text = "1";
        yield return new WaitForSeconds(1);
        CountdownTMP.text = "";
        RaceTrack.Instance.HasTimerStarted = true;
        Drone.Instance.CanMove = true;
        yield return null;
    }
}
