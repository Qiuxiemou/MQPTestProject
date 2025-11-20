using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class EnemyLineOfSightChecker : MonoBehaviour
{
    public SphereCollider Collider;
    public float FieldOfView = 90f;
    public LayerMask LineOfSightLayers;

    public delegate void GainSightEvent(Transform Target);
    public GainSightEvent OnGainSight;
    public delegate void LoseSightEvent(Transform Target);
    public LoseSightEvent OnLoseSight;

    private Coroutine CheckForLineOfSightCoroutine;

    private void Awake()
    {
        Collider = GetComponent<SphereCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!CheckLineOfSight(other.transform))
        {
            CheckForLineOfSightCoroutine = StartCoroutine(CheckForLineOfSight(other.transform));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        OnLoseSight?.Invoke(other.transform);
        if (CheckForLineOfSightCoroutine != null)
        {
            StopCoroutine(CheckForLineOfSightCoroutine);
        }
    }

    private bool CheckLineOfSight(Transform target)
{
    Vector3 origin = transform.position + Vector3.up * 1.6f;
    Vector3 targetPos = target.position + Vector3.up * 1.0f;
    Vector3 dir = (targetPos - origin).normalized;

    float dist = Vector3.Distance(origin, targetPos) + 0.2f;

    if (Physics.Raycast(origin, dir, out var hit, dist, LineOfSightLayers, QueryTriggerInteraction.Collide))
    {
        if (hit.transform.root == target.root)
        {
            Debug.Log("[LOS] GainSight " + target.name);
            OnGainSight?.Invoke(target);
            return true;
        }
        else
        {
            Debug.Log("[LOS] Blocked by " + hit.transform.name);
        }
    }
    else
    {
        Debug.Log("[LOS] Ray miss");
    }

    Debug.DrawLine(origin, origin + dir * dist, Color.cyan, 0.1f);
    return false;
}


    private IEnumerator CheckForLineOfSight(Transform Target)
    {
        WaitForSeconds Wait = new WaitForSeconds(0.5f);

        while(!CheckLineOfSight(Target))
        {
            yield return Wait;
        }
    }
}