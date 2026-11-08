# 07 — Art Direction

Stylized realism: correct proportions and materials, simplified detail, restricted palette.
The target is "readable and cold", not "photoreal". A solo developer cannot win on fidelity and
can win on consistency, so every rule below is a consistency rule.

## 7.1 The three visual laws

1. **Value before colour.** A greyscale screenshot must still read: where the walkable floor is,
   where the doorway is, what is a prop and what is terrain, and whether something is moving.
   Albedo stays inside **20-70% luminance**; no pure black and no pure white in any texture.
2. **One warm accent, and it means human.** A single warm orange hue is reserved for fire,
   lamplight, safety, and things people made and maintained. Nothing decorative uses it. In a
   desaturated world a single warm pixel reads as "shelter" from 200 m, which is navigation
   doing the work of a HUD.
3. **Fog is the depth tool.** Depth comes from atmospheric separation, not from detail or
   saturation falloff. Four distance bands must always be distinguishable: hand, room, street,
   ridge.

## 7.2 Autumn palette (days 1-19)

| Role | Hex | Use |
|---|---|---|
| Deep shadow | `#16181A` | Interior corners, under eaves. Darkest value permitted |
| Wet asphalt | `#2E3133` | Roads, shingles, tarpaper |
| Bark and mud | `#3B3228` | Tree trunks, churned ground, fence posts |
| Dead grass | `#6B5F3C` | Fields, verges, hay |
| Oak leaf | `#8A6A32` | Canopy, leaf litter |
| Maple rust | `#9A4F2B` | Accent foliage, rusted metal, brick |
| Faded siding | `#7D7F74` | House walls, vinyl, painted wood |
| Fog and sky | `#8C9298` | Atmosphere, distant ridges |
| Overcast highlight | `#B9BEC0` | Sky, wet highlights, puddles |
| **Warm accent** | `#E08A2E` | Fire, lamps, safety, maintained human things. Reserved |
| Danger | `#7A2B22` | Blood, hazard markings, the roadblock |
| Spore | `#9FB08A` | Fungal growth, fairground haze. Reserved for the infection |

Autumn is **brown-dominant with a cool sky**. The mood is wet, heavy, and rotting. Saturation
sits low but not zero: the maple rust and the dead grass carry the warmth, which is exactly what
winter will take away.

## 7.3 Winter palette (days 20+)

| Role | Hex | Use |
|---|---|---|
| Deep shadow | `#14171C` | As autumn, shifted cooler |
| Shadowed snow | `#3E4A5A` | Snow in shadow. Blue, not grey — this is the single most important winter value |
| Grey ice | `#5E6E78` | Lake ice, frozen mud, icicles |
| Snow midtone | `#8C98A6` | Snow under overcast |
| Snow highlight | `#D7DEE4` | Snow in direct light. Brightest value permitted |
| Bare branch | `#2A2724` | Trees, fences, exposed structure |
| Weathered siding | `#6E7276` | House walls, further desaturated |
| Fog and sky | `#A8B2BA` | Atmosphere; higher value and lower contrast than autumn |
| **Warm accent** | `#E08A2E` | **Unchanged across both seasons.** This is the continuity anchor |
| Danger | `#6E2420` | Blood on snow; darker so it reads against the high-value ground |
| Spore | `#9FB08A` | Unchanged |

Winter is **blue-dominant with high values and low contrast**. The danger is that it reads as
flat and featureless, so the counter-rules are mandatory:

- **Snow in shadow is blue, not grey.** The `#3E4A5A` to `#D7DEE4` spread is the only strong
  value contrast winter has; protect it.
- **Keep the dark verticals.** Bare branches, fences, telephone poles, and structure at `#2A2724`
  are what stop a white field from becoming a void. Winter dressing adds dark vertical elements
  rather than removing them.
- **Tracks are content.** Footprints and drag marks in snow are both readability and gameplay
  information (see [05](05-world-and-map.md), 5.2).

## 7.4 The season switch

One map, two dressings, switched on day 20 (see [05](05-world-and-map.md), 5.5). It must not
require a second set of assets.

