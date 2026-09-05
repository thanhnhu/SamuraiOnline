public class DeadState : ICharacterState
{
    private BaseCharacter character;
    public DeadState(BaseCharacter character) { this.character = character; }
    public void EnterState() {}
    public void UpdateState() {}
    public void ExitState() {}
} 