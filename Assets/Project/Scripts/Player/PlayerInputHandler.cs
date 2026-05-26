using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }

    public bool JumpPressed { get; private set; }

    public bool AttackPressed { get; private set; }

    private void Update()
    {
        MoveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        JumpPressed = Input.GetButtonDown("Jump");

        AttackPressed = Input.GetMouseButtonDown(0);
    }
}