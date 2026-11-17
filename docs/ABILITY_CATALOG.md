# Workbook ability catalog

Source: `KATALOGUS.xlsx`, supplied outside the repository. It identifies its own origin as a 2026-11-09 extraction from Graveborne's `js/data.js`, `js/city-data.js`, and Hungarian localization tables (`Áttekintés!A2`). SHA256: `3d9c8790f51ff96c151af4f73c047721568922c2907f3b4380a8bfdf77813025`.

`WorkbookAbilityData.cs` contains all 98 rows of `Képességek!A2:O99` and all 13 rows of `Alkimista receptek!A2:G14`. Every record carries its original sheet/range in `Source`; every tier retains the original summary with font-safe punctuation. Passive names/descriptions come from `Kasztok!M2:O7`. The original workbook is never written. The other sheets were inspected for context: 48 enemies, 208 enemy actions, 11 biomes, 7 city NPCs, 4 followers, 29 encounters. Enemy data is outside this import's scope.

## Regeneration

Run `python tools/import-ability-catalog.py path/to/KATALOGUS.xlsx` from any directory. It requires only Python's standard library, reads XLSX XML directly, and regenerates the typed table plus the `AbilityEffect` declaration in `StoryTypes.cs`. It also accepts the read-only `ability-catalog.json` intermediate. No Excel or Unity process is needed. The runtime never depends on the original workbook or ignored `Logs` files.

The first 24 `AbilityEffect` numeric values remain unchanged. Each of the 111 definitions has a distinct effect and stable workbook ID. `BoneSpear` remains the Necromancer's `n_bonespear`; the Witch's different `bonespear` is `WitchBoneSpear`. Old four-entry saves map through the explicit `LegacyId` table. Class-local ordering is common/native workbook rows followed by non-signature skills from other classes. The Alchemist sees only the 13 recipes; follower Charm is excluded from every player catalog. Cross-class skills have 40% strength and remain at tier I (`Áttekintés!B40`).

## Values preserved from the workbook

- All SP values are already effective costs (`Áttekintés!B39`); the importer never adds another seven SP.
- Power is added to ATK or MAG (`Áttekintés!B32`). Chances and ratios are stored as fractions. Multihit powers are per hit, not totals.
- Shield multipliers use DEF (`Áttekintés!B33`). Weaken effects reduce ATK even when the skill's name is Amplify Damage or Lower Resist.
- The tier tables use one generic poison effect for several bleed/fire/ice descriptions. Those numeric effects remain poison.
- Sanctuary III really drops to 8 regeneration per turn after II's 15, while gaining DEFx5. These supplied numbers are preserved.
- Charm has only one supplied tier: 50% ATK/MAG theft (5% for bosses), three turns. Its tier accessor clamps to I.
- Workbook costs are kept, but a cast never costs more than the caster's maximum focus (`PlayerAbilities.CastCost`): the starting Knight's six focus pays for Guard (8) and Rally (9) with a full bar. Witch maximum focus is corrected to the supplied 25 so its starting 20-SP Life Drain is reachable. Other class maximum focus values remain 6/8/9/30/8.

## Explicit real-time adaptation

These are implementation balance choices where the workbook gives no real-time rules:

- One turn is two seconds. Unspecified shield duration is three turns; a successful stun lasts one turn. Critical damage multiplier is two.
- The literal shield `"def"` in Prayer I-III and Sanctuary I-II means DEFx1. Guard uses its explicit DEF multiplier; its unsized honor contribution is omitted.
- Every ability has a nonzero cooldown. Delivery defaults in seconds: Melee/Bolt 8, Fan/Charge/Leap/Target 10, Chain/Nova/Self/Orb 12, Field 14. SP costs of nine or more add two seconds, capped at 16. Ranges/radii and delivery assignments are explicit in the importer and generated table.
- Missing base accuracy means 100%; missing hit count means one. Specified accuracy and critical/stun chances remain exact.
- Typographic dashes, minus signs, multiplication/comparison symbols, curly quotes and ellipses are normalized to ASCII for the existing HUD font. Hungarian letters remain intact.

## Recipes: supplied data and chosen balance

The workbook supplies recipe names, descriptions, plant cost and INT thresholds; only Bitter Tonic has numeric effects. It remains exactly 10 damage and 4 healing at every rank. Recipes cost plants and zero SP. The runtime selects recipe rank `clamp(1 + floor((INT - requiredINT) / 10), 1, 3)` rather than learning/upgrading them like spells.

The following are explicit new balance values for ranks I/II/III, not claims about the source workbook. Durations are turns; periodic values are per turn. The generic poison channel also represents fire/acid effects.

| Recipe ID | Chosen effects by rank |
| --- | --- |
| `starter_tonic` | Power 10 and heal 4 at every rank (source) |
| `flask_vitriol` | Power 16/20/24; poison 4/6/8 for 3 turns; ATK reduction 3/4/5 for 3 turns |
| `tonic_ward` | Shield DEFx2/3/4 |
| `nourishing_stew` | Heal 12/18/24; regeneration 2/3/4 for 3 turns |
| `flask_pyre` | Power 22/28/34; poison 5/7/9 for 3 turns |
| `vial_miasma` | Power 4/6/8; poison 6/8/10 for 4 turns; ATK reduction 4/6/8 for 3 turns |
| `brew_vigor` | ATK +6/9/12 for 3 turns; shield DEFx2/3/4 |
| `elixir_mend` | Heal 20/28/36; regeneration 4/6/8 for 3 turns |
| `vial_torpor` | Power 2/4/6; guaranteed one-turn stun; ATK reduction 3/5/7 for 2 turns |
| `vial_solvent` | Power 8/10/12; ATK reduction 10/14/18 for 3 turns |
| `flask_shatter` | 4/5/6 hits of power 8/10/12; 85% accuracy; 15% critical chance |
| `draught_fervor` | ATK +10/14/18 and regeneration 2/3/4 for 3 turns |
| `cordial_marrow` | Heal 28/36/44; regeneration 5/7/9 for 4 turns |

Passive descriptions specify intended behavior, but several magnitudes are absent: Knight opening shield/fury, Rogue bleed/critical coin result values, the Witch's eligible random-stat pool, and exact death-countdown timing. Those runtime choices must remain clearly distinguished from workbook values.
