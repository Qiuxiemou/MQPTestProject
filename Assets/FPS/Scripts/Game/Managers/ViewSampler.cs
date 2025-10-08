using System.Collections;
using UnityEngine;
using Unity.FPS.Game;

public class ViewSampler : MonoBehaviour
{
    public string PlayerId = "Player1";
    public Transform CameraTransform;  

    void OnEnable()
    {
        if (!CameraTransform) CameraTransform = Camera.main ? Camera.main.transform : transform;

    }

    void Update()
    {
        var t = CameraTransform ? CameraTransform : transform;
        EventManager.Broadcast(new ViewSampleEvent
        {
            PlayerId = PlayerId,
            Position = t.position,
            RotationEuler = t.rotation.eulerAngles,
            Forward = t.forward
        });

    }

}
