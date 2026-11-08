# 08 — Audio and UI

First person removed the player's free intelligence feed (see
[01](01-vision-and-pillars.md), 1.3). Audio replaces it. This document is therefore a gameplay
specification, not a polish list.

## 8.1 Audio design contract

1. **Every threat is audible before it is visible.** Minimum two seconds of warning. This is the
   same contract as [04](04-zombies-and-combat.md), 4.1 and it is primarily audio's job.
2. **A sound must answer three questions:** *what is it, where is it, how far is it.* A player
   who cannot answer all three from a sound alone has found a bug.
3. **Diegetic first.** Everything the player hears exists in the world. No stingers on discovery,
   no combat music, no tension layer that knows something the player does not.
4. **Silence is the default texture.** The soundscape must be quiet enough that one scuff is
   loud. Ambience is thin by design: wind, rain on surfaces, distant crows, a loose gutter, a
   door banging two streets away.
5. **The mix reports the body.** Player condition changes the mix, which is how the game tells
   the player they are dying without drawing a bar (8.4).

## 8.2 Middleware and spatialization

- **FMOD Studio** for the audio engine. Reason: snapshot-based mix states (8.4) and parameter
  automation are first-class, the authoring is usable by one person, and the licence is free
  below a revenue threshold a solo project will not cross during development. Wwise would also
  work; Unity's built-in audio would not, because the mix-state system is the design.
- **HRTF binaural spatialization** for all threat sounds, with a stereo-safe fallback. Vertical
  localization matters: "upstairs" and "the basement" must be distinguishable, because buildings
  are up to three storeys plus a basement (see [05](05-world-and-map.md), 5.7).
- **Occlusion and obstruction**, both modelled, because they mean different things to the player:
  - *Occlusion* (a wall between): heavy low-pass, large volume cut. "Something is in another
    room."
  - *Obstruction* (same room, blocked line): mild low-pass, small volume cut, reverb unchanged.
    "Something is in here with me, behind the shelving."
  Occlusion is computed from the same portal graph that carries heat (see 5.7), so an open door
  changes both the temperature and the sound. One authored structure, two systems.
- **Reverb zones per interior volume**, authored alongside the thermal volume: bathroom, hallway,
  garage, church nave, turbine hall. The turbine hall's reverb is a landmark in itself.

## 8.3 Sound as information

### Zombie audio must encode variant and state

| Variant | Signature |
|---|---|
| Walker | Dragging step, slow wet breathing, cloth |
| Crawler | Scraping, close-to-ground drag, no footfall, faster breathing |
| Fresh | Faster step, sharper breathing, audible vocal cords, less fluid in the throat |
| Bloated | Heavy wet step, a low internal gurgle audible at 15 m, a distinctive pressure note |

| State | Signature |
|---|---|
| DORMANT | Breathing only, irregular, very quiet |
| ALERT | Breath catches, a single head-turn cloth rustle, then stillness. **The most important sound in the game** |
| INVESTIGATE | Deliberate footsteps, a low questioning vocalization |
| PURSUE | The vocalization that cascades (radius 25 m), faster footfall, no breath control |
| SEARCH | Footsteps that stop and start, sniffing |
| Stiffened (4.2) | Everything slower and drier, joints audible, breathing shallower |

The ALERT sound is the player's whole stealth game. It must be unmistakable, it must be the
same every time, and it must be quiet enough that it is only heard by a player who is listening.

### Surface materials
Both the player and zombies produce footsteps from a surface map, and the surfaces are
seasonal: dry leaves, wet leaves, mud, standing water, gravel, asphalt, floorboards, tile,
carpet, corrugated steel, fresh snow, packed snow, ice. **Standing water and dry leaves are loud
and the player must learn to route around them** (see the noise table in
[04](04-zombies-and-combat.md), 4.3).

### Weather as a mixer
Rain masks (hearing modifier 0.5-0.85), fog carries (1.25), blizzard smothers (0.35). The
weather's effect on the player's own hearing is mixed identically to its effect on zombie
hearing, so the player can intuit the rule from their own ears. **This is the only honest way
to teach a perception system.**

### Non-threat information
A car alarm half a kilometre away, the generator at the dam, a tree coming down in a storm,
the gutter on the house you chose as shelter. The soundscape is also how the player learns the
world clock: the hum of mains electricity stops on grid-failure day, and the pipes go quiet on
day 10.

## 8.4 Mix states

FMOD snapshots, blended by player condition. They are the HUD.

| State | Trigger | Mix |
|---|---|---|
| Neutral | Default | Flat, wide, quiet |
| Exertion | Stamina below 40% | Breathing forward and loud, world ducked 3 dB, heartbeat layer in |
| Cold | Core temp 33-35 degC | Shivering breath, slight high-frequency roll-off, teeth |
| Moderate hypothermia | 31-33 degC | Heavy low-pass, world distant, own body loud |
| **Severe hypothermia** | 28-31 degC | **Calm.** Wind recedes, reverb lengthens, breathing slows. The paradoxical warmth cue |
| Bleeding | Bleed rate above threshold | Heartbeat prominent, pitch rising with blood loss |
| Fever | Any illness with fever, including the bite | Dull pressure, slight pitch instability, tinnitus bed |
| Grappled | Zombie grapple | Everything else cut hard, close wet detail only |
| Pain | Pain above 60 | Transients softened, own breathing irregular |
| Indoors / Outdoors | Volume transition | Ambience bed swap, reverb swap |

