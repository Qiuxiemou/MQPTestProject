using Unity.FPS.Game;
using UnityEngine;

public class AimPointDamageProxy : MonoBehaviour
{
    public Health PlayerHealth;

    public void InflictDamage(
        float damage,
        bool isExplosionDamage,
        GameObject damageSource,
        Vector3 damageDirection,
        Vector3 damagePoint)
    {
        if (PlayerHealth == null)
            return;
        Debug.Log("Hit delayed aimpoint!");
        PlayerHealth.TakeDamage(damage, damageSource);
    }
}
