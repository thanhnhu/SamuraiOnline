public class AttackingState : ICharacterState
{
    private BaseCharacter character;
    public AttackingState(BaseCharacter character) { this.character = character; }
    public void EnterState() {}
    public void UpdateState() {}
    public void ExitState() {}
} 