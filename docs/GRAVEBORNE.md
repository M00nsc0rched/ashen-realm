# Graveborne in Ashen Realm

Graveborne is the user's turn-based top-down roguelike (source: `E:\Ashen Realm\graveborne`, not in this repository).
Ashen Realm takes its world, story, cast and bestiary, and keeps its own real-time first-person combat.

## How the two games map

| Graveborne | Ashen Realm |
|---|---|
| Hollowgate, the Rope and Lantern, the stair | Zone 1: walled city. Zone 2: the tavern, a walkable room; the stair is the iron-bound door at its far end |
| The Cold Forge, the Grey Chapel | Zones 3 and 4: walkable rooms off the square, with the goods in them and a shop panel at the counter |
| Gallows Market | The road in from the gate: two rows of empty stalls and the gallows, with Iron Mabb, Wick and the Widow |
| The Deep Dark, procedural floors | Zones 5-9: rooms and corridors from the same idea as `dungeon.js`, raised to 3D |
| Biomes: floor colours, enemy mix | `BiomeStyle` in `PrototypeSceneBuilder.Biomes()` |
| Legendary Guardians bar the stair | A keeper stands in the far hall; `ZonePortal` is barred by the flag `dead_<id>` |
| Guardian speeches by class and alignment | `EnemyDefinition.Dialogue`, shown by `StoryDirector.OnConfrontation` |
| Gloamlord on the last floor | Depth 5 throne; his death opens the class ending |
| Skill power and turns | Damage per hit and seconds; `EnemyMove`, `StatusEffects` |
| Enemy move weights | `EnemyAI.PickMove` rolls by weight among the moves the situation allows |
| SP | Focus, with a cooldown per ability because real time has no turns |
| Honor, warped perception | `GameSession.Honor`; honor below 0 shows `EncounterVariant.Warped` and a dark figure |
| The city's three moods (warm, wary, hostile) | Three painted faces per person, chosen by `Alignment` in `PortraitLibrary` |
| Scenery never blocks a route | Depth props have no colliders |

## What is in

- Four classes with their starting kit: Ashen Knight, Gravethief, Hollow Witch, Oathwarden.
- Hollowgate with Iron Mabb, Wick, the Waiting Widow and the Hollow Merchant at the market, and the Rope and Lantern as a room of its own with Aggie Voss, the Dark Wanderer and Sergeant Halloway.
- The Cold Forge with its smith and four weapons for gold, the Grey Chapel with the Choir's book-keeper, three relics for Souls, the offering and the blessing.
- Five depths (zones 5-9): Catacombs, Fungal Deep, Sunken Harbor, Ember Chasm, Whitemarrow.
- 13 common foes, 5 named elites, 4 Legendary Guardians (Morr, Mycel, Brine, Pyraxes), the Gloamlord.
- 8 honor encounters: well, beggar, cage, sporewife, ferryman, shrine, forge widow, bone choir.
- Gold, potions, weapon tempering, absolution; whispers; class endings.

Written for this port, in Graveborne's voice: the Gloamlord's speech (Graveborne gave him only a death line), the
class endings, and the service and interface strings.

## Not ported yet

Listed so nobody assumes they exist:

- The Necromancer and Alchemist classes, the other 76 skills, and skill tiers. The class choice shows both as sealed cards, the picture `CardLocked` with
  `Zárolva` on the button: they are earned classes in Graveborne, and their unlocks (Omen, the potion-maker's quest) are not ported either.
- The class passives. The cards print all six (`Catalog.Cards`), but no passive works in the game yet. Last Vigil's two
  turns will need a time limit, and Cutthroat's Luck needs critical hits and bleeding, which real-time combat does not have.
- The remaining 20 encounters, followers (Eliza, the Waif and the others).
- The Reckoning, the Sanctum, sets, hunger, limb targeting. Of the items, only the first stock of the forge and the chapel is in; the rest of Graveborne's table, armor, the forge's restocking and giving items up for Souls are not.
- Levels: the soul card's `Szint` is 1 + weapon level until levelling is ported.
- Biomes beyond the five above, their guardians and elites, the other four thrones.
- The Gilded Inquisition and the Bounty meter.