### Implementation
- **A global shader property `_SnowAmount` (0-1)** drives every opaque material. Snow accumulates
  by world-space up-normal, masked by an authored "snow receive" channel in the vertex colour, so
  overhangs, porch undersides, and vertical walls stay bare. One property, animated over the
  five-day transition.
- **Two colour grading LUTs** plus two fog colour and density sets, cross-faded by the same
  curve.
- **Two sun setups:** autumn sun is lower-contrast and warmer-shadowed; winter sun is harsher,
  lower in the sky, with a longer shadow length and a cooler ambient.
- **Limited prop swaps only:** drift meshes at wall bases, icicle decals on eaves, frozen-lake
  surface material, bare-branch LOD variants for deciduous trees, and tarpaulins gaining snow
  loads. Budget: **no more than 25 new meshes** for the entire winter dressing.
- **Vegetation:** one shader with an autumn-to-winter blend for foliage colour and density; the
  canopy thins rather than being replaced.

The season switch must be achievable by a single developer in under two weeks of work. If it is
not, cut the icicles first and the drift meshes second.

## 7.5 Lighting

URP, **Forward+**, mixed lighting.

| Element | Approach |
|---|---|
| Sun | One realtime directional light with shadows. Four cascades, 60 m shadow distance |
| Static GI | Baked lightmaps for all interiors and static exteriors, non-directional mode |
| Dynamic objects | Light probes, plus one probe volume per interior |
| Fires and lamps | Realtime point lights with authored flicker curves. **Maximum 4 shadow-casting realtime lights visible at once** |
| Flashlight | One spot light with shadows, counted against the budget above |
| Night | Never pitch black outdoors: a cool ambient floor at `#14171C` keeps silhouettes readable. Interiors at night *are* allowed to be nearly black, which is what makes a lamp matter |
| Volumetrics | Height fog plus a cheap light-shaft pass on the sun and on the flashlight only |

### Interior lighting rule
Every interior is authored to read at three light levels: daylight through windows, lamplight,
and darkness. A room that is only legible with the lights on is not finished, because the grid
fails on day 6.

## 7.6 Fog and atmosphere

Fog is a gameplay system as much as an art one (see [03](03-survival-systems.md), 3.8), so its
values are authored per weather state and are the same numbers the gameplay reads.

| Weather state | Fog density | Fog colour | Effective visibility |
|---|---|---|---|
| Clear cool / clear cold | 0.004 | Palette fog colour | 400-450 m |
| Overcast | 0.010 | Fog colour, +4% value | 250 m |
| Drizzle / light snow | 0.016 | Fog colour | 120-180 m |
| Rain / wet snow | 0.025 | Fog colour, -6% value | 100-120 m |
| Downpour / heavy snow | 0.045 | Fog colour, -10% value | 50-60 m |
| River fog / freezing fog | 0.080 | Fog colour, +10% value | 30 m |
| Blizzard | 0.140 | Snow highlight colour | 15 m |

Exponential height fog, with the height falloff tuned so the valley floor is foggier than the
ridges — which makes climbing the lookout tower visibly worth it.

### Weather particles
Rain and snow are screen-space-anchored particle systems with a world-velocity bias, not
world-volume emitters, for cost. Rain adds wet-surface roughness and puddle growth through a
global `_Wetness` material property; snow adds `_SnowAmount` locally during a storm.

## 7.7 Asset budgets

| Asset class | Triangles | Texel density | Materials |
|---|---|---|---|
| Modular architecture piece | 150-800 | 128 px/m | Shared trim sheet |
| Building exterior, assembled | ≤ 8,000 | - | ≤ 4 |
| Building interior set, assembled | ≤ 15,000 | - | ≤ 5 |
| Small prop | 150-800 | 256 px/m | 1, atlased |
| Hero prop (held item, weapon) | 1,500-4,000 | 512 px/m | 1 |
| Zombie | 12,000 with 4 LODs (12k / 5k / 1.8k / 600) | 256 px/m | 2 (body, clothing) |
| Player arms | ≤ 8,000 | 512 px/m | 2 |
| Tree | 3,000 with 3 LODs plus a billboard | 128 px/m | 1 |

### Scene targets

