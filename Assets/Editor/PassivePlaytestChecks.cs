using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Input;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace AshenRealm.EditorTools
{
    /// <summary>Class passives exercised through real focus, sphere casts, health filters and saved souls.</summary>
    internal static class PassivePlaytestChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        static IEnumerable<float> WaitGame(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return 0.025f;
        }
        static float StatTotal(PlayerProgression growth) => growth.MaxHealth + growth.MaxFocus + growth.Attack + growth.Defense + growth.Magic + growth.Speed;
        static float SkillPower(PlayerProgression growth) => growth.Capture().Skills.Sum(s => s.PowerBonus);
        static int CriticalSeed(bool critical)
        {
            for (int seed = 0; seed < 10000; seed++)
            {
                Random.InitState(seed);
                float roll = Random.value;
                if (critical ? roll > 0.08f && roll < 0.1f : roll > 0.1f && roll < 0.12f) return seed;
            }
            throw new InvalidOperationException("No deterministic critical boundary seed found");
        }

        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var keeper = SoulKeeper.Instance;
            var session = GameSession.Instance;
            var sources = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include)
                .Where(e => e.gameObject.scene.IsValid()).GroupBy(e => e.EnemyId).ToDictionary(g => g.Key, g => g.First());
            if (controller == null || keeper == null || session == null || !keeper.HasSoul ||
                !sources.ContainsKey("skeleton") || !sources.ContainsKey("rat") || !sources.ContainsKey("gravemaw"))
            { check(false, "Passive scenarios have the current player, soul and regenerated enemy capsules"); yield break; }
            var passives = controller.GetComponent<PlayerPassives>();
            var growth = controller.GetComponent<PlayerProgression>();
            var health = controller.GetComponent<Health>();
            var status = controller.GetComponent<StatusEffects>();
            var combat = controller.GetComponent<PlayerCombat>();
            var lifecycle = controller.GetComponent<PlayerLifecycle>();
            var input = controller.GetComponent<PlayerInputHub>();
            if (passives == null || growth == null || lifecycle == null)
            { check(false, "The generated player includes its cached passive damage filter and lifecycle"); yield break; }

            keeper.Save();
            SoulRecord original = Copy(keeper.LatestSoul());
            MetaRecord originalMeta = Copy(keeper.Meta);
            Vector3 oldPosition = controller.transform.position;
            float oldYaw = controller.transform.eulerAngles.y, oldPitch = controller.Pitch;
            float oldMax = health.Max, oldHealth = health.Current, oldScale = Time.timeScale;
            bool oldInput = controller.InputEnabled, oldPassives = passives.enabled, oldArmed = combat.Armed;
            CursorLockMode oldLock = Cursor.lockState;
            bool oldCursor = Cursor.visible;
            Random.State randomState = Random.state;
            var oldProjectiles = new HashSet<EntityId>(Object.FindObjectsByType<Projectile>().Select(p => p.GetEntityId()));
            MonoBehaviour[] guards = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(b => b is EnemyAI || b is CursedKnightAI || b is StoryDirector || b is CityActivityLedger ||
                    b == lifecycle || b == input || b is PlayerAbilities).ToArray();
            bool[] wasEnabled = guards.Select(b => b.enabled).ToArray();
            var ownIds = new List<string>();
            var clones = new List<GameObject>();
            Vector3 origin = new Vector3(-1650f, 0f, -700f);
            var arena = new GameObject("Passive Scenario Arena");
            var floor = new GameObject("Passive Scenario Floor");
            floor.transform.SetParent(arena.transform, false);
            floor.transform.position = origin + Vector3.down * 0.5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(60f, 1f, 60f);
            var underground = new GameObject("Passive Scenario Depth").AddComponent<Zone>();
            underground.transform.SetParent(arena.transform, false);
            Set(underground, "underground", true);
            var surface = new GameObject("Passive Scenario Surface").AddComponent<Zone>();
            surface.transform.SetParent(arena.transform, false);
            var outsider = new GameObject("Passive Scenario Nonplayer Source");
            outsider.transform.SetParent(arena.transform, false);

            void ClearActors()
            {
                CombatFocus.Set(null);
                foreach (GameObject clone in clones)
                    if (clone != null) { clone.SetActive(false); Object.Destroy(clone); }
                clones.Clear();
            }
            void PositionPlayer()
            {
                controller.Teleport(origin + new Vector3(0f, 0.06f, 2f), 180f, 0f);
                Physics.SyncTransforms();
            }
            SoulRecord LoadCase(CharacterClass id)
            {
                ClearActors();
                SoulRecord soul = Copy(original);
                soul.Id = "passive_test_" + Guid.NewGuid().ToString("N");
                soul.ClassId = (int)id;
                soul.Zone = ZoneNumbers.City;
                soul.WorldVersion = 0;
                soul.Progress = null;
                soul.Flags = new List<string>();
                soul.WeaponId = soul.RelicId = null;
                soul.WeaponLevel = 0;
                soul.Health = soul.Focus = 10000f;
                ownIds.Add(soul.Id);
                SaveStore.Put(soul);
                keeper.Load(SaveStore.Find(soul.Id));
                status.Clear();
                health.ResetHealth();
                combat.SetArmed(true);
                PositionPlayer();
                return soul;
            }
            EnemyAI Spawn(string id, float x = 0f, bool dungeon = true)
            {
                var holder = new GameObject("Passive Scenario Spawn");
                holder.transform.SetParent((dungeon ? underground : surface).transform, false);
                holder.SetActive(false);
                var clone = Object.Instantiate(sources[id].gameObject, holder.transform);
                clones.Add(holder);
                clone.name = "Passive Scenario " + id;
                clone.transform.SetPositionAndRotation(origin + new Vector3(x, 0.06f, 0f), Quaternion.identity);
                var enemy = clone.GetComponent<EnemyAI>();
                Set(enemy, "saveKey", "passive_foe_" + Guid.NewGuid().ToString("N"));
                clone.GetComponent<CharacterController>().enabled = true;
                enemy.enabled = false;
                clone.SetActive(true);
                holder.SetActive(true);
                enemy.Health.SetMax(1000f, true);
                enemy.GetComponent<StatusEffects>().Clear();
                Physics.SyncTransforms();
                return enemy;
            }
            void Kill(EnemyAI enemy, GameObject source)
            {
                // Enable only while the real Health events run; no autonomous enemy Update runs in this arena.
                enemy.enabled = true;
                enemy.Health.ApplyDamage(new DamageInfo(enemy.Health.Max * 100f, enemy.transform.position + Vector3.up,
                    Vector3.zero, source, bypassMitigation: true));
                enemy.enabled = false;
            }
            void Lethal(EnemyAI attacker) => health.ApplyDamage(new DamageInfo(health.Max * 100f,
                controller.transform.position + Vector3.up, Vector3.forward, attacker.gameObject));

            try
            {
                foreach (MonoBehaviour guard in guards) guard.enabled = false;
                // Disabling lifecycle removes its Health.Died subscription before any deadline can end a soul.
                controller.InputEnabled = false;
                passives.enabled = true;
                Time.timeScale = 3f;
                int critSeed = CriticalSeed(true), ordinarySeed = CriticalSeed(false);

                LoadCase(CharacterClass.Knight);
                EnemyAI beast = Spawn("rat");
                beast.Engage();
                yield return 0.05f;
                check(Mathf.Approximately(status.ShieldPoints, 0f) && Mathf.Approximately(status.AttackBonus, 0f),
                    "Knight's opening does not trigger from an engaged living beast");
                float normalAttack = combat.Damage;
                EnemyAI undead = Spawn("skeleton", 5f);
                undead.Engage();
                yield return 0.05f;
                float shield = growth.Defense * 2f;
                check(Mathf.Abs(status.ShieldPoints - shield) < 0.01f && Mathf.Abs(combat.Damage - normalAttack - 5f) < 0.01f,
                    "Engaging a real undead grants DEF times two shield and five actual attack damage");
                float beforeShieldHit = health.Current;
                health.ApplyDamage(new DamageInfo(3f, controller.transform.position + Vector3.up, Vector3.zero, undead.gameObject));
                float spentShield = status.ShieldPoints;
                undead.Engage(); CombatFocus.Set(undead);
                yield return 0.05f;
                check(Mathf.Approximately(health.Current, beforeShieldHit) && spentShield < shield &&
                    Mathf.Abs(status.ShieldPoints - spentShield) < 0.01f && Mathf.Abs(combat.Damage - normalAttack - 5f) < 0.01f,
                    "The opening shield absorbs real HP damage and repeated focus neither stacks nor refills it");
                foreach (float wait in WaitGame(5.3f)) yield return wait;
                check(status.ShieldPoints > 0f && Mathf.Approximately(status.AttackBonus, 5f),
                    "Knight's opening remains active near the end of its six seconds");
                foreach (float wait in WaitGame(0.9f)) yield return wait;
                check(Mathf.Approximately(status.ShieldPoints, 0f) && Mathf.Abs(combat.Damage - normalAttack) < 0.01f,
                    "Knight's shield and five attack damage expire after six seconds despite repeated focus");

                LoadCase(CharacterClass.Gravethief);
                EnemyAI rogueTarget = Spawn("skeleton");
                rogueTarget.Engage();
                yield return 0.05f;
                int coin = growth.PassiveState.RogueLuck;
                keeper.Save();
                SoulRecord rogueSave = Copy(keeper.LatestSoul());
                keeper.Load(SaveStore.Find(rogueSave.Id));
                PositionPlayer();
                check((coin == 1 || coin == 2) && rogueSave.Progress.Passive.RogueLuck == coin && growth.PassiveState.RogueLuck == coin,
                    "Real combat flips a one-or-two Rogue coin and SoulKeeper loading preserves that result");
                CombatFocus.Set(rogueTarget);
                yield return 0.05f;
                check(growth.PassiveState.RogueLuck == coin, "Repeated engagement keeps the saved Rogue coin");
                ProgressRecord rogueProgress = growth.Capture();
                rogueProgress.Passive.RogueLuck = 1;
                growth.Restore(rogueProgress);
                float rogueBefore = rogueTarget.Health.Current;
                GameObject struck = combat.Strike(1f, 3f, 0.4f);
                float bleedStart = rogueTarget.Health.Current;
                check(struck == rogueTarget.gameObject && rogueBefore > bleedStart && rogueTarget.GetComponent<StatusEffects>().Bleeding,
                    "A real Rogue sphere-cast hit starts the bleeding face of the coin");
                foreach (float wait in WaitGame(6.3f)) yield return wait;
                check(Mathf.Abs(bleedStart - rogueTarget.Health.Current - 9f) < 0.02f,
                    "The Rogue's three-turn bleed pays exactly nine HP over six seconds");
                rogueTarget.GetComponent<StatusEffects>().Clear();
                rogueProgress = growth.Capture(); rogueProgress.Passive.RogueLuck = 2; growth.Restore(rogueProgress);
                float beforeCritical = rogueTarget.Health.Current;
                Random.InitState(critSeed);
                combat.Strike(10f, 3f, 0.4f);
                float criticalDamage = beforeCritical - rogueTarget.Health.Current;
                float beforeOrdinary = rogueTarget.Health.Current;
                Random.InitState(ordinarySeed);
                combat.Strike(10f, 3f, 0.4f);
                float ordinaryDamage = beforeOrdinary - rogueTarget.Health.Current;
                check(Mathf.Abs(ordinaryDamage - 10f) < 0.02f && Mathf.Abs(criticalDamage - 30f) < 0.02f &&
                    !rogueTarget.GetComponent<StatusEffects>().Bleeding,
                    "The critical coin triples actual basic-hit HP damage below ten percent and leaves rolls above it normal");

                LoadCase(CharacterClass.Witch);
                float witchStats = StatTotal(growth), witchPower = SkillPower(growth);
                EnemyAI uncredited = Spawn("rat");
                Kill(uncredited, outsider);
                check(Mathf.Approximately(StatTotal(growth), witchStats) && Mathf.Approximately(SkillPower(growth), witchPower),
                    "A nonplayer kill grants no Witch accretion");
                EnemyAI credited = Spawn("skeleton", 5f);
                Kill(credited, controller.gameObject);
                ProgressRecord witchProgress = growth.Capture();
                check(Mathf.Abs(StatTotal(growth) - witchStats - 5f) < 0.01f && Mathf.Abs(SkillPower(growth) - witchPower - 5f) < 0.01f &&
                    witchProgress.Skills.Where(s => s.PowerBonus > 0f).All(s => growth.SkillTier(s.Id) > 0 && AbilityBook.Find(s.Id).Tier(growth.SkillTier(s.Id)).Power > 0f),
                    "An actual player kill adds five real Witch stat points and five power to a learned damaging skill");
                credited.Health.SetCurrent(credited.Health.Max);
                Kill(credited, controller.gameObject);
                check(Mathf.Abs(StatTotal(growth) - witchStats - 5f) < 0.01f && Mathf.Abs(SkillPower(growth) - witchPower - 5f) < 0.01f,
                    "A second real death event from the same enemy cannot credit Witch accretion twice");

                LoadCase(CharacterClass.Warden);
                EnemyAI vigilTarget = Spawn("skeleton");
                EnemyAI wrongTarget = Spawn("rat", 6f);
                vigilTarget.Engage();
                yield return 0.05f;
                Lethal(vigilTarget);
                check(Mathf.Abs(health.Current - 1f) < 0.01f && growth.PassiveState.VigilUsed && growth.PassiveState.VigilActive &&
                    Mathf.Abs(growth.PassiveState.VigilRemaining - 4f) < 0.01f && growth.PassiveState.VigilTarget == vigilTarget.SaveKey,
                    "Health's cached Warden damage filter turns a lethal enemy blow into one HP and a four-second saved target");
                foreach (float wait in WaitGame(1.2f)) yield return wait;
                keeper.Save();
                SoulRecord vigilSave = Copy(keeper.LatestSoul());
                float remaining = vigilSave.Progress.Passive.VigilRemaining;
                keeper.Load(SaveStore.Find(vigilSave.Id));
                PositionPlayer();
                check(remaining > 0f && remaining < 3f && growth.PassiveState.VigilActive &&
                    Mathf.Abs(growth.PassiveState.VigilRemaining - remaining) < 0.01f && growth.PassiveState.VigilTarget == vigilTarget.SaveKey,
                    "Real Warden save/load restores the elapsed remainder and foe key instead of restarting four seconds");
                Kill(wrongTarget, controller.gameObject);
                check(growth.PassiveState.VigilActive && health.IsAlive,
                    "Killing the wrong real foe leaves Warden's deadline active");
                Kill(vigilTarget, controller.gameObject);
                check(!growth.PassiveState.VigilActive && Mathf.Approximately(growth.PassiveState.VigilRemaining, 0f) && health.IsAlive,
                    "Killing the saved attacking foe clears Warden's deadline");
                health.ResetHealth(); status.Clear();
                EnemyAI secondAttacker = Spawn("skeleton", -5f);
                Lethal(secondAttacker);
                check(!health.IsAlive && growth.PassiveState.VigilUsed && !lifecycle.IsDead && keeper.HasSoul,
                    "A second lethal blow in the same descent kills normally while the isolated lifecycle cannot end the soul");
                health.ResetHealth();
                session.AddDescent();
                yield return 0.05f;
                Lethal(secondAttacker);
                check(Mathf.Abs(health.Current - 1f) < 0.01f && growth.PassiveState.VigilActive,
                    "The next descent gives Warden one fresh last-vigil interception");
                status.ApplyShield(100000f, 10f);
                foreach (float wait in WaitGame(4.3f)) yield return wait;
                check(!health.IsAlive && status.ShieldPoints > 90000f && !growth.PassiveState.VigilActive && !lifecycle.IsDead && keeper.HasSoul,
                    "The actual Warden deadline bypasses a large live shield and cannot trigger a second vigil or a soul-ending lifecycle");

                LoadCase(CharacterClass.Alchemist);
                float immuneHealth = health.Current;
                status.ApplyPoison(8f, 2f, outsider);
                status.ApplyTurnPoison(8f, 1, outsider);
                status.ApplyBleed(8f, 2f, outsider);
                status.ApplyRot(8f, 2f, outsider);
                foreach (float wait in WaitGame(2.3f)) yield return wait;
                check(Mathf.Approximately(health.Current, immuneHealth) && !status.Poisoned && !status.Bleeding && !status.Rotting,
                    "Alchemist's iron palate prevents actual poison, turn poison, bleeding and rot HP ticks");
                int herbs = growth.Herbs;
                EnemyAI common = Spawn("rat"); Kill(common, controller.gameObject);
                check(growth.Herbs == herbs + 1, "An actual common dungeon kill gives Alchemist one plant");
                EnemyAI nonplayer = Spawn("rat", 5f); Kill(nonplayer, outsider);
                EnemyAI aboveGround = Spawn("rat", -5f, false); Kill(aboveGround, controller.gameObject);
                check(growth.Herbs == herbs + 1, "Nonplayer dungeon kills and player surface kills grant no Alchemist plants");
                EnemyAI elite = Spawn("gravemaw", 10f); Kill(elite, controller.gameObject);
                check(growth.Herbs == herbs + 3, "An actual elite dungeon kill gives Alchemist two plants");
                elite.Health.SetCurrent(elite.Health.Max); Kill(elite, controller.gameObject);
                keeper.Save();
                check(growth.Herbs == herbs + 3 && keeper.LatestSoul().Progress.Herbs == herbs + 3,
                    "Repeated enemy death cannot duplicate Alchemist plants and the earned stock is really saved");

                LoadCase(CharacterClass.Necromancer);
                keeper.AddSouls(Mathf.Max(0, 100 - keeper.Souls));
                growth.TryChoosePath(NecromancerPath.Attack);
                growth.TryLearnOrUpgradeSkill("n_bonespear");
                int learnedTier = growth.SkillTier("n_bonespear");
                session.AddDescent();
                yield return 0.05f;
                check(growth.PassiveState.DescentId == session.Descents && growth.ChosenPath == NecromancerPath.None &&
                    learnedTier > 0 && growth.SkillTier("n_bonespear") == learnedTier && !growth.PassiveState.VigilUsed &&
                    growth.PassiveState.RogueLuck == 0 && Mathf.Approximately(status.ShieldPoints, 0f),
                    "The live passive Update cooperates with Necromancer's descent reset while preserving learned skills");
            }
            finally
            {
                ClearActors();
                arena.SetActive(false);
                Object.Destroy(arena);
                foreach (Projectile projectile in Object.FindObjectsByType<Projectile>())
                    if (!oldProjectiles.Contains(projectile.GetEntityId())) Object.Destroy(projectile.gameObject);
                passives.enabled = false;
                keeper.Load(Copy(original));
                status.Clear();
                health.SetMax(oldMax, false); health.SetCurrent(oldHealth);
                controller.Teleport(oldPosition, oldYaw, oldPitch);
                combat.SetArmed(oldArmed);
                SaveStore.Put(Copy(original));
                foreach (string id in ownIds) SaveStore.Delete(id);
                MetaRecord meta = keeper.Meta;
                meta.Souls = originalMeta.Souls; meta.Descents = originalMeta.Descents; meta.Deepest = originalMeta.Deepest;
                meta.Codex = originalMeta.Codex; meta.UnlockedClasses = originalMeta.UnlockedClasses;
                meta.SkillMigrationRefunds = originalMeta.SkillMigrationRefunds;
                SaveStore.SaveMeta(meta);
                controller.InputEnabled = oldInput;
                for (int i = guards.Length - 1; i >= 0; i--) if (guards[i] != null) guards[i].enabled = wasEnabled[i];
                passives.enabled = oldPassives;
                Cursor.lockState = oldLock; Cursor.visible = oldCursor;
                Time.timeScale = oldScale;
                Random.state = randomState;
            }
        }
    }
}
