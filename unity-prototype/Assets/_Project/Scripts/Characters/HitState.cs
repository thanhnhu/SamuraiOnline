public class HitState : ICharacterState
{
    private BaseCharacter character;
    public HitState(BaseCharacter character) { this.character = character; }
    public void EnterState() {}
    public void UpdateState() {}
    public void ExitState() {}
} 