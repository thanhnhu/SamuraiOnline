public class SpecialState : ICharacterState
{
    private BaseCharacter character;
    public SpecialState(BaseCharacter character) { this.character = character; }
    public void EnterState() {}
    public void UpdateState() {}
    public void ExitState() {}
} 