# 03 — Survival Systems

All values are first-pass tuning targets. They exist so the systems can be built and felt
immediately, then tuned against playtests. Every number lives in a `ScriptableObject`; see
[09](09-technical-architecture.md), section 9.6.

Implementation depth per the locked scope: **temperature full, wetness full, disease medium
(five named illnesses), toxic air deferred.**

## 3.1 Simulation tick

| Layer | Rate | Contents |
|---|---|---|
| Physics | 50 Hz | Character controller, melee traces, ragdolls |
| Needs tick | 1 Hz | Calories, hydration, fatigue, temperature and wetness integration |
| Body tick | 0.2 Hz (every 5 s) | Wound progression, infection, illness, healing |
| World tick | 1 per game-minute | Spoilage, fire fuel burn, weather, drying, horde meta-sim |

Needs integrate at 1 Hz with the current game-time scale applied, so changing time compression
does not change the balance. **All rates below are stated per game hour.**

## 3.2 Needs — primary

| Need | Range | Nominal drain / game hour | Death threshold | Notes |
|---|---|---|---|---|
| **Calories** | 0-3000 kcal | 85 at rest, up to 400 sprinting in cold | 0 for 36 h | Shivering adds up to +150/h. Starvation drains strength before it kills |
| **Hydration** | 0-100% | 2.5% at rest, 7% under exertion | 0% for 8 h | Eating unmelted snow costs 4 degC of core heat per litre |
| **Fatigue** | 0-100% | +4.2%/h awake, -12%/h asleep | No direct death | Above 75%: stamina cap falls, aim sway doubles, blackout microsleeps |
| **Core temperature** | 28.0-41.0 degC | See 3.3 | Below 28.0 degC | The master variable |
| **Wetness** | 0.0-1.0 per garment | See 3.4 | No direct death | The fastest route to the master variable |

### Cross-interactions
- Below 500 kcal, passive metabolic heat generation halves. **Starving makes you cold.**
- Below 20% hydration, stamina regeneration halves.
- Above 60% fatigue, maximum maintainable core temperature drops by 0.4 degC.
- Wetness is the most dangerous state in the model: soaked clothing keeps 15% of its dry insulation, and evaporation from it becomes the largest loss term of all (see 3.3).
- Fever from any illness raises core temperature, which the cold model reads as a buffer. A
  fevered character resists the cold slightly and is dying of something else.

## 3.3 Core temperature model

The heart of the simulation. Implement this first, and implement it exactly.

Core temperature integrates a heat balance each needs tick:

```
dT_core = (H_metabolic + H_radiant + H_conductive
           - L_convective - L_evaporative - L_respiratory) / C_body
```

Terms are signed contributions in degC per game hour. `C_body` is a thermal mass constant,
nominally **3.5** for a 70 kg adult, so a net contribution of +3.5 raises core temperature by
1.0 degC per hour.

### Contributions

| Term | Source | Typical magnitude |
|---|---|---|
| `H_metabolic` | Basal plus activity | +2.0 at rest, +5.5 walking, +11.0 sprinting or chopping |
| `H_radiant` | Fires, stoves, heaters | +1.0 to +14.0; inverse-square falloff, blocked by geometry |
| `H_conductive` | Hot food and drink, heated interiors, bedding | +0.5 to +4.0 |
| `L_convective` | Ambient delta and wind | The core term; formula below |
| `L_evaporative` | Wetness and sweat | Scales `L_convective` by up to 1.6x at full wetness; the dominant term when soaked |
| `L_respiratory` | Breathing cold air | -0.3 per 10 degC below freezing |

**Calibration note.** An earlier draft put evaporation at +40% of convective loss. That figure
cannot produce this document's own outcomes: at +40% the gap between soaked and dry is only about
1.4x, and no convective scale satisfies both "soaked and idle reaches mild hypothermia in 20-35
minutes" and "dry by a fire is safe" at the same time. Evaporation dominates when soaked, which
is also the physically honest answer. `Coldfall.Sim` holds the authoritative constants and they
are covered by tests.

### Convective loss and insulation

```
L_convective = (T_skin_target - T_effective) * (1 - Insulation_effective) * K_exposure
T_effective  = T_ambient - WindChill(T_ambient, wind_speed)
Insulation_effective = Insulation_dry * (1 - 0.85 * Wetness_layer)
```

- `Insulation_effective` is the critical line: **a soaked garment retains about 15% of its
  insulation value.** This is what makes rain lethal and drying mandatory.
