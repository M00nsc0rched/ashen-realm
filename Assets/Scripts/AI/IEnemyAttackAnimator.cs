using AshenRealm.Story;

namespace AshenRealm.AI
{
    /// <summary>Optional presentation extension; the Cursed Knight's original animation contract stays intact.</summary>
    public interface IEnemyAttackAnimator
    {
        void SetAttackStyle(MoveKind kind);
        void SetBurrow(float amount);
    }
}
