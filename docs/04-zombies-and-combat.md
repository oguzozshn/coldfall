# 04 — Zombies and Combat

## 4.1 Design contract for the dead

1. **They are an environmental hazard with legs, not an enemy roster.** One base archetype and
   four variants. No spectacle. Difficulty comes from count, position, and information denial.
2. **They always announce themselves before contact.** Minimum two seconds of audible or visible
   warning in any engagement the player could reasonably have perceived. Violating this is a
   bug, not a difficulty setting.
3. **They are deadlier than the player in a fair fight.** Two walkers in an open room is a losing
   proposition. The player's tools are doorways, geometry, noise discipline, and route choice.
4. **They never spawn in the player's view, and never inside a volume the player has cleared and
   sealed.** Repopulation happens at the district level through migration; see 4.5.
5. **Their behaviour does not change with ordinary temperature.** There is exactly one weather
   exception, in 4.2, and it is a hard threshold rather than a gradient.

## 4.2 The freezing threshold — the only environmental modifier

Zombies behave identically in a heated room and in a 4 degC drizzle. There is **no** continuous
temperature-to-metabolism coupling: warming your shelter does not make the dead faster, and this
is a deliberate rejection of that mechanic.

There is one exception, which exists because winter arriving on day 20 should change something
about the enemy, not only about the player.

| Condition | State | Effect |
|---|---|---|
| Ambient above -15 degC, no blizzard | **Normal** | Move 3.2 m/s pursuing, hearing multiplier 1.0, reaction delay 0.4 s, full grapple strength |
| Ambient at or below -15 degC, **or** an active blizzard | **Stiffened** | Move 1.8 m/s pursuing, hearing multiplier 0.5, reaction delay 1.4 s, grapple strength 0.7x, knocked down more easily and slow to rise |

- The transition interpolates over **60 seconds** in either direction, so a horde following the
  player out of a building into a blizzard visibly loses its edge, and one following the player
  indoors out of a blizzard visibly regains it.
- "Indoors" does not by itself lift the Stiffened state: only ambient temperature above -15 degC
  does, which an unheated building in a -22 degC night does not provide. Heating a room is not
  required for that; outdoor temperature alone is above the threshold most of the time.
- **Blizzard overrides temperature.** A blizzard at -6 degC still stiffens them, because the
  stated reason is wind, driven snow, and sensory load, not a metabolic model.

### What this is for
It gives deep winter a genuine tactical character. After day 24, blizzards become the player's
cover: terrible visibility for them, but a horde that cannot hear and cannot run. Crossing the
open ground of Cross Street in a blizzard is a real strategy with a real price, and it is the
only place in the design where the weather helps.

## 4.3 Perception

Three independent senses, each a separate cheap check. No omniscience and no shared knowledge
except through sound.

### Hearing — the primary sense
A noise event is `(position, loudness, type, timestamp)`. Loudness is a radius in metres,
modulated by walls (each wall between source and listener removes 60% of the remaining radius),
by weather, and by the listener's hearing multiplier from 4.2.

| Weather | Hearing modifier |
|---|---|
| Clear, overcast | 1.0 |
| Drizzle | 0.85 |
| Rain | 0.7 |
| Downpour | 0.5 |
| River fog, freezing fog | 1.25 (sound carries unusually far) |
| Light or wet snow | 0.8 |
| Heavy snow | 0.6 |
| Blizzard | 0.35, and the Stiffened multiplier applies on top |

| Action | Base radius |
|---|---|
| Crouch-walking | 3 m |
| Walking | 8 m |
| Running | 22 m |
| Sprinting | 34 m |
| Walking through standing water or deep mud | 14 m |
| Opening a door carefully | 4 m |
| Breaking a window | 45 m |
| Melee hit on a zombie | 12 m |
| Melee hit on a wall, railing, or metal | 30 m |
| Chopping wood (repeating) | 30 m |
| Generator running (continuous) | 40 m |
| Pistol shot | 180 m |
| Shotgun shot | 320 m |
| Car alarm (world event) | 450 m |
| Player coughing (chest infection) | 10 m |