- `Insulation_dry` sums across layers with diminishing returns, clamped to 0.0-0.92. Perfect
  insulation does not exist.
- `K_exposure` weights body parts so head, hands, and feet lose heat disproportionately. Losing
  gloves is a real crisis.
- `WindChill` uses a simplified NWS curve, clamped, modulated by shelter occlusion (3.8).

### Hypothermia stages

| Stage | Core temp | Effects |
|---|---|---|
| Normal | 36.5-37.5 | None |
| Cold | 35.0-36.5 | Shivering, +150 kcal/h, mild aim sway, visible breath below 5 degC |
| Mild hypothermia | 33.0-35.0 | Heavy shivering, -25% stamina cap, -20% fine motor speed (bandaging, lockpicking, suturing) |
| Moderate | 31.0-33.0 | Shivering stops, screen desaturates, -50% stamina, muffled audio mix, map markers become unreliable |
| Severe | 28.0-31.0 | Paradoxical warmth, vision tunnels, collapse risk per minute |
| Death | Below 28.0 | Death |

The paradoxical-warmth cue at severe stage is deliberate cruelty: the visual and audio mix
*calms down* just before death. Players must learn to distrust comfort.

### Hyperthermia
Bounded and shallow, because the setting is autumn into winter. Above 39.5 degC core (from
fever, or from heavy work in full winter layers next to a fire) the player suffers -30% stamina
and heavy sweat, which drives wetness up from the inside. The only reason it exists is to
punish over-dressing during labour, which closes a loop with Pillar 2.

### Frostbite
Extremities (hands, feet, face, ears) track a local temperature. Below 0 degC locally for a
sustained period, frostbite accumulates as a **permanent** injury: reduced manual dexterity for
hands, reduced walk speed for feet. Rapid rewarming near a fire worsens it; slow rewarming does
not. Frostbite never fully heals and is one of the two permanent-scar systems (see 3.5) that
make a long-lived character feel historical.

## 3.4 Wetness — full depth

Wetness is tracked **per garment**, plus a skin wetness value for the body underneath.

### Sources

| Source | Rate | Notes |
|---|---|---|
| Drizzle | +0.04 / game hour | Survivable indefinitely with a shell layer |
| Rain | +0.18 / game hour | The autumn default threat |
| Downpour | +0.45 / game hour | Soaked through in about two game hours |
| Wet snow | +0.12 / game hour | Melts on contact; worse than dry snow |
| Dry snow | +0.03 / game hour | Brushes off; mostly harmless |
| Fog | +0.02 / game hour | Slow and easy to miss |
| Wading (shin deep) | +0.30 instant to legs and feet | The flooded underpass |
| Swimming | +1.0 instant, all layers | Effectively a death sentence outside summer |
| Sweat | Up to +0.15 / game hour | From sustained exertion, scaled by clothing bulk |

**Waterproofing** (0.0-1.0 per garment) reduces incoming wetness for that garment and for every
layer beneath it. A good shell layer is the single most valuable clothing find in the autumn
half of the game.

### Drying

| Condition | Rate |
|---|---|
| Worn, outdoors, humid | -0.01 / game hour (effectively never) |
| Worn, indoors, unheated | -0.05 / game hour |
| Worn, next to a fire | -0.28 / game hour |
| Removed, hung on a line indoors | -0.14 / game hour |
| Removed, hung near a fire | -0.55 / game hour |
| Frozen (ambient below -2 degC) | 0.0; the garment stiffens and must be thawed first |

**Drying is the maintenance action players underestimate.** A soaked three-layer torso set takes
most of an evening to dry near a fire, which is fuel the player then does not have for the
night. The honest consequence of a rainy expedition is paid at home.

Design rule: a player must be able to **remove and carry** wet clothing. Stripping the soaked
outer shell and travelling cold-but-dry is a legitimate expert play.

## 3.5 Body, wounds, and the bite

Per-body-part model: **head, torso, left arm, right arm, left leg, right leg**, each with
independent health 0-100 and a wound list.

### Wound types

