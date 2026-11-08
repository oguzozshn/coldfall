# 01 — Vision and Pillars

## 1.1 One-line pitch

A rural valley, the week the world stopped working, and the weather turning. You have a
crowbar, a wet jacket, and no idea where anything is.

## 1.2 The fusion thesis

The two references solve different problems, which is why they combine rather than compete.

**Project Zomboid contributes the systemic body and the settlement layer.** Per-limb injury,
infection, learn-by-doing proficiency, deep item interaction, container looting, fortifying a
building you did not build, permadeath on a single save, and a world that degrades on its own
schedule instead of reacting to the player's story.

**The Long Dark contributes the environmental pressure and the traversal layer.** Core body
temperature as the master resource, clothing as a layered insulation system rather than armour,
hand-authored named places learned by memory instead of by minimap, long dangerous trips
between safe points, and silence as the dominant texture.

**There is no third invented hook.** This game is positioned as a faithful fusion of those two
designs in a first-person camera. The novelty is the camera and the honesty of the simulation,
not a gimmick bolted on top. Every design decision below is in service of making the fusion
work, not of making it different.

## 1.3 What first person changes

Moving from Project Zomboid's isometric camera to first person is not a presentation change,
it is a design change. The isometric camera hands the player a free intelligence feed: you see
the zombie two rooms away before it sees you. First person removes that feed.

**Information therefore becomes the scarce resource.** These consequences are binding on every
other document.

1. **Audio is a primary mechanic, not polish.** Sound must carry direction, distance, surface
   material, and zombie type. A player who listens well must survive measurably longer than one
   who does not. See [08](08-audio-and-ui.md).
2. **Sightlines are level design.** Corridors, door states, boarded windows, and stair landings
   are the tools that give and deny information. Interiors are authored around what the player
   cannot see. See [05](05-world-and-map.md), section 5.4.
3. **Weather is a difficulty dial.** Rain, fog, and snow reduce visibility and mask sound,
   which raises tension without adding a single enemy.
4. **Threat telegraphing is generous but indirect.** Zombies announce themselves through
   scuffing, breathing, wet cloth, and disturbed objects at least two seconds before contact.
   Unannounced contact reads as unfair in first person, and is a bug.
5. **No minimap, no enemy markers, no compass by default.** The player navigates by paper map,
   landmarks, and memory.

## 1.4 Design pillars

Four pillars. A feature that serves none of them is cut.

### Pillar 1 — Your body is the hardest system to manage
The character's body is the central simulation, not a set of bars to top up. Core temperature
and wetness are the master variables; hunger, thirst, fatigue, wounds, and illness feed into
them. Getting wet in the rain at 4 degC is a genuine emergency, and the chain from wet to cold
to ill to audible is the spine of the game. The weather is an opponent that never notices you.

### Pillar 2 — Every action costs something you can feel
No free actions. Running costs stamina, makes noise, and soaks your clothes with sweat. Warm
clothing costs mobility and carry space. Fire costs fuel, makes light and smoke, and takes
time. Breaking a window is fast and loud; the back door is slow and quiet. The player should
always be able to name what they just spent.

### Pillar 3 — The world is indifferent, not reactive
No difficulty rubber-banding, no director tuning encounters to the player's state, no scripted
rescues. The world runs on its own clock: the grid fails on a fixed day, the mains stop on a
fixed day, food spoils, hordes migrate for their own reasons, and winter arrives whether or not
the player is ready. Fairness comes from legibility and consistency, never from mercy.

### Pillar 4 — Knowledge is the only permanent progression
The character does not level into power. The player does. Learning that the dam has a working
generator, that the underpass floods after two days of rain, that fresh corpses are the ones
still able to run, that the pharmacy roof hatch is unlocked — that knowledge survives the
character's death. Mechanical proficiency exists but is deliberately shallow and slow, so it
never overshadows player skill. See [06](06-items-crafting-progression.md), section 6.6.

## 1.5 Fiction

A fungal pathogen. It spread through the county in under a week, and the valley was on the
wrong side of the roadblocks. The infected do not rot the way corpses should: the fungus keeps
them moving, and it fruits through dense accumulations of the dead, which is why the fairground
where the county piled the bodies is now a place you can smell from half a kilometre away.

The fiction does three jobs and no more:

