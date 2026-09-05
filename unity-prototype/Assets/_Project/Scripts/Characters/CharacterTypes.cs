//public enum CharacterState
//{
//    Idle,
//    Walking,
//    Running,
//    Jumping,
//    Falling,
//    Attacking,
//    Guarding,
//    Hit,
//    Dead,
//    Special
//}

public enum AttackType
{
    Light,
    Medium,
    Heavy,
    Special
}

public interface ICharacterState
{
    void EnterState();
    void UpdateState();
    void ExitState();
}

public class CharacterStateData
{
    public CharacterState currentState;
    // Có thể thêm các trường khác nếu cần
} 