Rain is cover and fog is a trap. That single pair of rows makes the autumn weather table a
stealth system as well as a survival one.

Hearing produces an **investigate target with positional error** proportional to distance, never
a perfect fix on the player. Zombies walk towards roughly where the sound was. This detail is
what makes stealth readable and survivable in first person.

### Sight — secondary
A 130-degree horizontal cone, range scaled by light level at the player's position, the player's
movement speed, crouch state, and weather visibility (see [03](03-survival-systems.md), 3.8). A
still, crouched player in darkness is effectively invisible beyond 6 m. Flashlights are a hard
trade: they extend the player's sight and extend the zombie's much further.

### Smell — tertiary and slow
A coarse grid (4 m cells) holds a decaying scent value along the player's path, amplified by
bleeding wounds and by cooking. Zombies in **Search** state sample it. Smell never produces a
direct lock, only a gradient to follow. It exists so that hiding in a closet while bleeding is
not a guaranteed escape, and so that cooking meat has a price.

Rain washes the scent grid quickly; frozen ground holds it longer.

## 4.4 Individual AI state machine

```
   DORMANT --(noise/sight)--> ALERT --(confirm)--> INVESTIGATE
      ^                         |                      |
      |                         |                  (target seen)
      |                         v                      v
   (timeout 90s)  <-------- LOST <--(lose target)-- PURSUE
                              |                       |
                              v                   (in range)
                           SEARCH                  ATTACK
                              |                       |
                              +------(found)----------+
```

| State | Behaviour |
|---|---|
| **DORMANT** | Idle in place, or a slow wander inside a leash. Most zombies in the world are here. Cheapest LOD tick, no pathfinding |
| **ALERT** | Stops, orients towards the stimulus, 0.4-1.4 s depending on the reaction delay in 4.2. The player's window to break line of sight |
| **INVESTIGATE** | Paths to the noise estimate. Opens unlocked doors. Vocalizes, which can cascade to neighbours |
| **PURSUE** | Direct pathing to the player. Pushes through other zombies and attacks barricades in the way |
| **ATTACK** | Swing, or grapple-and-bite. See 4.7 |
| **SEARCH** | Lost the target: spirals around the last known position, samples the scent grid, checks adjacent rooms. Lasts 40-120 s |
| **LOST** | Returns towards origin, stays at raised alertness for 90 s |

### Cascade and vocalization
A zombie entering ALERT or PURSUE vocalizes, which is itself a noise event of radius 25 m. This
is how one mistake becomes a horde, and it must be the player's dominant fear. **There is no
aggro-sharing flag anywhere in the code** — propagation happens only through the noise system,
so the player can always reason about what is happening and why.

## 4.5 Horde meta-simulation

Full agents exist only inside the streaming radius. Beyond it, zombies are **population counts
per district node** on a coarse graph.

### Structure
- The region is partitioned into about 40 **district nodes**, with edges along real traversable
  routes: roads, trails, tree lines, and the rail cut.
- Each node holds population count, aggregation level (scattered / loose / packed), the current
  Stiffened flag from its ambient temperature, and an attraction score.
- Attraction rises from loud events, from light at night, from smoke plumes, and from running
  generators, and decays over hours.

### Migration
On each world tick, population flows along edges towards higher attraction, plus a background
random drift. A gunshot does not spawn a horde: it raises an attraction score, and over the
following in-game hours population physically walks there from neighbouring nodes.
**Consequences arrive late.** A shotgun fired on day 3 is met on day 4, somewhere the player was
not standing. This is the Project Zomboid lesson translated into a spatial model.

### Materialization
Entering a node instantiates its population at authored spawn anchors — behind buildings, in
tree lines, inside structures — that are **never inside the player's current frustum and never
closer than 25 m**. Leaving de-instantiates agents back into a count, preserving aggregation and
alert level for 10 game minutes, so fleeing across a node boundary does not launder pursuit.

