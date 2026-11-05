using AshenRealm.Story;

namespace AshenRealm.Combat
{
    /// <summary>Applies what a landed enemy move does beyond its damage.</summary>
    public static class EnemyMoveEffects
    {
        public static void Apply(EnemyMove move, StatusEffects target)
        {
            if (target == null) return;
            if (move.PoisonTime > 0f) target.ApplyPoison(move.PoisonDps, move.PoisonTime);
            if (move.StunTime > 0f) target.ApplyStun(move.StunTime);
            if (move.WeakenTime > 0f) target.ApplyWeaken(move.WeakenTime);
        }
    }
}
