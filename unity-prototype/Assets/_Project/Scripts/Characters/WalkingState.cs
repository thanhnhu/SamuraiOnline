public class WalkingState : ICharacterState
{
    private BaseCharacter character;
    public WalkingState(BaseCharacter character) { this.character = character; }
    public void EnterState() {}
    public void UpdateState() {}
    public void ExitState() {}
} 