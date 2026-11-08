# 09 — Technical Architecture

Unity 6, URP, C#. Single-player. One developer. Every decision below is weighted towards
**iteration speed and debuggability** over theoretical performance, with Jobs and Burst applied
only where the profiler demands it.

## 9.1 Tick model

Four rates, all driven from one authoritative clock. No gameplay system may use `Update()` for
simulation.

| Layer | Rate | Driver | Contents |
|---|---|---|---|
| **Physics** | 50 Hz fixed | `FixedUpdate` | Character controller, melee arc traces, ragdolls, projectiles |
| **Needs** | 1 Hz | `SimClock` | Calories, hydration, fatigue, core temperature, wetness integration |
| **Body** | 0.2 Hz (5 s) | `SimClock` | Wound progression, infection, illness, healing, frostbite |
| **World** | 1 per game-minute | `SimClock` | Spoilage, fuel burn, drying, weather, thermal diffusion, scent decay, horde meta-sim |
| Presentation | Per frame | `Update` | Animation, audio parameters, post-processing, UI. **Never simulation** |

### SimClock
```
gameMinutesPerRealSecond = timeScaleSetting / 60      // 20:1 default -> 0.333
```
`SimClock` accumulates game time, fires the three simulation events, and is the only source of
"what time is it". Pausing, sleeping (fast-forward), and time-scale changes all happen here, so
no system needs its own notion of time. Sleeping runs the world tick at up to 240x with the
needs tick coarsened to one call per simulated hour, which is why all rates in
[03](03-survival-systems.md) are expressed per game hour.

**Determinism requirement:** the three simulation ticks must produce identical results for
identical inputs, independent of frame rate. This is required for reliable testing today and for
the co-op hooks in 9.9 later. Simulation code therefore never reads `Time.deltaTime`,
`Random.value`, or transform state written during presentation.

## 9.2 Core architecture — hybrid, not pure ECS

**GameObjects and MonoBehaviours for everything the developer touches by hand.** Buildings,
props, doors, the player, individual zombie agents. The reason is prosaic and decisive: a solo
developer needs the Unity editor's inspector, scene view, and gizmos to author 40 interiors and
debug a grapple system. Pure DOTS would cost more in authoring tooling than it returns.

**Jobs plus Burst for the four systems that are genuinely data-parallel**, each implemented as a
plain C# struct job over a `NativeArray`, with no GameObject dependency:

| System | Why it needs Burst | Shape |
|---|---|---|
| **Horde meta-simulation** | ~40 nodes x migration edges, once per game-minute, plus up to 120 live agents' perception checks | `NativeArray<DistrictNode>`, parallel edge-flow job |
| **Thermal diffusion** | Every thermal volume exchanging heat through portals, once per game-minute | `NativeArray<ThermalVolume>` plus `NativeArray<Portal>`, Jacobi iteration, 4 passes |
| **Scent grid** | 4 m cells across 1.5 x 1.5 km = ~140,000 cells decaying and diffusing | `NativeArray<half>`, two-pass separable blur, every 10 game-minutes |
| **Zombie perception** | Hearing checks for every live agent against every active noise event | Parallel job producing an investigate-target list consumed by the agents' state machines |

Everything else — the needs integration, the body tick, inventory, crafting, items, save — is
ordinary managed C#. The needs tick runs for exactly one entity, so optimizing it would be
absurd.

### Noise events
A ring buffer of `NoiseEvent { float3 position; float loudness; NoiseType type; double gameTime; }`
with a 10-second retention. Producers write into it; the perception job reads it. **Nothing in
the game may make a zombie aware of the player except this buffer** (see
[04](04-zombies-and-combat.md), 4.4) — the absence of an aggro-share path is an architectural
invariant, enforced by keeping the zombie AI assembly unable to reference the player assembly.

## 9.3 Assembly layout

Assembly definitions enforce the dependency direction, which is the cheapest architectural
discipline available and the one most likely to save the project.

```
Coldfall.Data          (ScriptableObject definitions, pure data, no logic)
   ^
Coldfall.Sim           (SimClock, needs, temperature, wetness, body, items,
   ^                    crafting, noise buffer, scent, thermal, meta-sim)
   |                    NO UnityEngine rendering, NO scene references
Coldfall.World         (streaming, thermal volume components, doors, containers,
   ^                    NavMesh links, district nodes)
Coldfall.Agents        (zombie state machine, perception consumption)
   ^                    May reference Sim and World. MAY NOT reference Player
Coldfall.Player        (character controller, camera, melee, inventory UI bridge)
   ^
Coldfall.Presentation  (animation, audio bridge, post-processing, UI)
   ^
Coldfall.Tests         (edit-mode and play-mode tests)
```

