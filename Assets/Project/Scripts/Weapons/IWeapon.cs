using UnityEngine;

public interface IWeapon
{
    void Attack();

    void Equip(Transform parent);

    void Unequip();
}