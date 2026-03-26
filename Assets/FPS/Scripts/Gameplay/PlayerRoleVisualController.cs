using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerRoleVisualController : MonoBehaviour
{
    [Header("FPS UI")]
    public GameObject crosshair;
    public GameObject weaponHUDManager;

    [Header("FPS Objects")]
    public GameObject firstPersonSocket;
    public GameObject weaponCamera;

    [Header("Input")]
    public PlayerInputHandler inputHandler;

    //bool _uiReady;
    //bool _pendingIsSeeker;
    //bool _hasPendingRole;

    bool isSeeker;

    void Start()
    {
        //ApplyRole(RoundManager.Instance.IsSeeker);
        //RoundManager.Instance.OnPlayerRoleChanged += ApplyRole;

        //_uiReady = true;

        //if (_hasPendingRole)
        //{
        //    ApplyUI(_pendingIsSeeker);
        //    _hasPendingRole = false;
        //}

        if (RoundManager.Instance != null)
        {
            if (RoundManager.Instance.IsSeeker == isSeeker)
            {
                isSeeker = !RoundManager.Instance.IsSeeker;
                ApplyUI(isSeeker);
            }
        }
    }

    public void Update()
    {
        if (RoundManager.Instance != null)
        {
            //if (RoundManager.Instance.IsSeeker == isSeeker)
            //{
                isSeeker = !RoundManager.Instance.IsSeeker;
                ApplyUI(isSeeker);
            //}
        }
    }

    void OnDestroy()
    {
        //if (RoundManager.Instance != null)
        //    RoundManager.Instance.OnPlayerRoleChanged -= ApplyRole;
    }

    //void ApplyRole(bool isSeeker)
    //{
    //    isSeeker = !isSeeker;
    //    // UI
    //    if (crosshair) crosshair.SetActive(isSeeker);
    //    if (weaponHUDManager) weaponHUDManager.SetActive(isSeeker);

    //    // FPS visuals
    //    if (firstPersonSocket) firstPersonSocket.SetActive(isSeeker);
    //    if (weaponCamera) weaponCamera.SetActive(isSeeker);

    //    // Input gating
    //    if (inputHandler)
    //        inputHandler.SetSeekerControls(isSeeker);
    //}

    //void ApplyRole(bool isSeeker)
    //{
    //    isSeeker = !isSeeker;
    //    //if (!_uiReady)
    //    //{
    //    //    _pendingIsSeeker = isSeeker;
    //    //    _hasPendingRole = true;
    //    //    return;
    //    //}

    //    ApplyUI(isSeeker);
    //}

    void ApplyUI(bool isSeeker)
    {
        if (crosshair) crosshair.SetActive(isSeeker);
        //if (weaponHUDManager) weaponHUDManager.SetActive(isSeeker);

        if (firstPersonSocket) firstPersonSocket.SetActive(isSeeker);
        if (weaponCamera) weaponCamera.SetActive(isSeeker);
    }

}