| Wound | Cause | Progression | Treatment | Untreated outcome |
|---|---|---|---|---|
| Scratch | Zombie claw, thorns, broken glass | Minor bleed, low bacterial risk | Clean, bandage | 7% wound infection |
| Laceration | Blades, heavy impacts | Moderate bleed | Clean, suture, bandage | 25% wound infection, slow heal |
| Deep wound | Impalement, falls | Severe bleed | Disinfect, suture, bandage | Bleed-out in 4-20 minutes |
| **Bite** | Zombie bite | Severe bleed plus **certain fungal infection** | Bandage stops the bleed; the infection needs the antifungal course (3.6) | Death in 72-96 hours |
| Fracture | Falls, heavy blunt impact | Immobilizes the limb | Splint, 10-21 days immobilized | Permanent limp or weak arm |
| Burn | Fire, hot surfaces | Pain, infection risk | Cool, cover; never bandage a fresh burn | 30% wound infection |
| Frostbite | Sustained local cold | Permanent dexterity or speed loss | Rewarm slowly | Permanent, partial |
| Glass shard | Climbing through a broken window | Ongoing bleed until removed | Tweezers, then bandage | Will not heal while embedded |
| Trench foot | Feet above 0.6 wetness for 24+ h | Pain, walk speed loss | Dry feet, dry socks, rest | Becomes a wound infection site |

Trench foot is the wetness axis reaching the body directly, and it is the cheapest possible way
to teach the player that dry socks are loot.

### Functional consequences
- **Head:** vision effects, concussion (unreliable audio direction), unconsciousness risk.
- **Torso:** stamina cap, breathing volume — **louder breathing is a larger noise radius.**
- **Arms:** melee damage and swing speed, aim steadiness, carry capacity, two-handed actions.
- **Legs:** walk and sprint speed, vaulting, fall damage threshold, limp sound.

### Pain, bleed, and bacterial infection
- **Bleed rate** sums per wound. Communicated without a HUD: blood on the player's hands, drips
  on the floor that feed the scent grid, and a rising heartbeat in the audio mix.
- **Pain** 0-100 rises with wound count and severity. Painkillers suppress it, and also suppress
  the player's ability to notice new wounds. A deliberate trade.
- **Bacterial infection load** 0-100, separate from the fungal bite infection. Rises in unclean
  wounds, produces fever.

### Permanent conditions
Two systems leave marks that never clear: **frostbite** and **badly healed fractures**. A
character who survives 30 days should be visibly diminished — a limp, a weaker grip, a lost
fingertip's worth of dexterity. This is how time is written on the body.

## 3.6 The bite and the antifungal course

A bite that breaks the skin **always** transmits the fungal infection. There is no roll. The
player is told immediately and unambiguously.

Clothing **bite resistance** (0.0-0.8 per garment, see 3.7) is rolled first and can convert a
bite into a scratch. That roll, plus the shove verb in [04](04-zombies-and-combat.md), is where
player agency against bites lives.

### Untreated progression

| Elapsed | Stage | Effects |
|---|---|---|
| 0-8 h | Incubation | No symptoms. The window in which treatment is cheapest |
| 8-24 h | Fever | Core temperature +1.5 degC, -20% stamina, light nausea |
| 24-60 h | Weakness | -45% stamina cap, aim sway doubled, intermittent vision blur |
| 60-84 h | Collapse | Cannot run, frequent blackouts, cannot defend meaningfully |
| 84-96 h | Death | Death |

### The cure

A **systemic antifungal course**: three doses of injectable antifungal, one every 24 hours,
with the first dose administered within 24 hours of the bite.

Requirements for the course to succeed, all of which must hold for its full 72 hours:

| Requirement | Value | Why it is the expensive part |
|---|---|---|
| Doses | 3, at 24 h +/- 6 h intervals | Missing a window fails the course permanently |
| First dose timing | Within 24 h of the bite | Forces an immediate decision, not a plan |
| Exertion ceiling | Walking only; no sprinting, fighting, or chopping | No expeditions for three days |
| Location | Must remain within one building | You must already have a shelter |
| Hydration | Above 60% throughout | Three days of water you did not gather |
| Calories | Above 1800 throughout, with a +20% fever surcharge | Three days of food you did not loot |
| Warmth | Core temperature 36.0-39.0 degC throughout | Three days of fuel you did not cut |
| Sleep | At least 8 game hours per day | Which means a building secure enough to sleep in |

**The cure does not cost an item. It costs three days of a stockpile you had to build before
you were bitten.** A player bitten on day 2 cannot be cured because they do not yet have three
days of supplies, even if they are standing on the doses. A player bitten on day 25 can, and
will spend most of their winter reserve doing it.

### Dose scarcity

**Exactly four doses exist in the region.** Fixed locations, never randomized, so that knowing
where they are is Pillar 4 progression:

