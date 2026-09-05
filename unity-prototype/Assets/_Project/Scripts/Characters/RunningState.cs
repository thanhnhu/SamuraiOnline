public class RunningState : ICharacterState
{
    private BaseCharacter character;
    public RunningState(BaseCharacter character) { this.character = character; }
    public void EnterState() {}
    public void UpdateState() {}
    public void ExitState() {}
} 