1. **It explains the enemy** without requiring lore delivery. The player can see the fungus on
   the bodies.
2. **It explains the cure.** A bite is a fungal infection, and a systemic antifungal course
   will clear it if you have the doses, the time, and somewhere safe to lie down. See
   [03](03-survival-systems.md), section 3.5.
3. **It explains the toxic-air axis** reserved for after the vertical slice: spore density
   around mass-corpse sites, masks, filters, and a reason that burning bodies is worth the
   smoke plume. See [05](05-world-and-map.md), section 5.2.

There is no plot, no cause to discover, no cure to invent, and no rescue. The outbreak is
weather with teeth.

## 1.6 Player fantasy and emotional arc

The target feeling across one life, in order.

1. **Hours 0-2 — Exposure.** You are wet, you do not know where anything is, and one walker
   frightens you. Success is finding a door that closes and getting dry.
2. **Hours 2-10 — Foothold.** One building becomes yours. You know two ways out of it. You have
   a fire, a water source, and a melee weapon you trust. Success is a night that passes
   without incident.
3. **Hours 10-30 — Reach.** Expeditions get longer and go further. This is a logistics problem
   now, not a panic. Success is bringing back something heavy.
4. **Hours 30+ — Erosion.** Winter has arrived, the grid is down, the near loot is gone, and
   the hordes have consolidated into your district. Success is knowing when to abandon the
   place you built.

The game is not winnable. A run ends when the character dies, and the record of it is how long
you lasted and how far you got.

## 1.7 Target experience parameters

| Parameter | Value | Note |
|---|---|---|
| Expected first-run survival | 20-60 minutes | Death is the teacher |
| Target skilled run length | 20-40 hours | One region, slice scope |
| Session length | 45-90 minutes | A full expedition cycle must fit one sitting |
| Time compression | 1 real minute = 20 game minutes | Tunable; see [03](03-survival-systems.md) |
| Zombie density | Low and clustered | Scarcity makes each one matter in first person |
| Combat lethality | Very high in both directions | One survivable, two dangerous, three fatal |
| Winter onset | Day 20 | Fixed; see [05](05-world-and-map.md), section 5.5 |
| Bite cure availability | 4 doses in the whole region, 3 needed per cure | One cure, one spare dose |

## 1.8 Scope guardrails — explicitly NOT in the vertical slice

Binding. Each entry is a project-killer for a solo developer, and each has a cheaper
substitute that is already designed.

| Cut | Why | Substitute in the slice |
|---|---|---|
| Multiplayer or co-op | Doubles the cost of every system | Single-player only; [09](09-technical-architecture.md) section 9.9 keeps the architectural hooks open |
| Procedural world generation | Hand-authored space is the whole point of the TLD half | One authored region, 1.5 x 1.5 km |
| Toxic air / spore gameplay | Needs masks, filters, exposure UI, and a second art pass | Fiction written now, map space reserved, systems deferred |
| Vehicles | Physics, damage, fuel, and streaming all get harder at once | A bicycle, at most, after the slice |
| Base building from parts | Large art and UX cost | Fortify existing structures only: board windows, barricade doors, move furniture |
| NPC survivors, dialogue, factions | Needs AI, writing, animation, and voice | Environmental storytelling and corpses with readable context |
| Visible full body in first person | Animation cost is enormous | Arms-only first-person rig |
| Farming and animal husbandry | Multi-season systems with very slow feedback | Foraging, snare trapping, ice fishing after freeze-up |
| Dynamic destruction | Physics and authoring nightmare | Pre-authored break states on doors, windows, barricades |
| Skill trees and perks | Contradicts Pillar 4 | Learn-by-doing proficiency with a hard, low ceiling |
| Boss or spectacle infected | Wrong genre register | Four variants that change the tactical problem, not the scale |
| A third invented signature mechanic | The user explicitly rejected a forced differentiator | Execute the fusion honestly and well |

## 1.9 Audience

- **Primary:** players of Project Zomboid, The Long Dark, DayZ played solo, SCUM, and the
  Stalker series. People who read item tooltips for fun and keep notes on paper.
- **Not the audience:** players who want power fantasy, wave defence, or a win condition.
- **Store-page honesty:** this is sold on execution and atmosphere, as a first-person take on a
  known and loved formula. No claim of inventing a genre.