### Density budget

| Context | Live agents |
|---|---|
| Typical rural node | 2-6 |
| Town residential node | 8-20 |
| Town centre node | 25-60 |
| After day 28 consolidation, town centre | up to 90 |
| Hard cap | 120 |

The cap is a performance contract; see [09](09-technical-architecture.md), section 9.8.

### Winter consolidation
From day 28 the meta-sim biases migration towards aggregation: scattered populations merge into
packed ones, and the town centre and the highway corridor accumulate. Fewer, larger, more
dangerous groups, with large genuinely empty stretches between them. The late game is a
different map without a single new asset.

## 4.6 Variants

Four, all from one rig and animation set. No unique assets beyond material and proportion
variation.

| Variant | Share | What changes | Tactical problem it poses |
|---|---|---|---|
| **Walker** | ~80% | Baseline | The unit of threat |
| **Crawler** | ~10% | Prone, very slow, hard to see, attacks legs | Punishes inattention in tall grass, mud, and snow drifts; cannot be outrun in a doorway |
| **Fresh** | ~7% | Recently turned: faster and more alert, ignores the Stiffened state for its first days | Readable from intact clothing and little fungal growth, which teaches the player to read corpses |
| **Bloated** | ~3% | Slow; bursts on heavy damage into a **spore and noise cloud** that raises local attraction for 10 minutes and causes a coughing fit | Forbids careless melee in a crowd, and foreshadows the deferred toxic-air axis |

The Bloated variant is where the fungal fiction touches gameplay inside the slice: it is a
walking preview of the spore system, at a cost of one particle effect and one status effect.

**Explicitly no:** screamers, armoured, spitters, exploders, or anything that reads as a video
game enemy. Pillar 3 forbids escalation for its own sake.

## 4.7 Melee combat

Design targets: **slow, heavy, committal, and frightening.** Not an action game. The player
should feel like someone who has never fought before, using a tool as a weapon.

### Swing model
- **Charge-and-release:** hold to wind up (0.35-0.8 s by weapon), release to swing. Early
  release gives a weak fast hit. A full charge is required for a reliable kill.
- **Arc trace, not a hitscan:** the weapon collider sweeps the swing and can clip walls,
  doorframes, and furniture, which cancels the swing and makes a loud impact noise. **Cramped
  rooms take away the player's best option.** Level design must exploit this.
- **Stamina is the limiter:** a full-charge swing costs 12-22 of 100 stamina. Four heavy swings
  and the player is gasping, and gasping is louder (see [03](03-survival-systems.md), 3.5).
- **Hit zones:** head (lethal multiplier), torso (knockback), limbs (crippling). A full-charge
  heavy weapon to the head is a one-hit kill. Everything else is attrition.
- **Clothing bulk** reduces swing speed, which is why the winter travel loadout is a bad fight
  kit (see 3.7).

### Shove
A separate, fast, cheap input. No damage, 6 stamina, creates distance, can stagger a zombie into
a stumble, and **interrupts a grapple attempt**. This is the primary defensive verb and the main
reason a skilled player survives a two-zombie encounter. A shove against a Stiffened zombie
knocks it down, and it is slow to get back up.

### Grapple and bite
Zombie attacks resolve as a **grapple attempt**, not a damage trade.

1. The zombie closes and grabs. The screen shifts, movement locks, the audio goes close and wet.
2. The player gets a break-out window: **0.9 s** normally, **2.2 s** against a Stiffened zombie.
   The struggle is modulated by arm health, stamina, and clothing bulk.
3. Failing to break out produces a bite, which rolls against the clothing bite resistance for
   that body part and converts to a scratch on a success.
4. Being grappled by a second zombie while already held is effectively unrecoverable.

This yields **one zombie survivable, two dangerous, three fatal** with no numeric difficulty
scaling. The lethality is structural.

