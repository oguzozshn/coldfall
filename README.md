# COLDFALL — Design Documentation

Working title. A first-person survival game set in a rural North American valley through the
turn of the season: the systemic depth of Project Zomboid fused with the environmental
pressure and isolation of The Long Dark, rebuilt for a first-person camera.

- **Engine:** Unity 6 (URP)
- **Scope:** solo developer, vertical slice (one region, complete core loop)
- **Players:** single-player only
- **Setting:** rural North America — wooden houses, pickup trucks, a gas station, a diner, a trailer park
- **Climate:** rainy late autumn turning to winter around day 20; one map, two seasonal dressings
- **Perspective:** first person
- **Positioning:** a faithful fusion of its two references, not a gimmick. No artificial hook.

## Locked design decisions

These are settled and binding on every document. Changing one means revisiting the documents
it touches.

| Decision | Value |
|---|---|
| Death and saving | Permadeath, single save slot, overwritten on quit, deleted on death |
| Zombie bite | Certain infection, **curable** with a rare antifungal course: expensive, slow, and nearly all of the region's supply |
| Outbreak origin | Fungal. Dense corpse areas emit spore clouds (see the toxic-air axis) |
| Temperature vs zombies | **Not** a continuous driver. One freezing threshold only: below -15 degC or in a blizzard they slow and go partly deaf |
| Pressure axes | Temperature (full depth), wetness (full depth), disease (medium), toxic air (deferred, map space reserved) |
| Time structure | Season progression **and** a fixed infrastructure decay calendar |
| Melee | Heavy, committal, charge-and-release, grapple-based zombie attacks, shove as the primary defence |
| Progression | Learn-by-doing proficiency with a low ceiling. No skill trees, no perks |
| Art style | Stylized realism: correct proportions and materials, simplified detail, restricted palette. Readable and cold, not photoreal |
| Visual reference | The Long Dark is the primary reference, for silhouette readability, palette discipline, atmosphere as depth and light as emotion. Not for hand-painted texture, saturated skies or stylized lighting |
| Asset sourcing | Licensed packs (commercial-use licences only) for the world; made by hand for the zombie, the first-person arms, held items, trim sheets, decals and the named shelters |
| Zombie faces | Not an asset in the slice. The fungal growth layer, hair, hoods and shadow cover the face. No facial rig |
| Gore | Heavy but concrete: persistent blood decals, wounds, broken limbs, pre-authored break states. No procedural fracture |
| Zombie variants | One mesh, one skeleton, two blendshapes, three locomotion sets. Crawler is an animation set; Bloated is a blendshape |

## Document index

| # | Document | Covers |
|---|---|---|
| 01 | [Vision and Pillars](docs/01-vision-and-pillars.md) | Fusion thesis, what first person changes, pillars, scope guardrails |
| 02 | [Core Loop](docs/02-core-loop.md) | Session structure, the Warmth/Noise/Weight triangle, pacing, onboarding |
| 03 | [Survival Systems](docs/03-survival-systems.md) | Needs, the heat equation, clothing, wounds, the bite cure, illness, weather |
| 04 | [Zombies and Combat](docs/04-zombies-and-combat.md) | Perception, AI states, horde meta-simulation, melee, grapple, firearms |
| 05 | [World and Map](docs/05-world-and-map.md) | Hallow Valley, seven districts, landmarks, the decay calendar, streaming |
| 06 | [Items, Crafting, Progression](docs/06-items-crafting-progression.md) | Item model, inventory, recipes, proficiency, the antifungal economy |
| 07 | [Art Direction](docs/07-art-direction.md) | Autumn and winter palettes with hex values, fog, lighting, asset budgets |
| 08 | [Audio and UI](docs/08-audio-and-ui.md) | Audio as a mechanic, mix states, HUD-less interface, paper map, inventory UX |
| 09 | [Technical Architecture](docs/09-technical-architecture.md) | Tick model, hybrid Jobs/Burst, streaming, thermal volumes, saves, budgets |
| 10 | [Roadmap](docs/10-roadmap.md) | Milestones M0-M5 with hard exit criteria, and the risk register |

## Reading order for implementation

Read 01 and 02 to know what the game is. Read 09 before writing any code. Documents 03-06 are
the system specifications and are the build order inside each milestone. Documents 07-08 are
production standards. Document 10 gates every milestone and is the only document allowed to
say when something is finished.

## Tuning values

Every number in these documents is a **first-pass tuning target**, not a final value. All of
them live in `ScriptableObject` assets, never in code. If a number appears in a C# file, that
is a bug.
