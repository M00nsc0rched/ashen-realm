using System;
using System.Collections.Generic;
using System.Linq;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>Controlled real creature bodies isolate skill targeting and persistence from enemy tactics.</summary>
    internal static class AbilityPlaytestChecks
    {
        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var abilities = controller.GetComponent<PlayerAbilities>();
            var growth = controller.GetComponent<PlayerProgression>();
            var playerHealth = controller.GetComponent<Health>();
            var playerStatus = controller.GetComponent<StatusEffects>();
            var session = GameSession.Instance;
            var keeper = SoulKeeper.Instance;
            zones.Place(ZoneNumbers.FirstDepth, Vector3.zero, 0f, false);
            yield return 0.2f;

            EnemyAI source = Object.FindObjectsByType<EnemyAI>().FirstOrDefault(e => e.Definition.Rank == EnemyRank.Common && e.Health.IsAlive);
            if (source == null) { check(false, "Ability checks need a common creature to clone"); yield break; }
            var targets = new List<Health>();
            for (int i = 0; i < 3; i++)
            {
                GameObject body = Object.Instantiate(source.gameObject);
                body.name = "Ability Test Body " + i;
                body.transform.SetParent(zones.Current.transform, true);
                var ai = body.GetComponent<EnemyAI>();
                SceneWiring.Set(ai, "saveKey", "ability_test_body_" + i);
                ai.enabled = false;
                Health h = body.GetComponent<Health>();
                h.SetMax(10000f, true);
                targets.Add(h);
            }
            Vector3 anchor = zones.Current.Entry.position + Vector3.forward * 2f;
            void ResetTargets()
            {
                controller.Teleport(anchor, 0f, 0f);
                for (int i = 0; i < targets.Count; i++)
                {
                    targets[i].transform.position = anchor + new Vector3((i - 1) * 1.1f, 0f, 2.3f + (i == 1 ? 0f : 0.6f));
                    targets[i].SetMax(10000f, true);
                    targets[i].GetComponent<StatusEffects>().Clear();
                }
                Physics.SyncTransforms();
            }

            session.ChooseClass(CharacterClass.Knight);
            growth.Restore(new ProgressRecord { AbilityOneTier = 2, AbilityTwoTier = 3 });
            check(growth.KnownAbilityTier(0) == 2 && growth.KnownAbilityTier(1) == 3 && growth.KnownAbilityTier(2) == 1 &&
                growth.EquippedAbilityIndex(0) == 0 && growth.EquippedAbilityIndex(1) == 1, "Old two-skill saves migrate without losing tiers");
            growth.TryEquipAbility(0, 2);
            growth.TryEquipAbility(1, 3);
            ProgressRecord persisted = JsonUtility.FromJson<ProgressRecord>(JsonUtility.ToJson(growth.Capture()));
            growth.Restore(persisted);
            check(growth.EquippedAbilityIndex(0) == 2 && growth.EquippedAbilityIndex(1) == 3 && growth.KnownAbilityTier(1) == 3,
                "All four tiers and both equipped skill indices survive a save-format roundtrip");
            keeper.AddSouls(200);
            int soulsBefore = keeper.Souls;
            int price = growth.KnownAbilityUpgradeCost(2);
            check(growth.TryUpgradeKnownAbility(2) && growth.KnownAbilityTier(2) == 2 && keeper.Souls == soulsBefore - price,
                "A newly added skill buys its own tier at the displayed Soul cost");

            bool savedLoadout = keeper.Save();
            SoulRecord saved = keeper.LatestSoul();
            check(savedLoadout && saved?.Progress?.EquippedAbilities != null &&
                saved.Progress.EquippedAbilities.SequenceEqual(new[] { 2, 3 }),
                "SoulKeeper saves the two newly equipped skill indices through SaveStore");
            if (savedLoadout && saved != null)
            {
                session.ChooseClass(CharacterClass.Witch);
                bool loaded = false;
                void OnLoaded() => loaded = true;
                keeper.Loaded += OnLoaded;
                try { keeper.Load(SaveStore.Find(saved.Id)); }
                finally { keeper.Loaded -= OnLoaded; }
                check(loaded && session.Class.Id == CharacterClass.Knight &&
                    growth.EquippedAbilityIndex(0) == 2 && growth.EquippedAbilityIndex(1) == 3 &&
                    Enumerable.Range(0, AbilityBook.KnownCount).Select(growth.KnownAbilityTier).SequenceEqual(new[] { 2, 3, 2, 1 }) &&
                    abilities.ActiveAbility(0).Effect == AbilityEffect.Whirlwind && abilities.ActiveAbility(1).Effect == AbilityEffect.IronSkin,
                    "SoulKeeper reloads four bought tiers and Q/R effects after a live class reset");
            }

            ResetTargets();
            abilities.SetFocus(abilities.MaxFocus);
            playerStatus.Clear();
            check(abilities.TryUse(0), "The equipped new skill can be cast");
            growth.TryEquipAbility(0, 0);
            growth.TryEquipAbility(0, 2);
            float focusBefore = abilities.Focus;
            check(abilities.CooldownFraction(0) > 0f && !abilities.TryUse(0) && Mathf.Approximately(abilities.Focus, focusBefore),
                "Swapping skills cannot erase a skill's cooldown or spend focus on a blocked cast");

            for (int classId = 0; classId < Catalog.Classes.Length; classId++)
            for (int known = 2; known < AbilityBook.KnownCount; known++)
            {
                session.ChooseClass((CharacterClass)classId);
                growth.TryEquipAbility(0, known);
                ResetTargets();
                playerStatus.Clear();
                playerHealth.SetCurrent(playerHealth.Max * 0.5f);
                abilities.SetFocus(abilities.MaxFocus);
                var skill = abilities.ActiveAbility(0);
                float total = targets.Sum(h => h.Current);
                float hp = playerHealth.Current;
                check(abilities.TryUse(0) && abilities.CooldownFraction(0) > 0f, skill.Name + ": cast spends focus and starts its own cooldown");
                yield return 0.25f;
                bool support = skill.Effect == AbilityEffect.IronSkin || skill.Effect == AbilityEffect.Sanctuary;
                bool changed = support ? playerStatus.Warded || playerHealth.Current > hp : targets.Sum(h => h.Current) < total;
                check(changed, skill.Name + ": its actual combat or protection effect reaches a real body");
                if (skill.Effect == AbilityEffect.FrostLance || skill.Effect == AbilityEffect.FlashPowder || skill.Effect == AbilityEffect.BoneNova)
                    check(targets.Any(h => h.GetComponent<StatusEffects>().Stunned), skill.Name + ": it interrupts its target");
                if (skill.Effect == AbilityEffect.ChainLightning)
                    check(targets.Count(h => h.Current < 10000f) >= 2, "Chain lightning jumps to a second visible enemy");
            }

            session.ChooseClass(CharacterClass.Gravethief);
            growth.TryEquipAbility(0, 3);
            ResetTargets();
            playerStatus.Clear();
            abilities.SetFocus(abilities.MaxFocus);
            check(abilities.TryUse(0), "A persistent caltrop field can be placed");
            foreach (Health h in targets) h.GetComponent<StatusEffects>().Clear();
            zones.Place(ZoneNumbers.City, Vector3.zero, 0f, false);
            yield return 0.2f;
            zones.Place(ZoneNumbers.FirstDepth, Vector3.zero, 0f, false);
            float afterTravel = targets.Sum(h => h.Current);
            yield return 1.2f;
            check(Mathf.Approximately(afterTravel, targets.Sum(h => h.Current)), "Persistent skill fields stop when their owner leaves the zone");
            session.ChooseClass(CharacterClass.Knight);
            foreach (Health h in targets) Object.Destroy(h.gameObject);
            playerStatus.Clear();
            playerHealth.SetCurrent(playerHealth.Max);
            yield return 0.1f;
        }
    }
}