### What a bite now costs
Because the bite is curable (see [03](03-survival-systems.md), 3.6), a bite is not an
immediate death sentence — it is the loss of three of the region's four antifungal doses, plus
three days of your stockpile, plus three days of no expeditions while winter advances on
schedule. The second bite of a run is fatal. In practice the player should fear a bite about as
much as they would fear a fatal one, and the difference is that the fear has a decision attached
to it instead of a reload.

### Weapons

| Weapon | Damage | Swing | Stamina | Durability | Note |
|---|---|---|---|---|---|
| Fists | Trivial | Fast | Low | - | Risks hand injury |
| Kitchen knife | Low, high head multiplier | Fast | Low | Very low | Quiet instant kill on a DORMANT zombie |
| Crowbar | Medium | Medium | Medium | Very high | The reliable default; also a prying tool |
| Hatchet | High | Medium | Medium | Medium | Dual use: firewood and skulls |
| Fire axe | Very high | Slow | High | High | Heavy, bulky, two-handed |
| Baseball bat | Medium-high, strong knockback | Medium | Medium | Medium | Cheap and forgiving |
| Sledgehammer | Extreme | Very slow | Very high | High | Also breaks barricades and interior walls |
| Pitchfork | Medium, long reach | Slow | Medium | Low | Rural-appropriate; attacks from outside grapple range through a window or fence |
| Spear (crafted) | Medium, long reach | Slow | Medium | Very low | As above, from scrap |

Durability is real and weapons break, usually at the worst possible moment. Repair is partial
and leaves a permanent condition ceiling.

## 4.8 Firearms

Rare, loud, and tactically narrow. Rural America means they exist; the design makes them a
liability rather than a solution.

- Found ammunition is counted in single digits. A hunting rifle with four rounds is a typical
  find.
- A shot is a 180-320 m noise event that reshapes district populations for a day (4.5).
- First-person aiming with real sway driven by stamina, cold (shivering), pain, and arm injury.
  **A shivering character cannot shoot accurately**, which couples firearms to the temperature
  system instead of letting them bypass it.
- No crosshair by default. Iron sights only.
- Firearms are an **emergency exit**, priced accordingly. Using one well means accepting that
  you must leave the district afterwards.

## 4.9 Stealth and evasion

The player's real toolkit, in priority order.

1. **Noise discipline** — crouch-walk, close doors behind you, avoid glass, do not run indoors,
   avoid standing water.
2. **Line of sight** — break it, then stop moving. SEARCH state is beatable by stillness.
3. **Geometry** — doorways let the player fight one at a time; windows allow a pitchfork or
   spear from outside grapple range; stairs slow pursuit badly.
4. **Distraction** — throwable noisemakers: a bottle, a wind-up alarm clock, a car radio left
   on. The alarm clock is the single most valuable consumable in the game and should become
   known as such.
5. **Weather** — rain masks the player's noise; a blizzard stiffens pursuit. Fog does the
   opposite and must be respected.
6. **Barricades** — boarded windows and furniture-blocked doors do not stop zombies. They cost
   them time and make noise while they work, which buys the player information.

## 4.10 Corpses

Bodies persist. They are not cleanup, they are data, material, and fiction.

- **Readable:** intact clothing and light fungal growth means a Fresh zombie died here recently,
  which means this area was active recently.
- **Lootable**, but searching a corpse is slow and leaves the player vulnerable.
- **Fungal growth** advances on corpses over days. A corpse pile indoors becomes a sickness risk
  and a scent source that draws investigation. Burning a pile is a real chore whose price is a
  smoke plume visible across the valley.
- **Reserved hook:** corpse density is the input the deferred toxic-air axis will read. The
  slice tracks density per node and spawns only visual spore haze and the Bloated variant from
  it; the exposure, mask, and filter systems come later. See
  [05](05-world-and-map.md), 5.2.
- **Performance budget:** beyond 40 corpses in a district, the oldest collapse to a cheap static
  mesh with no physics.
