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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Every skill each class can equip, cast the way a player casts it: equipped in Q, the touch ability button pressed,
    /// the camera aimed at a real foe of the first depth at a distance that suits the delivery. A cast counts only when it
    /// changes the fight: the foe loses health or gains a condition, or the caster gains health, a shield, a bonus or focus.
    /// </summary>
    internal static class SkillSweepPlaytestChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const float TargetHealth = 100000f, PlayerMax = 10000f, PlayerStart = 5000f;
        public static readonly List<string> Table = new List<string>();
        static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        /// <summary>How far away a player would stand for this delivery.</summary>
        public static float Distance(AbilityDefinition a)
        {
            switch (a.Delivery)
            {
                case AbilityDelivery.Melee: return 2f;
                case AbilityDelivery.Nova: return 2.5f;
                case AbilityDelivery.Field: return Mathf.Clamp(a.Range * 0.45f, 2f, 6f);
                case AbilityDelivery.Charge: case AbilityDelivery.Leap: return a.Range * 0.8f;
                case AbilityDelivery.Fan: return 6f;
                default: return 7f;
            }
        }

        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var keeper = SoulKeeper.Instance;
            var session = GameSession.Instance;
            if (player == null || zones == null || keeper == null || !keeper.HasSoul)
            { check(false, "The skill sweep needs the player and an active soul"); yield break; }
            var growth = player.GetComponent<PlayerProgression>();
            var health = player.GetComponent<Health>();
            var status = player.GetComponent<StatusEffects>();
            var combat = player.GetComponent<PlayerCombat>();
            var abilities = player.GetComponent<PlayerAbilities>();
            var input = player.GetComponent<PlayerInputHub>();
            Camera eye = player.GetComponentsInChildren<Camera>(true).First(c => c.CompareTag("MainCamera"));
            keeper.Save();
            SoulRecord original = Copy(keeper.LatestSoul());
            MetaRecord originalMeta = Copy(keeper.Meta);
            var tempSoul = Copy(original);
            tempSoul.Id = "skill_sweep_" + Guid.NewGuid().ToString("N");
            MonoBehaviour[] guards = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(b => b is EnemyAI || b is CursedKnightAI || b is PlayerLifecycle || b is StoryDirector).ToArray();
            bool[] oldEnabled = guards.Select(b => b.enabled).ToArray();
            var clones = new List<GameObject>();
            var settings = InputSystem.settings;
            var focusBehaviour = settings.editorInputBehaviorInPlayMode;
            var backgroundBehaviour = settings.backgroundBehavior;
            Keyboard keyboard = null;
            Gamepad pad = null;
            Table.Clear();
            try
            {
                foreach (MonoBehaviour guard in guards) guard.enabled = false;
                SaveStore.Put(tempSoul);
                keeper.Load(tempSoul);
                zones.Place(ZoneNumbers.FirstDepth, Vector3.zero, 0f, false);
                yield return 0.3f;
                Zone depth = zones.Current;
                EnemyAI source = depth.GetComponentsInChildren<EnemyAI>(true).FirstOrDefault(e => e.Definition != null && e.Definition.Rank == EnemyRank.Common);
                if (source == null) { check(false, "The first depth has a common foe to clone"); yield break; }
                EnemyAI Clone(string name)
                {
                    var copy = Object.Instantiate(source.gameObject, depth.transform);
                    copy.name = name;
                    copy.SetActive(true);
                    var ai = copy.GetComponent<EnemyAI>();
                    typeof(EnemyAI).GetField("saveKey", Private).SetValue(ai, tempSoul.Id + "_" + name);
                    ai.enabled = false;
                    clones.Add(copy);
                    return ai;
                }
                EnemyAI foe = Clone("Sweep Foe");

                // The longest straight, open run from the depth's entry: the aisle a player would fight in.
                Vector3 start = depth.Entry.position + Vector3.up * 0.05f;
                Vector3 lane = depth.Entry.forward;
                float best = 0f;
                for (int i = 0; i < 16; i++)
                {
                    Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                    float free = Physics.SphereCast(start + Vector3.up * 1f, 0.5f, d, out RaycastHit hit, 14f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 14f;
                    if (free > best) { best = free; lane = d; }
                }
                check(best >= 8.5f, $"The first depth offers an open aisle for casting ({best:F1} m)");

                void Place(EnemyAI who, float distance)
                {
                    var body = who.GetComponent<CharacterController>();
                    Vector3 at = start + lane * distance;
                    if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out RaycastHit floor, 6f, ~0, QueryTriggerInteraction.Ignore)) at = floor.point;
                    body.enabled = false;
                    who.transform.SetPositionAndRotation(at + Vector3.up * 0.06f, Quaternion.LookRotation(-lane));
                    body.enabled = true;
                }
                void Aim(Vector3 at)
                {
                    player.Teleport(start, Quaternion.LookRotation(lane).eulerAngles.y, 0f);
                    Physics.SyncTransforms();
                    Vector3 look = at - eye.transform.position;
                    float pitch = -Mathf.Asin(look.normalized.y) * Mathf.Rad2Deg;
                    player.Teleport(start, Quaternion.LookRotation(new Vector3(look.x, 0f, look.z)).eulerAngles.y, pitch);
                    Physics.SyncTransforms();
                }
                ProgressRecord Record(AbilityDefinition a) => new ProgressRecord
                {
                    SkillSchemaVersion = PlayerProgression.SkillSchema, FocusBonus = 1000, Food = 0,
                    Intellect = a.IsRecipe ? a.IntellectRequired : 10, NecromancerPath = a.Path,
                    Skills = new List<SkillProgressRecord> { new SkillProgressRecord { Id = a.Id, Tier = 1, Stock = a.IsRecipe ? 3 : 0 } },
                    EquippedSkillIds = new[] { a.Id, string.Empty },
                    Passive = new PassiveProgressRecord { DescentId = session.Descents }
                };

                int total = 0, working = 0;
                var broken = new List<string>();
                for (int classId = 0; classId < 6; classId++)
                {
                    session.ChooseClass((CharacterClass)classId);
                    foreach (AbilityDefinition a in AbilityBook.All(session.Class))
                    {
                        bool cast = false, effect = false; string line = null; int attempts = 0;
                        // A miss is the workbook accuracy, not a broken skill: a skill below 100% gets three honest tries.
                        for (int attempt = 0; attempt < 3 && !(cast && effect); attempt++)
                        {
                        attempts++;
                        abilities.ApplyClass(session.Class);
                        growth.Restore(Record(a));
                        combat.SetArmed(true);
                        status.Clear();
                        health.SetMax(PlayerMax, true); health.SetCurrent(PlayerStart);
                        abilities.SetFocus(500f);
                        var foeHealth = foe.Health;
                        foeHealth.SetMax(TargetHealth, true);
                        foe.GetComponent<StatusEffects>().Clear();
                        Place(foe, Distance(a));
                        EnemyAI corpse = null;
                        if (a.Id == "corpseexplosion" || a.Id == "n_corpseburst")
                        {
                            corpse = Clone("Sweep Corpse " + classId);
                            Place(corpse, 3.6f);
                            Place(foe, 5f);
                            corpse.Health.ApplyDamage(new DamageInfo(corpse.Health.Current + 1f, corpse.transform.position, Vector3.zero, null, true));
                        }
                        Aim(foe.transform.position + Vector3.up * 1f);
                        if (a.Tier(1).CleansePoison) status.ApplyTurnPoison(1f, 5, foe.gameObject);
                        Random.InitState(7 + total * 3 + attempt);
                        yield return 0.05f;
                        float focusBefore = abilities.Focus, shieldBefore = status.ShieldPoints, bonusBefore = status.AttackBonus;
                        int foodBefore = growth.Food;
                        input.SetTouchButton(PlayerButton.Ability1, true);
                        input.SetTouchButton(PlayerButton.Ability1, false);
                        yield return a.Delivery == AbilityDelivery.Self ? 1.1f : 0.9f + Mathf.Max(1, a.Tier(1).Hits) * 0.1f;
                        cast = abilities.CooldownFraction(0) > 0f;
                        var condition = foe.GetComponent<StatusEffects>();
                        float dealt = TargetHealth - foeHealth.Current, healed = health.Current - PlayerStart;
                        bool conditioned = condition.Poisoned || condition.Stunned || condition.Weakened || condition.Charmed;
                        bool selfGain = status.ShieldPoints > shieldBefore + 0.01f || status.AttackBonus > bonusBefore + 0.01f || healed > 0.01f ||
                            abilities.Focus > focusBefore + 0.01f || growth.Food > foodBefore || (a.Tier(1).CleansePoison && !status.Poisoned);
                        effect = a.Delivery == AbilityDelivery.Self ? selfGain : (dealt > 0.01f || conditioned) && (a.Tier(1).Lifesteal <= 0f || healed > 0.01f);
                        string native = AbilityBook.IsNative(session.Class, a) ? "native" : "foreign";
                        line = $"{(CharacterClass)classId}/{a.Id} ({a.Delivery}, {native}, {Distance(a):F1} m): cast {(cast ? "yes" : "no")}, " +
                            $"foe -{dealt:F1}{(conditioned ? " +condition" : "")}, caster {(healed >= 0 ? "+" : "")}{healed:F1} hp, shield {status.ShieldPoints:F1}, bonus {status.AttackBonus:F1}";
                        if (corpse != null) corpse.gameObject.SetActive(false);
                        if (a.Tier(1).Accuracy >= 1f) break;
                        }
                        string focusNote = a.Cost <= session.Class.MaxFocus ? "" : $", needs {a.Cost} focus over the class start of {session.Class.MaxFocus}";
                        line += (attempts > 1 ? $", {attempts} tries" : "") + focusNote;
                        Table.Add((effect && cast ? "WORKS  " : "BROKEN ") + line);
                        total++;
                        if (effect && cast) working++; else broken.Add($"{(CharacterClass)classId}/{a.Id}");
                        check(cast && effect, "Skill through the touch Q button at a real depth foe: " + line);
                    }
                }
                check(broken.Count == 0, $"Every equippable skill of every class has a real effect ({working}/{total}; broken: {string.Join(", ", broken)})");

                // Life drain through the keyboard and the gamepad, on either slot.
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                keyboard = InputSystem.AddDevice<Keyboard>("Sweep Keyboard");
                pad = InputSystem.AddDevice<Gamepad>("Sweep Gamepad");
                session.ChooseClass(CharacterClass.Witch);
                AbilityDefinition drain = AbilityBook.Find("drain");
                foreach (string device in new[] { "keyboard Q", "gamepad right shoulder", "keyboard R", "touch R" })
                {
                    abilities.ApplyClass(session.Class);
                    ProgressRecord record = Record(drain);
                    bool second = device.EndsWith("R");
                    if (second) record.EquippedSkillIds = new[] { string.Empty, drain.Id };
                    growth.Restore(record);
                    combat.SetArmed(true);
                    status.Clear();
                    health.SetMax(PlayerMax, true); health.SetCurrent(PlayerStart);
                    abilities.SetFocus(500f);
                    foe.Health.SetMax(TargetHealth, true);
                    Place(foe, 7f);
                    Aim(foe.transform.position + Vector3.up * 1f);
                    yield return 0.1f;
                    if (device.StartsWith("keyboard")) InputSystem.QueueStateEvent(keyboard, new KeyboardState(second ? Key.R : Key.Q));
                    else if (device.StartsWith("gamepad")) InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                    else { input.SetTouchButton(PlayerButton.Ability2, true); input.SetTouchButton(PlayerButton.Ability2, false); }
                    yield return 0.1f;
                    if (device.StartsWith("keyboard")) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    else if (device.StartsWith("gamepad")) InputSystem.QueueStateEvent(pad, new GamepadState());
                    yield return 0.8f;
                    float dealt = TargetHealth - foe.Health.Current, healed = health.Current - PlayerStart;
                    check(dealt > 0f && healed > 0f && Mathf.Abs(dealt - healed) < 0.05f,
                        $"Life drain from the {device} at a depth foe 7 m away hurts it ({dealt:F1}) and heals the witch by as much ({healed:F1})");
                }

                // As a player meets it: a new witch with her own focus and health, and a live foe whose AI is awake.
                abilities.ApplyClass(session.Class);
                growth.Restore(null);
                bool equipped = growth.TryEquipAbility(0, "drain");
                combat.SetArmed(true);
                status.Clear();
                health.SetCurrent(health.Max * 0.4f);
                abilities.SetFocus(abilities.MaxFocus);
                float liveMax = source.Health.Max;
                source.Health.SetMax(liveMax, true);
                Place(source, 6f);
                source.enabled = true;
                Aim(source.transform.position + Vector3.up * 1f);
                float drained = 0f, gained = 0f, lastHealth = health.Current;
                void Dealt(DamageInfo info, float amount) { if (info.Source == player.gameObject) drained += amount; }
                void Changed(float current, float max) { if (current > lastHealth) gained += current - lastHealth; lastHealth = current; }
                source.Health.Damaged += Dealt;
                health.Changed += Changed;
                input.SetTouchButton(PlayerButton.Ability1, true); input.SetTouchButton(PlayerButton.Ability1, false);
                yield return 0.6f;
                source.Health.Damaged -= Dealt;
                health.Changed -= Changed;
                source.enabled = false;
                check(equipped && drained > 0f && gained >= drained - 0.05f,
                    $"A new witch's own focus pays for Life Drain at a live foe 6 m away: it loses {drained:F1}, she gains {gained:F1} " +
                    $"({session.Class.MaxFocus} focus, drain costs {drain.Cost})");
                source.Health.SetMax(liveMax, true);
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (pad != null) InputSystem.RemoveDevice(pad);
                settings.editorInputBehaviorInPlayMode = focusBehaviour;
                settings.backgroundBehavior = backgroundBehaviour;
                abilities.ApplyClass(session.Class);
                foreach (GameObject clone in clones) if (clone != null) Object.DestroyImmediate(clone);
                foreach (Projectile projectile in Object.FindObjectsByType<Projectile>()) Object.Destroy(projectile.gameObject);
                keeper.Load(original);
                status.Clear();
                health.SetCurrent(health.Max);
                SaveStore.Delete(tempSoul.Id);
                MetaRecord meta = keeper.Meta;
                meta.Souls = originalMeta.Souls; meta.Descents = originalMeta.Descents; meta.Deepest = originalMeta.Deepest;
                meta.Codex = originalMeta.Codex; meta.UnlockedClasses = originalMeta.UnlockedClasses;
                meta.SkillMigrationRefunds = originalMeta.SkillMigrationRefunds;
                SaveStore.SaveMeta(meta);
                for (int i = guards.Length - 1; i >= 0; i--) if (guards[i] != null) guards[i].enabled = oldEnabled[i];
            }
        }
    }
}
