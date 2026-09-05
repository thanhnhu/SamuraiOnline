public class FallingState : ICharacterState
{
    private BaseCharacter character;
    public FallingState(BaseCharacter character) { this.character = character; }
    public void EnterState() {}
    public void UpdateState() {}
    public void ExitState() {}
} 