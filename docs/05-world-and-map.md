# 05 — World and Map

## 5.1 Region concept — Hallow Valley

One hand-authored region, **1.5 x 1.5 km**: a rural North American valley in the last weeks of
autumn. A dammed reservoir at the north end, a two-lane state highway running north to south
through a town of about 900 people, mixed hardwood and conifer ridges east and west, and a rock
quarry cut into the eastern slope.

The valley is a **bowl**. The ridges are the natural boundary, so the map needs no invisible
walls: everything the player can see, they can reach.

### Why this shape
- **A bowl reads from inside.** Standing anywhere, the player can orient by the dam to the north
  and the quarry scar to the east. Navigation without a minimap requires authored landmarks on
  the skyline.
- **Altitude is a weather gradient.** The valley floor is mildest; the ridges run 5-8 degC colder
  and much windier, and they get snow days before the town does. Altitude becomes a difficulty
  dial for free.
- **The town concentrates loot and population**, the wilderness concentrates fuel and quiet, and
  the dam concentrates infrastructure. One dense hard zone, one sparse one, one piece of
  machinery: the whole Project Zomboid / Long Dark spectrum inside a small space.

### Rural American dressing
Wooden-frame houses with porches and detached garages. Pickup trucks and one school bus. A gas
station with a convenience store, a diner, a hardware store, a pharmacy, a volunteer fire
station, a white clapboard church, a grain silo, a feed store, and a trailer park strung along
the highway. Chain-link, corrugated steel, vinyl siding, and asphalt shingle. Mud, wet leaves,
and tarpaulins in autumn; drifts, grey ice, and bare branches after day 20.

## 5.2 Districts

Six active districts plus one reserved, about 40 meta-simulation nodes in total (see
[04](04-zombies-and-combat.md), 4.5). District names are learned from in-world signage, never
from a UI label.

| District | Area | Character | Loot profile | Micro-climate | Zombie density |
|---|---|---|---|---|---|
| **Mill Row** | Town south, 300 x 250 m | Residential street: wooden houses, back yards, detached garages, a corner store, the veterinary clinic | Food, clothing, household tools, one antifungal dose | Valley floor, sheltered | Medium |
| **Cross Street** | Town centre, 250 x 200 m | Main street: pharmacy, hardware store, diner, gas station, fire station, church | The best loot in the region by a wide margin, and one antifungal dose | Valley floor, open and windy along the street | High |
| **Hallow Dam** | North, 200 x 200 m | Concrete dam, turbine hall, control building, maintenance sheds | Tools, fuel, machine parts, the generator, one antifungal dose | Wind funnel off the reservoir; the control building has working heat until the grid fails | Low count, high danger |
| **Westridge Pines** | West slope, 500 x 700 m | Conifer and hardwood forest, three hunting cabins, a fire lookout tower | Firewood, snares, rifle ammunition, warm clothing | -6 degC versus the floor; snows first | Very low |
| **Eastcut Quarry** | East slope, 350 x 400 m | Open quarry, machinery, site office, worker trailers, a diesel tank | Explosives, heavy tools, diesel, the generator part | -4 degC, extreme wind exposure, no cover | Low and clustered |
| **The Narrows** | Highway corridor and lake shore | Connective tissue: the state highway, the Pinewood trailer park, a boat house, the flooded underpass, the failed roadblock | Vehicle loot, trailer-park scraps, scattered supplies | Floor; the shore is the windiest place in the valley | Scattered, concentrated at the roadblock |

### Reserved district — County Fairground

| District | Status |
|---|---|
| **County Fairground** | **Authored but sealed in the vertical slice.** |

The fairground east of Cross Street is where the county staged its mass-casualty response and
then its body pile. In the slice it is fenced, gated, visibly hazed with spore bloom, and
non-enterable: the player can walk its perimeter, read it, smell it in the audio and visual mix,
and loot the **EMS truck at the gate** (which holds the fourth antifungal dose).

It exists now so that the deferred toxic-air axis has a home that was always part of the map
rather than an expansion bolted on later. When that axis is implemented, the fairground opens as
the region's highest-risk, highest-reward district with mask and filter gameplay. Nothing inside
it is modelled in the slice beyond the fence line, the haze, and the gate.

