public class PlayerStateMachine
{
    private PlayerBrain player;

    private PlayerState currentState;

    public PlayerStateMachine(PlayerBrain player)
    {
        this.player = player;

        ChangeState(PlayerState.Idle);
    }

    public void Update()
    {
        switch (currentState)
        {
            case PlayerState.Idle:

                if (player.input.MoveInput.magnitude > 0.1f)
                    ChangeState(PlayerState.Move);

                break;

            case PlayerState.Move:

                if (player.input.MoveInput.magnitude < 0.1f)
                    ChangeState(PlayerState.Idle);

                break;
        }

        if (player.input.AttackPressed)
            player.combat.Attack();
    }

    public void ChangeState(PlayerState newState)
    {
        currentState = newState;
    }
}