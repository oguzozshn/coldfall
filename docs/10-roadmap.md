# 10 — Roadmap

This is the only document allowed to say when something is finished.

**Rule:** a milestone ends when every one of its exit criteria is demonstrably true, verified by
actually playing or running the stated test. Not when the work "feels done", and not when most of
it is done. A milestone that cannot pass its criteria is re-scoped by cutting features from
*that* milestone, never by moving criteria into the next one.

## 10.1 Schedule overview

Solo developer, assumed 25-30 productive hours per week.

| Milestone | Theme | Estimate | Cumulative |
|---|---|---|---|
| **M0** | Greybox and feel | 4 weeks | 4 |
| **M1** | The body and the weather | 7 weeks | 11 |
| **M2** | The dead | 7 weeks | 18 |
| **M3** | Things and work | 5 weeks | 23 |
| **M4** | The valley | 10 weeks | 33 |
| **M5** | Slice polish | 6 weeks | 39 |

**Honest estimate: 9-10 months to a vertical slice.** The earlier back-of-envelope figure of 3-6
months assumed one pressure axis; the locked scope has temperature and wetness at full depth
plus five illnesses, which is roughly three extra weeks in M1 and two in M5. If the schedule must
come down, 10.4 lists what to cut and in what order.

## 10.2 Milestones

### M0 — Greybox and feel (4 weeks)

Build nothing that is not needed to answer one question: *does moving and hitting things in this
game feel good?*

**Work:** first-person character controller (walk, crouch, sprint, vault, stairs); `SimClock` and
the four-rate tick (9.1); the assembly layout (9.3); one greybox house with two entries, a
chokepoint, and a basement; placeholder melee with charge-and-release and arc tracing against
static targets; the editor time-and-weather scrubber.

**Exit criteria**
1. The player can walk, crouch, sprint, vault a window, and climb stairs without any camera
   artefact, at a stable 60 fps in the greybox scene.
2. A full-charge swing visibly clips a doorframe and cancels, and the developer can feel the
   difference between a clipped and a clean swing without looking at a debug readout.
3. `SimClock` drives all four tick rates, and changing the time scale from 1:20 to 1:1 and to
   1:240 changes nothing about simulation outcomes in the edit-mode tests.
4. `Coldfall.Sim` compiles and its tests run with no scene loaded.
5. Ten minutes of walking around the greybox is enjoyable with no other systems present. **If
   this is not true, stop and fix the controller before M1.**

### M1 — The body and the weather (7 weeks)

The hardest and most important milestone. This is the game.

**Work:** the heat equation (3.3) with wind chill, insulation, and the hypothermia stages; the
per-garment wetness system (3.4) with all sources and drying rates; calories, hydration, fatigue;
clothing layers and the three loadouts; thermal volumes and portals (9.5) with the Burst
diffusion job; the autumn weather table and fronts with the wind telegraph; fire, fuel burn, and
drying by a fire; sleep; the body-part model and wounds (3.5) without the bite; two illnesses
(chest infection, food poisoning); the condition card; the condition-driven screen effects and
FMOD mix states for cold, exertion, and bleeding; the thermal volume visualizer.

**Exit criteria**
1. Starting wet in 6 degC drizzle, a player who does nothing reaches mild hypothermia in 20-35
   game minutes, and a player who lights a fire and dries their clothes does not.
2. Opening a window in a heated room measurably cools it on the thermal visualizer, and closing
   and boarding it warms it back up.
3. A soaked three-layer torso set dries in 2.5-4 game hours next to a fire and does not
   meaningfully dry outdoors in the rain.
4. Trench foot can be contracted by ignoring wet feet for a day, and cured by drying them.
5. A chest infection contracted from exposure produces a cough that is audible as a discrete
   sound event on a fixed interval.
6. No numeric HUD exists. A playtester can state their own condition correctly from the screen
   effects, their hands, and the condition card alone.
7. The soak test runs 10 game days with no NaN, no runaway temperature, and no memory growth.
8. **The feel test:** a developer playtest where the only threat is the weather is tense for at
   least 30 minutes. If the weather alone is boring, the zombies will not save it.

### M2 — The dead (7 weeks)

**Work:** the zombie rig, one animation set, four clothing sets, the fungal growth material
parameter; the individual state machine (4.4); the noise event buffer and the Burst perception
job (4.3); the scent grid; vocalization cascade; the horde meta-simulation with district nodes,
migration, and materialization (4.5); the freezing threshold (4.2); the four variants (4.6);
grapple and break-out; shove; the bite, the bite clock, and the full antifungal course (3.6);
melee durability and hit zones; barricading windows and doors; the noise event visualizer.

**Exit criteria**
1. A zombie hears a 22 m noise through one wall at the correct attenuated distance and
   investigates a position with the correct error radius, verified by the play-mode test and
   visible in the noise visualizer.
2. No code path makes a zombie aware of the player except the noise buffer, verified by the
   assembly reference constraint plus a code review of the agent assembly.