### Spoke-and-hub traversal
The town is the hub. Each wilderness district is reachable in 8-20 game minutes from the town
edge. A full expedition to the quarry and back is roughly two game hours including looting,
which fits the day structure in [02](02-core-loop.md), section 2.4.

**Autumn blocks one spoke; winter blocks another and opens a third.** The flooded underpass
closes the direct southern route in heavy rain; the frozen lake opens a direct northern crossing
from day 24; the western ridge trail becomes impassable in a blizzard.

## 5.3 Landmarks

Hard requirement: **from any outdoor position, at least two named landmarks are visible**,
weather permitting. These are the navigation system.

| Landmark | Visible from | Function |
|---|---|---|
| Hallow Dam wall | Most of the valley floor, north half | Primary north reference |
| Fire lookout tower | Western ridge, visible valley-wide | Observation point; see below |
| Church steeple | Town and its approaches | Town-centre reference |
| Quarry scar and crane | Eastern half | East reference |
| Grain silo | Mill Row and the highway, over rooftops | Local reference where sightlines are short |
| Water tower, Pinewood | The Narrows | Highway corridor reference |
| Fairground Ferris wheel | Town and eastern floor | Marks the sealed district; a permanent warning sign on the skyline |

### The fire lookout tower
The one piece of deliberate affordance in an otherwise unhelpful world. Climbing it costs a long,
exposed, cold trip and returns **information**: visible smoke plumes (other fires and world
events), the direction of the incoming weather front, the spore haze over the fairground, and the
ability to pencil a landmark onto the paper map. Exploration is rewarded with knowledge, which is
the scarce resource identified in [01](01-vision-and-pillars.md), 1.3.

## 5.4 Buildings and interiors

### Budget for the slice

| Type | Count | Interior |
|---|---|---|
| Fully enterable, every room authored | 40 | Yes |
| Partially enterable, ground floor only | 12 | Yes, limited |
| Shell or facade only | ~60 | No |

Shells are the honest scope compromise and are authored to read as permanently sealed: collapsed
roofs, welded shutters, fire damage, a caved-in porch. **Never a door that simply will not
open.**

### Interior authoring rules
Every enterable building must satisfy all six, so that the entry-choice decision in
[02](02-core-loop.md), 2.3 is always real.

1. **At least two entries of different cost.** A front door (locked, loud to force) and a back
   window (quiet, risks a glass shard), or a cellar hatch, a garage side door, a porch roof to a
   second-storey window.
2. **At least one interior chokepoint** — a doorway or stair landing where the player can fight
   one zombie at a time.
3. **At least one dead end** that punishes a careless flight path.
4. **An authored thermal state:** windows intact or broken, a heat source or none, a declared
   insulation quality, and a wind exposure value. Three broken windows means it is not shelter.
5. **A readable prior story in props alone.** No notes, no audio logs. Barricaded from the inside
   versus from the outside tells a whole story by itself.
6. **A container layout that rewards searching unglamorous places:** bathroom cabinets, under
   beds, garage shelving, the truck bed, the chest freezer in the basement — not an obvious chest
   in the middle of the room.

### Candidate long-term shelters
Four buildings are authored as viable bases with genuinely different profiles, so choosing one is
a strategic commitment.

| Shelter | Strengths | Costs |
|---|---|---|
| **Mill Row house** | Fireplace, good insulation, a clothesline, a basement, close to residential loot | Many windows to board; medium-density district; wooden walls take barricade damage badly |
| **Westridge hunting cabin** | Wood stove, very low zombie density, firewood in every direction, a well | Far from all good loot; brutal ridge cold and the first district to snow in |
| **Dam control building** | Concrete walls, a single steel entrance, working heat and light until the grid fails | Almost no food; a long exposed trip to anywhere; the wind funnel makes the approach miserable |
| **Church** | Enormous defensible interior, high sightlines, a bell tower observation point | Huge volume is nearly impossible to heat; dead centre of the highest-density district |

## 5.5 The world clock — season plus decay

One fixed calendar, identical in every run (Pillar 3). The player is expected to learn it and
plan against it. This is the clock that drives the DECAY stage of the core loop.