| # | Location | Access cost |
|---|---|---|
| 1 | Cross Street pharmacy, controlled-substances cabinet | Locked steel cabinet: pharmacy key, or a crowbar and a 30 m noise event |
| 2 | Hallow Dam control building first-aid locker | A long trip; the dam is a low-count, high-danger district |
| 3 | Veterinary clinic cold store, Mill Row | Clinic is small and crowded; the cold store door is loud |
| 4 | EMS truck at the fairground gate | At the edge of the reserved spore district; see [05](05-world-and-map.md), 5.2 |

Three doses per cure means the region holds **one cure and one spare dose.** A second bite in
the same run is fatal. This makes the doses the most valuable objects in the world, makes
finding them a genuine mid-game goal, and makes the decision to spend them on a bite taken
carelessly on day 6 a real and painful mistake.

**Expired or frozen doses:** doses left in an unheated building below -5 degC are ruined. The
player who caches them carelessly destroys the only cure in the valley.

## 3.7 Clothing

Clothing is a layered insulation and waterproofing system, with a secondary protection role.

| Property | Range | Effect |
|---|---|---|
| Insulation (dry) | 0.0-0.35 per garment | Sums with diminishing returns; scaled by wetness (3.3) |
| Windproofing | 0.0-1.0 | Multiplier on the wind-chill term only |
| Waterproofing | 0.0-1.0 | Reduces wetness gain for this garment and the layers beneath |
| Bulk | 0.0-1.0 | Reduces move and swing speed, raises sprint stamina cost and sweat |
| Coverage | Body-part mask | Which `K_exposure` zones it protects |
| Bite resistance | 0.0-0.8 | Chance to convert a bite into a scratch |
| Wetness | 0.0-1.0 | Tracked per garment; see 3.4 |
| Condition | 0.0-1.0 | Degrades with wear and combat; scales insulation and bite resistance |

Layer slots: head, face, torso base, torso mid, torso shell, hands, legs, feet, back.
Maximum three torso layers.

### Three archetypal loadouts the player will discover
| Loadout | Insulation | Waterproof | Bulk | Use |
|---|---|---|---|---|
| Autumn working | Low | High (shell) | Low | Rainy expeditions; the default for days 1-19 |
| Winter travel | High | Medium | High | Crossing open ground after day 20 |
| Fight kit | Medium | Low | Low | Thick sleeves and leather for bite resistance, worn under or instead of the shell |

Those three cannot be combined into one, and switching between them is an inventory and weight
decision taken at the shelter before leaving. That is the Warmth/Weight conflict as a packing
screen.

## 3.8 Weather, season, and shelter

Weather is authored as a sequence of fronts with a readable progression, not random rolls. The
player must be able to learn to forecast. The valley has two weather tables and a transition.

### Autumn table (days 1-19)

| State | Ambient (day / night) | Wind | Visibility | Wetness source |
|---|---|---|---|---|
| Clear cool | 11 / 3 degC | Low | 400 m | None; the good travel day |
| Overcast | 9 / 4 degC | Low | 250 m | None |
| Drizzle | 7 / 3 degC | Low | 180 m | Drizzle |
| Rain | 6 / 2 degC | Moderate | 120 m | Rain |
| Downpour | 5 / 2 degC | High | 60 m | Downpour |
| River fog | 6 / 1 degC | None | 30 m | Fog; sound carries unusually far |
| Cold snap | 2 / -4 degC | Moderate | 300 m | None; first frost, mud freezes solid |

### Winter table (days 20+)

| State | Ambient (day / night) | Wind | Visibility | Wetness source |
|---|---|---|---|---|
| Clear cold | -6 / -16 degC | Low | 450 m | None; coldest nights, best travel |
| Overcast cold | -3 / -9 degC | Low | 250 m | None |
| Light snow | -4 / -11 degC | Moderate | 120 m | Dry snow; covers tracks |
| Wet snow | -1 / -4 degC | Moderate | 100 m | Wet snow; the nastiest winter state |
| Heavy snow | -8 / -15 degC | High | 50 m | Dry snow |
| **Blizzard** | -14 / -22 degC | Extreme | 15 m | Dry snow; lethal outdoors |
| Freezing fog | -5 / -12 degC | None | 30 m | Fog, freezes on contact |

### Transition
Days 15-19 mix the two tables: sleet, the first snow that does not settle, mud that freezes
overnight and thaws by noon. Day 20 is the hard switch to the winter table and the winter art
dressing (see [07](07-art-direction.md), section 7.3).

### What the season change does to play