`Coldfall.Sim` must compile and be testable without a scene. The heat equation in
[03](03-survival-systems.md), 3.3 is a pure function and is unit tested as one.

## 9.4 Streaming

- **Addressables.** Exterior terrain and shells in 128 m scene chunks, loaded in a 3 x 3 ring
  (384 m across) around the player, with a 1-chunk unload hysteresis to prevent thrash at
  boundaries.
- **Interiors as separate scenes**, triggered by a proximity volume at the building shell rather
  than by opening the door, so entry never hitches. Interiors are authored in place at their real
  world coordinates — no teleport trick — so windows show the real exterior and the sun angle is
  correct.
- **A persistent bootstrap scene** holds `SimClock`, the save service, the meta-sim, the audio
  banks, and the player. It is never unloaded.
- **Loading budget:** a chunk load must complete inside 150 ms of main-thread time spread across
  frames; anything heavier is authored wrong.

### District nodes
About 40 authored `DistrictNode` components, each with a bounds volume, edges to neighbours, a
list of spawn anchors, an ambient temperature offset, and an attraction score. They exist in the
bootstrap scene as lightweight data, independent of whether their chunk is loaded, which is what
lets the horde meta-sim run across the whole valley at all times.

## 9.5 Thermal volumes and portals

```
ThermalVolume  { Bounds bounds; float insulation; float windExposure;
                 float currentTemp; float wetnessShield; int[] portalIds; }
Portal         { int volumeA; int volumeB; PortalState state; float area; }
PortalState    = Sealed | Closed | Boarded | Open | Broken
```

- Authored by hand per room as box volumes, plus one implicit "outdoors" volume per district node
  that takes its temperature from the weather system and the node's altitude offset.
- Doors and windows each own a portal; their state is the same state the gameplay reads, so
  boarding a window in [04](04-zombies-and-combat.md), 4.9 raises the room's temperature in
  [03](03-survival-systems.md), 3.8 with no extra code.
- Diffusion runs in the Burst job in 9.2: four Jacobi passes per world tick, which is
  comfortably stable at one-minute steps and visibly convincing — a room with an open door to a
  blizzard cools in a believable handful of minutes.
- **The same portal graph feeds audio occlusion** (see [08](08-audio-and-ui.md), 8.2). One
  authored structure, three systems reading it.

## 9.6 Data authoring

All tuning lives in `ScriptableObject` assets under `Assets/Data/`. **A tuning number in a `.cs`
file is a bug.**

| Asset type | Holds |
|---|---|
| `ItemDefinition` | Per [06](06-items-crafting-progression.md), 6.1 |
| `RecipeDefinition` | Inputs, required tags, output, duration, proficiency, knowledge source |
| `ClothingDefinition` | Insulation, windproof, waterproof, bulk, coverage, bite resistance |
| `WoundDefinition` | Bleed rate, infection chance, healing curve, treatment requirements |
| `IllnessDefinition` | Trigger conditions, stage timings, effects, treatment |
| `WeatherStateDefinition` | Ambient day/night, wind, visibility, fog density and colour, wetness source, hearing modifier |
| `SeasonDefinition` | Which weather states, their transition weights, the art dressing parameters |
| `WorldEventDefinition` | The fixed decay calendar in [05](05-world-and-map.md), 5.5 |
| `ZombieVariantDefinition` | Share, speed, perception multipliers, special behaviour |
| `BalanceProfile` | The global constants: `C_body`, `T_skin_target`, drain rates, encumbrance bands, the freezing threshold |

### Validation
An editor validation pass, run on build and on demand, that fails loudly on: an item id that is
duplicated or missing, a recipe referencing a nonexistent item, a thermal volume with no portals,
a district node with no edges or no spawn anchors, an enterable building with fewer than two
entries or no chokepoint (the rules in [05](05-world-and-map.md), 5.4), and any antifungal dose
count other than four. The last one is a one-line check that protects the design's most
load-bearing number.

## 9.7 Save system

### Format
Binary, versioned, written through a schema that tolerates added fields. Three separate blocks,
and the separation is the important part:

| Block | Contents | Lifetime |
|---|---|---|
| `WorldState` | District populations and attraction, container contents and searched flags, door and window portal states, barricades, corpses, placed items, the world clock, weather history, generator state, which antifungal doses have been taken | The world |
| `CharacterState` | Needs, core temperature, per-garment wetness, body parts and wounds, illnesses, inventory, proficiencies, known recipes, the bite clock | The character |
| `PlayerMeta` | Settings, key bindings, accessibility options, run history (days survived, cause of death) | The installation |

**Permadeath** deletes `WorldState` and `CharacterState` and appends one line to the run history
in `PlayerMeta`. Saving happens on quit and overwrites; there is one slot.

The block separation costs nothing now and is what makes "a new character in the same world"
possible later without a format migration (see [02](02-core-loop.md), 2.7).

