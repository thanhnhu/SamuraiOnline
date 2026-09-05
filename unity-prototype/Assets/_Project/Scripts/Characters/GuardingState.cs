public class GuardingState : ICharacterState
{
    private BaseCharacter character;
    public GuardingState(BaseCharacter character) { this.character = character; }
    public void EnterState() {}
    public void UpdateState() {}
    public void ExitState() {}
} 