| Autumn | Winter |
|---|---|
| Rain is the main killer; waterproofing beats insulation | Cold is the main killer; insulation beats waterproofing |
| Mud shows tracks; puddles and the flooded underpass block routes | Snow shows tracks far better; the frozen lake opens a new route |
| Food spoils normally; refrigeration matters until the grid fails | Food cached outdoors does not spoil at all |
| Zombies behave identically everywhere | Zombies slow and go partly deaf below -15 degC and in blizzards (see [04](04-zombies-and-combat.md), 4.2) |
| Firewood is damp and burns poorly | Firewood is dry but demand roughly doubles |
| Foraging yields something | Foraging yields almost nothing; ice fishing opens |

**Wind telegraph:** a wind shift 20-40 minutes before any front arrives. Learning to read it is
the difference between a survivable blizzard and a fatal one.

### Shelter occlusion
Each interior volume computes ambient temperature (from outdoor temperature, insulation quality,
broken windows, and active heat sources), wind exposure 0-1, and a wetness shield. A building
with three broken windows is barely shelter. Boarding a window is therefore both a defensive and
a thermal action — one verb, two systems, which is the standard every verb in this design is
held to.

## 3.9 Food, water, and spoilage

### Food states

| State | Effect |
|---|---|
| Fresh | Full calories |
| Stale | -20% calories |
| Spoiled | -50% calories, 45% food-poisoning risk |
| Rotten | Inedible; 95% poisoning risk if eaten anyway |
| Frozen | Spoilage paused; must be thawed before cooking |
| Cooked | +15% calories, poisoning risk cleared, shorter new spoilage timer |
| Burnt | -40% calories, no poisoning risk |

After day 20, food stored outdoors below 0 degC stops spoiling. Combined with the grid failure
on day 6-8, the player's refrigeration strategy has three distinct eras in one run: fridge,
then nothing, then the outdoors. Planning around that is the clearest expression of Pillar 3.

### Water
Three qualities: **tainted** (ponds, rain barrels, snowmelt, toilets), **treated** (boiled or
purified), and **mains** (taps, before the day-10 shutdown). Tainted water carries a 65%
dysentery chance. Boiling needs a heat source, a pot, and 8 game minutes.

Rainwater collection is the autumn half's easy answer, and it stops working when the rain turns
to snow — a quiet, well-earned mid-game problem.

### Hunting and foraging
- **Snares** in authored animal-sign spots, checked after 6+ hours, yield rabbit.
- **Ice fishing** after the lake freezes (day 24+): an auger or hatchet, plus 20-60 minutes
  stationary in the wind. Stationary plus cold is the price.
- **Foraging:** rose hips, late mushrooms, birch bark for tinder. Deliberately poor, and nearly
  zero after day 20. This is not a farming game (see [01](01-vision-and-pillars.md), 1.8).

## 3.10 Illness — five named conditions

Medium depth by design: five illnesses, simple state machines, no stacking subsystems.

| Illness | Cause | Effect | Treatment |
|---|---|---|---|
| **Chest infection** | Sustained cold and wet exposure, or untreated hypothermia | Stamina cap -35%, and a **recurring cough that is a 10 m noise event** | Antibiotics, warmth, dryness, rest |
| **Food poisoning** | Spoiled or raw food | Nausea, vomiting (hydration loss), -40% stamina | Rest, water, time |
| **Dysentery** | Tainted water | Hydration drain x3 for 2 days | Rehydration, antibiotics |
| **Wound infection** | Untreated or dirty wounds | Fever, local limb health decay | Disinfectant, antibiotics |
| **Fungal infection** | Zombie bite | Terminal in 72-96 h unless cured | The antifungal course (3.6) |

**The cough is the most important line in this table.** Chest infection turns the player's own
body into a noise emitter they cannot fully control, which is the purest first-person survival
idea in this document: your illness gives you away. It is also the direct mechanical payoff of
the wetness system, which is what makes rain frightening rather than merely inconvenient.

## 3.11 Sleep

Sleeping advances time quickly and is the only efficient way to clear fatigue.

- Requires a bed, bedroll, or seat; quality sets the fatigue recovery rate.
- Core temperature must be above 34.0 degC to fall asleep; bedding adds insulation.
- Wet clothing prevents sleep entirely above 0.4 wetness. The player must undress and dry off,
  which means they wake up with nothing on if something comes through the door.
- The player sets a wake time, or sleeps until rested.
- **Sleep is interrupted** by loud noises, by core temperature falling below 33.0 degC, by
  severe pain, and by a zombie entering the same building. Interruption is the main reason to
  fortify, which is where the survival half of the design meets the combat half.
