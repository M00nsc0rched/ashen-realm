# Graveborne in Ashen Realm

Graveborne is the user's turn-based top-down roguelike (source: `E:\Ashen Realm\graveborne`, not in this repository).
Ashen Realm takes its world, story, cast and bestiary, and keeps its own real-time first-person combat.

## How the two games map

| Graveborne | Ashen Realm |
|---|---|
| Hollowgate, the Rope and Lantern, the stair | Zone 1: walled city, the stair is the tavern's front door |
| The Deep Dark, procedural floors | Zones 2-6: rooms and corridors from the same idea as `dungeon.js`, raised to 3D |
| Biomes: floor colours, enemy mix | `BiomeStyle` in `PrototypeSceneBuilder.Biomes()` |
| Legendary Guardians bar the stair | A keeper stands in the far hall; `ZonePortal` is barred by the flag `dead_<id>` |
| Guardian speeches by class and alignment | `EnemyDefinition.Dialogue`, shown by `StoryDirector.OnConfrontation` |
| Gloamlord on the last floor | Depth 5 throne; his death opens the class ending |
| Skill power and turns | Damage per hit and seconds; `EnemyMove`, `StatusEffects` |
| Enemy move weights | `EnemyAI.PickMove` rolls by weight among the moves the situation allows |
| SP | Focus, with a cooldown per ability because real time has no turns |
| Honor, warped perception | `GameSession.Honor`; honor below 0 shows `EncounterVariant.Warped` and a dark figure |
| Scenery never blocks a route | Depth props have no colliders |

## What is in

- Four classes with their starting kit: Ashen Knight, Gravethief, Hollow Witch, Oathwarden.
- Hollowgate with Aggie Voss, the Dark Wanderer, Sergeant Halloway, Iron Mabb and the Hollow Merchant.
- Five depths: Catacombs, Fungal Deep, Sunken Harbor, Ember Chasm, Whitemarrow.
- 13 common foes, 5 named elites, 4 Legendary Guardians (Morr, Mycel, Brine, Pyraxes), the Gloamlord.
- 8 honor encounters: well, beggar, cage, sporewife, ferryman, shrine, forge widow, bone choir.
- Gold, potions, weapon tempering, absolution; whispers; class endings.

Written for this port, in Graveborne's voice: the Gloamlord's speech (Graveborne gave him only a death line), the
class endings, and the service and interface strings.

## Not ported yet

Listed so nobody assumes they exist:

- The Necromancer and Alchemist classes, the other 76 skills, and skill tiers.
- The remaining 20 encounters, Codex, followers (Eliza, the Waif and the others).
- Souls, the Reckoning, the Sanctum, items, sets, hunger, limb targeting.
- Biomes beyond the five above, their guardians and elites, the other four thrones.
- The Gilded Inquisition and the Bounty meter.
- Save and resume: a descent lasts one page session.
