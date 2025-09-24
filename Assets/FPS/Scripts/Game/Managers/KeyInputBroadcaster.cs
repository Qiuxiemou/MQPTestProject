using UnityEngine;
using Unity.FPS.Game;

public class KeyInputBroadcaster : MonoBehaviour
{
    public string PlayerId = "Player1";

    public KeyCode[] KeysToWatch = new[]
    {
        KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
        KeyCode.Space, KeyCode.LeftShift, KeyCode.LeftControl,
        KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.R
    };

    void Update()
    {
        foreach (var k in KeysToWatch)
        {
            if (Input.GetKeyDown(k))
                EventManager.Broadcast(new KeyPressEvent { PlayerId = PlayerId, Key = k, Pressed = true });

            if (Input.GetKeyUp(k))
                EventManager.Broadcast(new KeyPressEvent { PlayerId = PlayerId, Key = k, Pressed = false });
        }
    }
}