| Metric | Target |
|---|---|
| Draw calls | < 1,800 with the SRP Batcher |
| Visible triangles | < 3.5 M |
| Frame time | 16.6 ms at 1920x1080 on a GTX 1660 / RX 580 |
| Texture memory | < 2.5 GB resident |
| Realtime shadow casters | ≤ 5 including the sun |

## 7.8 Modular kit rules

- **2 m horizontal grid.** Walls are 2 m wide. Interior ceiling height 2.6 m, exterior storey
  height 3.0 m. Doors 1.0 m, windows 1.0 m, both on the 1 m half-grid.
- **Props snap to 0.25 m**, rotate in 15-degree increments. Authored clutter may break the grid;
  structure never may.
- **Pivots at the lower-left-front corner** of every modular piece, origin at world zero, no
  negative-scale pieces anywhere.
- **Three master trim sheets**, 2048 x 2048, which carry the great majority of architectural
  detail: `trim_wood` (siding, framing, decking, fence), `trim_metal` (corrugated, chain-link,
  pipe, machinery), `trim_concrete` (foundation, plaster, kerb, dam). A fourth, `trim_interior`
  (tile, cabinetry, wallpaper, carpet), is permitted if the first three prove insufficient.
- **Decals carry the storytelling**, not unique textures: grime, water streaks, moss, leaf
  litter, blood, tyre marks, snow drift edges, fungal growth. Base textures stay clean and
  repeatable, and a building becomes specific through decals and props only.
- **No unique-texture buildings.** A building that needs its own 2 K texture set is over budget
  unless it is one of the four candidate shelters or the church.

## 7.9 Character and zombie visual design

### The player
Arms only, first person. Two glove or sleeve states per clothing slot, driven by the equipped
torso and hand items so the player can see what they are wearing. Hands visibly show blood,
wetness, and dirt, which is how the game reports wound and wetness state without a HUD (see
[08](08-audio-and-ui.md), 8.5).

### Zombies
One base mesh, one skeleton, one animation set. Variation comes from:
- **Four clothing sets** (work, casual, outdoor, uniform) as swappable meshes on the same rig.
- **A fungal growth parameter (0-1)** blending a `#9FB08A` sporing layer over skin and clothing
  through a material property. This is the readability system: low growth means **Fresh**, which
  means recently turned and dangerous (see [04](04-zombies-and-combat.md), 4.6). The player
  learns to read a corpse at a glance, which is information delivered as art.
- **Proportion jitter** per instance: height 0.94-1.06, limb thickness, head scale.

Silhouette requirement: a zombie must be distinguishable from a standing human prop, a scarecrow,
and a coat on a hook at 25 m in fog. If it is not, the pose and the growth layer are wrong.

## 7.10 Post-processing

| Effect | Setting |
|---|---|
| Colour grading | Per-season LUT, cross-faded during the transition. The primary mood tool |
| Tonemapping | ACES, slightly lifted blacks so deep shadow still reads as shape |
| Bloom | Low threshold, low intensity. Fire and lamps only; snow must not bloom |
| Vignette | 0.15, static. Enough to centre the eye, not enough to hide threats |
| Film grain | 0.10, animated. Unifies the image and hides banding in the fog |
| Chromatic aberration | **Off.** It fights readability, which Pillar-adjacent law 1 forbids |
| Motion blur | Camera only, low. Off entirely as an accessibility option |
| Depth of field | **Off** in gameplay. Permitted only in the inventory and examine views |

### Condition-driven effects
Screen effects are the HUD (see [08](08-audio-and-ui.md), 8.5), so they are authored as gameplay
signals with strict rules: desaturation for hypothermia stages, a slow pulse vignette for
bleeding, a breath-fog overlay below 5 degC, water droplets and lens streaking while wet, and a
tunnelling vignette at severe hypothermia paired with a *calming* grade — the paradoxical warmth
cue from [03](03-survival-systems.md), 3.3. No effect may fully obscure a threat.

## 7.11 Audio-visual consistency note

The art direction's job is to make the audio design legible, because audio carries the threat
information (see [08](08-audio-and-ui.md)). Practical consequences: surfaces must *look* like
what they sound like (wet leaves, gravel, floorboards, corrugated steel, snow), every door must
read as openable or sealed at a glance, and sightline-blocking geometry must be visually obvious
so the player can plan around what they cannot see.
