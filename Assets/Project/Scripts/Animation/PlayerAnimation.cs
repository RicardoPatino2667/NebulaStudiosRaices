using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator anim;

    private Rigidbody rb;

    private void Awake()
    {
        anim = GetComponent<Animator>();

        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        float speed =
            new Vector3(
                rb.linearVelocity.x,
                0,
                rb.linearVelocity.z
            ).magnitude;

        anim.SetFloat("Speed", speed);
    }
}