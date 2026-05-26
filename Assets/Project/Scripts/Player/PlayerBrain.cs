using UnityEngine;

public class PlayerBrain : MonoBehaviour
{
    public PlayerInputHandler input;
    public PlayerMovement movement;
    public PlayerCombat combat;
    public PlayerInteraction interaction;
    public PlayerAnimation animationController;
    public PlayerStateMachine stateMachine;

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        interaction = GetComponent<PlayerInteraction>();
        animationController = GetComponent<PlayerAnimation>();

        stateMachine = new PlayerStateMachine(this);
    }

    private void Update()
    {
        stateMachine.Update();
    }

    private void FixedUpdate()
    {
        //movement.PhysicsUpdate();
    }
}