### Volume
The world holds on the order of 40,000 item instances across its containers. The save must stay
under **8 MB** and write in under **400 ms**, achieved by storing item instances as
`(definitionId: ushort, condition: byte, wetness: byte, flags: byte, count: byte)` and by writing
only containers whose searched flag or contents have changed from the authored baseline.

### Autosave
On entering or leaving a building, on sleeping, and every 10 game-minutes, to a rolling temporary
file that is promoted on clean quit. A crash loses at most ten game-minutes. **Crash recovery
never resurrects a dead character** — if the character died, the save is gone, and the recovery
file is checked against that first.

## 9.8 Performance budgets

Target hardware: GTX 1660 / RX 580, 4-core CPU, 16 GB RAM, 1920 x 1080.

| Budget | Target | Hard limit |
|---|---|---|
| Frame time | 13 ms | 16.6 ms |
| Main-thread gameplay | 4 ms | 6 ms |
| Live zombie agents | 60 typical | 120 |
| Agent AI cost (all agents, per frame) | 1.2 ms | 2 ms |
| World tick cost (one game-minute) | 3 ms | 8 ms, spread across frames |
| Draw calls | 1,800 | 2,400 |
| Visible triangles | 3.5 M | 5 M |
| Resident textures | 2.5 GB | 3 GB |
| Managed allocation per frame in steady state | **0 bytes** | 0 bytes |

The zero-allocation rule applies to the simulation and agent loops only; UI and streaming may
allocate. It is enforced by a play-mode test that fails the build if the GC allocates during 60
seconds of standing still in the town centre.

### LOD and tick budgeting for agents
| Distance | AI tick | Animation |
|---|---|---|
| 0-20 m | Every frame | Full rig |
| 20-45 m | Every 3rd frame | Full rig, reduced bones |
| 45-90 m | Every 10th frame | LOD2, no IK |
| Beyond 90 m or out of the streaming ring | Meta-sim count only | None |

## 9.9 Hooks left open for co-op

Co-op is explicitly cut (see [01](01-vision-and-pillars.md), 1.8). These four disciplines keep it
possible without costing anything now, and each is also good single-player practice:

1. **Deterministic, fixed-step simulation** with no `Time.deltaTime` in simulation code (9.1).
2. **No static mutable state and no singletons holding player data.** Services are injected; the
   player is an entity, not a global. `Coldfall.Sim` already cannot reference the player assembly.
3. **Input as commands.** The character controller consumes an `InputCommand` struct produced by
   a provider, never `Input.GetKey` directly. A network layer, a replay system, and an automated
   test all become the same substitution.
4. **State changes as events.** Door opened, container searched, noise emitted, item transferred —
   each is an explicit event applied to state, not an inline mutation. This is also what makes the
   delta-based save in 9.7 cheap.

Nothing else is done for co-op. No serialization of transforms, no interest management, no lobby
code, no prediction. Those are a project of their own.

## 9.10 Testing

| Layer | What |
|---|---|
| Unit (edit mode) | The heat equation at known inputs; wind chill; wetness integration and drying; spoilage; encumbrance bands; the bite clock including every failure branch of the antifungal course; recipe resolution; the save round-trip |
| Burst job tests | Thermal diffusion stability over 10,000 ticks; scent diffusion conservation; meta-sim population conservation (**no zombie is ever created or destroyed by migration**) |
| Play mode | A zombie hears a noise at a known distance through a known number of walls and investigates the right place; a grapple break-out window measures correctly at both freezing states; a chunk load stays within budget; the zero-allocation standing test |
| Authoring validation | 9.6's validation pass, run on every build |
| Soak | A 40-game-day headless run with a scripted idle player, asserting that the decay calendar fires on the right days, the save stays under 8 MB, and memory does not grow |

The soak test is the single most valuable test in the list, because the design's whole thesis is
a world that degrades correctly over 40 days, and that is not something a developer can
playtest repeatedly by hand.

## 9.11 Tooling and repository

- **Unity 6 LTS**, URP, pinned version. One version per project, never upgraded mid-milestone.
- **Git with LFS** for binary assets. `.meta` files committed, Smart Merge configured for scenes
  and prefabs.
- **Scene discipline:** one scene per chunk, one per interior, one bootstrap. Scenes are never
  merged by hand; conflicts are resolved by re-authoring the smaller change.
- **Editor tools worth building early**, in priority order: a thermal volume visualizer (coloured
  by temperature, in the scene view), a noise event visualizer (expanding spheres with the
  through-wall attenuation drawn), a district node graph editor, a time-and-weather scrubber, and
  a one-key "spawn me at day N with loadout X" harness. These five tools will pay for themselves
  inside a month; the thermal visualizer will pay for itself inside a week.
