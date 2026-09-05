public class IdleState : ICharacterState
{
    private BaseCharacter character;
    public IdleState(BaseCharacter character) { this.character = character; }
    public void EnterState() {}
    public void UpdateState() {}
    public void ExitState() {}
} 