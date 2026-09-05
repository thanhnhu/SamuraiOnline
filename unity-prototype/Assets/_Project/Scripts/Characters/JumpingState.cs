public class JumpingState : ICharacterState
{
    private BaseCharacter character;
    public JumpingState(BaseCharacter character) { this.character = character; }
    public void EnterState() {}
    public void UpdateState() {}
    public void ExitState() {}
} 