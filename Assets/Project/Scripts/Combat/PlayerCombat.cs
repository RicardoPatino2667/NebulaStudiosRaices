using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public Transform weaponHolder;

    private IWeapon currentWeapon;

    public void Equip(IWeapon weapon)
    {
        currentWeapon?.Unequip();

        currentWeapon = weapon;

        currentWeapon.Equip(weaponHolder);
    }

    public void Attack()
    {
        currentWeapon?.Attack();
    }
}