| Day | Event | Effect |
|---|---|---|
| 1 | Start | Grid on, mains on, autumn rain, mild |
| 3 | First sustained downpour | The Narrows underpass floods; southern route closed for 2 days |
| 6-8 | **Grid failure** | All electric light, heat, and appliances die. Refrigerated food begins spoiling. The dam generator becomes the only power |
| 10 | **Water mains failure** | Taps stop. Rain barrels and boiling become mandatory |
| 12 | Vehicle fuel degrades | Siphoned fuel loses potency; generators consume more of it |
| 15-19 | **Season transition** | Sleet, first non-settling snow, overnight freezes, mud that thaws by noon |
| 20 | **Winter arrives** | Hard switch to the winter weather table and the winter art dressing. Rain barrels stop working. Outdoor food caching becomes viable |
| 24 | Lake freezes; **first blizzard** | A direct northern crossing opens. Blizzards begin; zombies Stiffen (see [04](04-zombies-and-combat.md), 4.2) |
| 28 | **Horde consolidation begins** | The meta-sim merges scattered populations. Large empty stretches, and far worse town centres |
| 30+ | Deep winter | Nights at -20 degC, blizzards every 2-3 days, foraging at zero, loot effectively exhausted |

### Loot depletion
Containers do not respawn. A searched container is searched forever. The region holds roughly
**45 days of calories** for a careful player before foraging, trapping, and fishing. A run is a
countdown, and the player's job is to lengthen it.

### Infrastructure as a puzzle
The dam generator can be restored. It needs a machine part from the quarry site office, diesel,
and a working wrench. Restoring it gives the player a switchable power source — light, heat, a
working chest freezer — at the cost of diesel and a **continuous 40 m noise event** that raises
the district's attraction score for as long as it runs (see 4.5). It is the largest decision of
the mid-game and it is purely a Warmth-versus-Noise trade.

## 5.6 Navigation and the paper map

- The player starts with a **county tourist map**: roads, district names, major landmarks. It is
  not accurate about buildings and it is not a minimap. It opens as a physical object held in
  both hands, which costs the player their weapon and most of their vision.
- **Pencil markers** can be placed only while standing at the location, or from the lookout
  tower. A small fixed glyph set — shelter, water, food, danger, cleared — not free text, to keep
  it in fiction and cheap to build.
- **No player position dot.** The player orients by landmarks. In a blizzard the map is nearly
  useless, which is the intended fear.
- **A compass exists as a findable item** and turns blizzard travel from suicide into a
  calculated risk. Finding it is a real power-up delivered as information rather than as stats.

## 5.7 Streaming and world structure

- **Chunking:** terrain and exteriors split into 128 m scene chunks, loaded in a 3x3 ring around
  the player through Addressables.
- **Interiors are separate scenes** loaded by a proximity trigger at the building shell, not on
  door opening, so there is no hitch on entry. Interiors are authored in place rather than
  teleported, so windows show the real exterior.
- **Thermal volumes** are authored per room as box volumes with an insulation value and a wind
  exposure value. Doors and windows are portals whose open or broken state connects volumes.
  Ambient temperature diffuses between connected volumes on the world tick, which makes "open the
  window to chill the room" and "board the window to keep the heat" work as real tactics.
- **Navigation:** NavMesh baked per chunk and per interior, with NavMesh links for windows,
  fences, and vaults. Dynamic carving for barricades and moved furniture.
- **Vertical budget:** at most three storeys plus a basement in any building, to keep NavMesh
  baking and streaming sane. The church tower and the lookout tower are authored exceptions with
  hand-placed links.

## 5.8 Environmental storytelling targets

The valley tells one story through props alone, with no text and no voice.

The fungus came up the state highway from the south. The county set a roadblock at the Narrows
bridge and held it for two days, which is why there are cruisers, cones, a burnt National Guard
truck, and a great many bodies there. The fairground was requisitioned as a casualty station and
then as a body pile, and it is now the worst place in the valley. Some townspeople barricaded
themselves inside the church — from the inside, with the pews. A handful of dam workers sealed
the turbine hall and died warm and in the dark. The quarry crew never came back down the hill.
The trailer park emptied fast and was looted by people who were still organised enough to loot.
The hunting cabins were used by people who waited too long and then left in a hurry.

Each district carries its chapter in barricade direction, corpse placement, abandoned supplies,
and what was taken versus what was left. A player who reads it carefully learns something
*useful*: where the Fresh zombies are, which doors were locked from which side, and where
someone else already cached food.