Rule: **no mix state may remove a threat cue.** Even the severe-hypothermia calm keeps zombie
footsteps and the ALERT breath audible, at reduced level. The mix lies about comfort, never
about danger.

## 8.5 UI philosophy — no HUD

There is no health bar, no stamina bar, no temperature gauge, no hunger icon, no compass, no
minimap, no quest log, and no damage numbers. Status is reported in four ways only.

### 1. Screen and audio effects
Specified in [07](07-art-direction.md), 7.10 and 8.4 above. Hypothermia desaturates, bleeding
pulses, cold fogs the breath, wetness streaks the lens, exhaustion drives the breathing in the
mix. These are the continuous, always-on channel.

### 2. The hands
The player's own hands report state: blood on them when bleeding or after treating a wound, red
and stiff in the cold, visibly wet, shaking when shivering or exhausted, bandaged where
bandaged. Looking at your hands is a deliberate input (hold a key), and it is the fastest status
check in the game.

### 3. The condition card
A folded card the character keeps in a pocket, read as a physical object. It lists, in words and
never in numbers:

```
  CONDITION                        19 NOV, 07:40

  Body        cold, tired
  Right arm   bandaged  (clean)
  Left leg    bruised
  Feet        wet, sore          <- trench foot warning

  Hunger      peckish
  Thirst      fine
  Rest        poor

  Carpentry   practised
  First aid   clumsy
  ...
```

Reading it takes a second and a half and occupies the hands. It is the game's only concession to
an explicit status screen, and keeping it diegetic means the player cannot read it mid-fight,
which is the point.

### 4. The paper map
Specified in [05](05-world-and-map.md), 5.6. A two-handed physical object, no position dot, a
fixed glyph set for pencil markers.

### Contextual prompts
The only persistent on-screen element is a small interaction prompt that appears when the player
is looking at something interactive, inside 2.5 m. One line, bottom centre, no icons bar. A
radial appears only where an object genuinely has several distinct actions (a window: open,
climb, board, break; a fire: add fuel, light, cook, extinguish).

### What breaks the no-HUD rule, deliberately
Two exceptions, both because their absence would be unfair rather than tense:
1. **A bite notification.** A single unmissable, unambiguous message, held on screen until
   dismissed. The player must know, immediately and certainly, that the clock in
   [03](03-survival-systems.md), 3.6 has started.
2. **Accessibility options** (8.7), which may restore numeric readouts for players who need them.

## 8.6 Inventory and interaction UX

### The inventory screen
Two panels: **self** on the left, **the container or the ground** on the right. Weight-based,
list-driven, keyboard and controller navigable, with the total weight and the encumbrance band
shown as a filled bar on the self panel. Opening it does **not** pause the game.

| Operation | Input |
|---|---|
| Move one item | Drag, or select and press transfer |
| Move a stack | Modifier plus transfer, with an amount field |
| Move everything | One button, with a confirm when it would overload the player |
| Equip | Drag to a body slot, or a context action |
| Examine | A dedicated key; shows the real numbers (see [06](06-items-crafting-progression.md), 6.1) |
| Favourite to the quick bar | Right-click, four slots |

### Timed actions
Every search, craft, and treatment is a timed action with a visible progress ring **at the point
of interaction in the world**, not on a HUD. Interruptible by input and interrupted automatically
by a threat sound. A player who is mid-suture when something vocalizes nearby stops suturing.

### The clothing screen
A paper-doll with the nine layer slots, each showing insulation, waterproofing, bulk, wetness,
and condition as small bars plus exact numbers on hover. This is the one screen in the game that
is unapologetically a spreadsheet, because 3.7's three loadouts are a packing decision and the
player needs the data to make it.

### Crafting
No global recipe browser. Crafting is reached through the object or the material:
- Looking at a window with planks and a hammer offers *board window*.
- Holding a broom handle and a knife offers *make spear*.
- At a lit fire, the fire's radial lists what the player's current inventory can cook or boil.
A **known-recipes notebook** exists as a readable item for reference, listing what the character
has learned, and it is reference only — nothing can be crafted from it.

## 8.7 Accessibility

Non-negotiable, because a no-HUD game built on audio cues excludes people by default.

| Option | Default | Notes |
|---|---|---|
| Visual sound indicators | Off | An optional directional ring showing the origin of significant sounds. Turning it on makes the game easier and that is acceptable |
| Numeric status readout | Off | Restores numbers for temperature, hunger, thirst, stamina. A full HUD mode |
| Subtitles for diegetic audio | Off | Named sound events as text: *dragging footsteps, close, left* |
| Colourblind-safe accents | Off | Swaps the warm accent and the danger colour for safe pairs; the spore green is also remapped |
| Motion blur | Low | Off available |
| Camera shake | On | Off available |
| Hold-to-press toggles | Hold | All hold inputs convertible to toggles |
| Timed-action auto-complete | Off | Removes the hold requirement on long actions |
| Text size | 100% | 75-200% |
| Mono audio | Off | With the directional indicator strongly recommended alongside |

The visual sound indicator and the numeric readout both make the game measurably easier. That is
the correct trade and it is not gated behind a difficulty label.
