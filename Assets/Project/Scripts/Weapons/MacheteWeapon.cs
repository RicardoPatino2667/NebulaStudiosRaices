using UnityEngine;

public class MacheteWeapon : MonoBehaviour, IWeapon
{
    public float damage = 25f;

    public float range = 2f;

    public LayerMask hitMask;

    public void Attack()
    {
        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                range,
                hitMask
            );

        foreach (var h in hits)
        {
            if (h.TryGetComponent(out IDamageable dmg))
            {
                dmg.TakeDamage(damage, gameObject);
            }
        }
    }

    public void Equip(Transform parent)
    {
        transform.SetParent(parent);

        transform.localPosition = Vector3.zero;
    }

    public void Unequip()
    {
        Destroy(gameObject);
    }
}