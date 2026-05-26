using UnityEngine;

public class DummyEnemy : MonoBehaviour, IDamageable
{
    public float health = 100f;

    public void TakeDamage(float amount, GameObject source)
    {
        health -= amount;

        Debug.Log($"{gameObject.name} recibió {amount}");

        if (health <= 0)
            Destroy(gameObject);
    }
}