3. One zombie in a doorway is survivable by a competent player, two in an open room is usually
   fatal, three is always fatal. Demonstrated across 20 recorded encounters.
4. Firing a shotgun in Cross Street measurably raises that node's attraction, and a larger
   population is physically present in that district 8-20 game hours later. No zombie is created
   or destroyed by the migration; the conservation test passes.
5. Below -15 degC and in a blizzard, zombies are visibly and audibly Stiffened, the transition
   takes about 60 seconds, and the grapple break-out window is measurably 2.2 s instead of 0.9 s.
6. **Warming a building changes zombie behaviour in no way whatsoever.** This is an explicit
   regression test, because an earlier design did the opposite and it is rejected.
7. A bite produces the unmissable notification, and all of these are true: the course succeeds
   when its eight requirements are met; it fails when a dose window is missed; it fails when the
   player sprints; exactly four doses exist in the region; a dose cached below -5 degC is ruined.
8. 120 live agents in the town centre hold the frame-time budget in 9.8.

### M3 — Things and work (5 weeks)

**Work:** the item model and ~180 item definitions; weight inventory, encumbrance bands, bags,
containers, caching; container content generation and persistence; search timing and rummage
noise; condition, repair, and the condition ceiling; ~60 recipes across the nine groups;
context-driven crafting and the fire radial; recipe knowledge from print; the six proficiencies
and the +30% ceiling; starting traits; food spoilage, cooking, water quality, boiling; the three
remaining illnesses; the save system (9.7).

**Exit criteria**
1. A container searched, looted, and returned to twenty game hours later holds exactly what was
   left in it, across a save, a quit, and a reload.
2. Overloading to 110% is possible, visibly punishing, and recoverable by one input that drops
   the pack.
3. All 60 recipes are reachable through context interaction, and none requires a global recipe
   browser.
4. A proficiency at level 5 is measurably +30% and no more, changes no damage value, and unlocks
   nothing.
5. The save round-trip test passes, the save stays under 8 MB with a fully searched town, and it
   writes in under 400 ms.
6. Permadeath deletes the save and appends the run to the history, and the crash-recovery file
   cannot resurrect a dead character.
7. The region's total available calories are measured by a tool and land within 40-50 game days
   for a careful player.

### M4 — The valley (10 weeks)

The longest milestone and almost entirely content. Systems work here is limited to streaming and
the season switch.

**Work:** terrain and the bowl topology; the 128 m chunk streaming (9.4); the three trim sheets
and the modular kit (7.8); 40 full interiors, 12 partial, ~60 shells, all satisfying the six
authoring rules in 5.4; the seven districts and the ~40 district nodes; the seven landmarks and
the lookout tower; the paper map and pencil markers; the fixed decay calendar (5.5); the dam
generator puzzle; the sealed fairground with its haze and gate; the winter dressing and the
`_SnowAmount` season switch (7.4); the two LUTs, fog sets, and sun setups; NavMesh baking and
links; the district node graph editor.

**Exit criteria**
1. From any outdoor position in clear weather, at least two named landmarks are visible.
2. All 40 full interiors pass the automated 5.4 validation: two entries of different cost, a
   chokepoint, a dead end, an authored thermal state, and a container layout.
3. The player can walk the whole valley without hitting an invisible wall or a door that should
   open and does not.
4. Streaming holds the 150 ms chunk budget while sprinting along the highway, with no visible pop
   and no NavMesh gap at a chunk boundary.
5. The decay calendar fires on the correct days in the 40-day soak run: grid on day 6-8, mains on
   day 10, transition days 15-19, winter on day 20, lake and first blizzard on day 24,
   consolidation on day 28.
6. Day 19 and day 21 screenshots of the same five locations read as the same place in two
   seasons, and the winter dressing added no more than 25 new meshes.
7. The four candidate shelters are each viable for a full run, and each loses for a different
   reason.
8. Restoring the dam generator works, costs diesel, and produces the continuous 40 m noise event
   that raises the district's attraction.

### M5 — Slice polish (6 weeks)

**Work:** the full FMOD mix-state set and all zombie and surface audio; occlusion and obstruction
through the portal graph; HRTF; the no-HUD interface finished; the inventory and clothing screens;
the accessibility options (8.7); the run summary; balance passes on all of
[03](03-survival-systems.md) and [04](04-zombies-and-combat.md); the full soak; external
playtesting.

**Exit criteria**
1. Every threat is audible before it is visible in 20 recorded encounters, with at least two
   seconds of warning in each.
2. A blindfolded listening test identifies variant and approximate distance correctly in at least
   8 of 10 trials.
3. Every accessibility option in 8.7 works and is reachable from the pause menu.
4. The 40-game-day soak passes clean: calendar correct, save under budget, no memory growth, no
   allocation in the standing test.
5. **The slice test.** Five external playtesters, no instruction, 90 minutes each. At least four
   of five must satisfy the five criteria in [02](02-core-loop.md), 2.8 — and criterion 5,
   immediately starting a second run, is the one that decides whether this project continues.

## 10.3 Build order inside a milestone

Within each milestone: simulation first, then the editor tool that makes it debuggable, then the
presentation, then the content. Reversing this order is the most common way a solo survival
project dies — building a beautiful room before the temperature system exists means re-building
the room.

## 10.4 Cut order under schedule pressure

If the schedule slips, cut strictly in this order and stop as soon as it fits. Everything here is
removable without invalidating the slice test in M5.

| # | Cut | Saves | Cost |
|---|---|---|---|
| 1 | Icicle decals and drift meshes from the winter dressing | 1 week | Winter looks slightly cheaper |
| 2 | Ice fishing and snares | 1 week | Late-game calories get tighter; acceptable, the run is a countdown anyway |
| 3 | Two of the four candidate shelters (keep the Mill Row house and the hunting cabin) | 1.5 weeks | Less strategic variety |
| 4 | The dam generator puzzle | 1.5 weeks | Loses the best mid-game decision. Painful |
| 5 | Illnesses down from five to three (keep chest infection, dysentery, fungal) | 1 week | Disease depth drops below "medium" |
| 6 | 40 full interiors down to 28 | 3 weeks | The town feels thinner. Cut shells first, then partials, then fulls |
| 7 | The Bloated and Crawler variants | 1 week | Combat gets less varied and the spore foreshadowing is lost |
| 8 | Eastcut Quarry as a district (fold the generator part into the fire station) | 2 weeks | One spoke fewer |

**Never cut, at any schedule:** the heat equation, the wetness system, the noise-based perception
model, the grapple, the four-dose antifungal economy, the fixed decay calendar, or the no-HUD
interface. Each of those *is* the design; cutting one produces a different and worse game that
would not be worth the remaining 30 weeks.

## 10.5 Risk register

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | **The weather alone is not interesting**, so the game is only tense when zombies are present | Medium | Fatal | M1 exit criterion 8 tests exactly this, before any zombie exists. If it fails, the fix is systemic (tighter fuel economy, harsher drying, more aggressive fronts), not more enemies |
| R2 | **Interior authoring consumes the schedule.** 40 interiors is the largest single content cost | High | Severe | The modular kit and three trim sheets in M4 exist for this. Build five interiors first and measure the real per-interior hours before committing to 40. Cut order item 6 is the release valve |
| R3 | **The simulation becomes unreadable to the player.** Too many interacting systems, so death feels arbitrary | Medium | Severe | The condition card, the hands, and the mix states are the countermeasure, tested by M1 exit criterion 6. Any system that cannot be communicated without a number is a candidate for cutting |
| R4 | **The grapple feels unfair rather than frightening** in first person | Medium | Severe | Prototype it in M2 week one, not week six. The break-out window, the shove interrupt, and the two-second telegraph are the three dials; tune them before building the variants |
| R5 | **Solo scope exhaustion** around month 6, in the middle of M4's content grind | High | Fatal | M4 is deliberately placed after the game is already playable, so there is something to play when motivation dips. Interleave one interior per day with system polish rather than doing ten weeks of pure authoring |
| R6 | **The season switch costs far more than two weeks** | Medium | Moderate | It is one shader property plus two LUTs plus 25 meshes by design (7.4). If the first week suggests otherwise, cut to a LUT-and-fog-only winter and accept that snow does not accumulate on geometry |
| R7 | **Horde meta-simulation produces unreadable consequences.** The player cannot connect a gunshot to a horde a day later | Medium | Moderate | The noise visualizer and the district graph editor make it debuggable; the lookout tower and smoke plumes make it legible in-fiction. If it stays opaque, shorten the migration delay rather than removing the system |
| R8 | **Performance collapse at 120 agents** in the town centre | Low | Moderate | The Burst perception job and the LOD tick budget in 9.8 are designed for it. The fallback is a lower hard cap with tighter aggregation, which also reads as a design choice |
| R9 | **The four-dose economy is too punishing**, so playtesters feel the cure does not exist | Medium | Moderate | It is one `BalanceProfile` number with a validation check. Raising it to six doses is a ten-second change; do it on evidence, not on instinct |
| R10 | **Unity 6 or URP regression** mid-project | Low | Moderate | Pin the editor version per milestone, never upgrade mid-milestone, keep the LFS repository clean enough to roll back |
| R11 | **The no-HUD interface excludes players** who need numeric or visual status | Certain | Moderate | 8.7's accessibility options are mandatory M5 scope, not a stretch goal. They are allowed to make the game easier |
| R12 | **Scope creep back towards the cut list** in [01](01-vision-and-pillars.md), 1.8 — especially vehicles and the toxic-air axis | High | Severe | The cut list is in the vision document for this reason. Toxic air has a reserved district and no systems; keep it that way until the slice ships |
