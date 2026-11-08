# 02 — Core Loop

## 2.1 The macro loop

One cycle is roughly one in-game day, 45-90 real minutes.

```
        +-------------------------------------------------+
        |                                                 |
        v                                                 |
   [ SHELTER ] --> [ PLAN ] --> [ EXPEDITION ] --> [ RETURN ]
     dry off        read needs     travel, loot,      process,
     warm up        pick target    fight or evade     craft, repair,
     eat, treat     pack load      manage exposure    dry, store, fortify
        ^                                                 |
        |                                                 |
        +---------------- [ DECAY ] <----------------------+
                   world clock advances: loot thins,
                   hordes consolidate, the season turns,
                   infrastructure fails, food spoils
```

The loop widens over a run. Early cycles are short and local because the player cannot survive
long outside. Later cycles are long and distant because the near loot is gone and the weather
costs more. The **DECAY** stage forces that expansion, and it is driven purely by the world
clock, never by player progress (Pillar 3).

## 2.2 The core tension triangle

Every meaningful decision trades between three resources. A proposed mechanic that touches
fewer than two of them is probably noise.

```
                WARMTH
                 /   \
                /     \
               /       \
           NOISE ----- WEIGHT
```

| Axis | What it is | Spent by | Earned by |
|---|---|---|---|
| **Warmth** | Core temperature, dryness, and the fuel economy behind them | Rain, wind, standing still, wet clothing, hunger | Clothing layers, fire, exertion, hot food and drink, sheltered interiors |
| **Noise** | How much attention you are drawing, and over what radius | Running, fighting, forced entry, generators, gunfire, chopping wood, coughing | Walking, crouching, closing doors, lockpicking, unlit camps |
| **Weight** | What you can carry and how much it slows you | Loot, water, fuel, tools, ammunition, layered clothing | Dropping things, caching, extra trips, bags and containers |

**The three pairwise conflicts are the game:**

- **Warmth vs Noise.** Fire keeps you alive; gathering and splitting the fuel for it is the
  loudest routine action you perform. A generator gives you light and heat and broadcasts your
  position for 40 m, continuously.
- **Warmth vs Weight.** Insulation is bulky and heavy, and wet insulation is heavier still.
  Wearing enough to survive the ridge means carrying less back from it, and moving slower while
  something follows you.
- **Noise vs Weight.** Heavy loads make running short and loud. The quiet route is the longer
  one, which costs warmth and daylight.

Every new feature proposal is written against this triangle before it is written in code.

## 2.3 The micro loops

### Expedition loop (3-15 minutes)
Approach a structure, read it from outside (lights, door states, tracks in mud or snow, sound),
choose an entry, clear or avoid, search containers, weigh what you found against what you can
carry, leave by a different route than you entered.

The decision that carries this loop is **entry choice**: fast and loud, slow and quiet, or not
at all. Every enterable building must support all three (see [05](05-world-and-map.md), 5.4).

### Combat loop (5-40 seconds)
Combat is a failure state you chose, not a goal. Shape: position (back off, find a doorway or a
corner), commit (swing, shove, or run), recover (stamina, wounds, noise consequences). Full
specification in [04](04-zombies-and-combat.md), section 4.7.

### Maintenance loop (1-5 minutes, at shelter)
Wring out and hang wet clothing, feed the fire, boil water, cook, stitch and bandage, sharpen
and repair tools, split firewood, patch a boarded window. This is the rhythm section of the
game and must feel good to perform hundreds of times: short clear animations, interruptible,
no minigames.

**Drying is the maintenance action the player will underestimate.** Wet clothing has a long
drying time and no insulation value while wet, so the real cost of a rainy expedition is paid
after you get home. This is Pillar 1 expressed as a chore.

## 2.4 Day structure and pacing

With time compression at 1:20, one in-game day is 72 real minutes.

| Game time | Real minutes | Typical use | Pressure source |
|---|---|---|---|
| 06:00-08:00 | 6 | Wake, warm up, eat, pack | Low; the planning window |
| 08:00-16:00 | 24 | Expedition window | Daylight is the resource |
| 16:00-18:00 | 6 | Return journey | Falling light and temperature |
| 18:00-22:00 | 12 | Processing, drying, crafting, fortifying | Fuel burn, noise from work |
| 22:00-06:00 | 24 | Night: sleep, or desperate travel | Temperature minimum, best zombie hearing |

Night is deliberately hostile and usually skipped by sleeping. Travelling at night is a
legitimate high-risk strategy — nothing can see you either — and after winter arrives it is
the only way to cross open ground that a daytime horde now occupies.

## 2.5 Session shape

A session must contain at least one complete expedition cycle. Requirements this puts on the
rest of the design:

- **Save on quit, single slot, overwritten. Deleted on death.** No save scumming.
- **No unskippable opening.** The character wakes in a valid shelter within 5 seconds of
  pressing New Game.
- **Shelter is a re-entry point.** On load the player must be able to rebuild their plan from
  the world itself: what is on the fire, what is hanging to dry, what is laid out on the floor,
  what is pencilled on the paper map. The world is the save summary.
- **Interruptible long actions.** Every multi-minute action (sleeping, boiling, drying,
  chopping, reading, treating a wound) can be cancelled, and is cancelled automatically by a
  nearby threat sound.

## 2.6 Onboarding without a tutorial

No tutorial level, no quest markers, no tooltip chain. Teaching happens through the start state
and the first ten minutes of pressure.

1. **Start kit teaches the pillars.** The character begins with a soaked denim jacket (worse
   than nothing while wet), a lighter with limited fuel, one bottle of water, one can of food,
   and a crowbar. Each item is one lesson: wet clothing is a liability, fire is finite, needs
   are real, entry is a choice, combat is possible but bad.
2. **Start location teaches the loop.** The spawn shelter has a working fireplace, a boarded
   window with a visible gap, a tap that still runs, a clothesline, and one walker audible but
   not visible outside. Every core verb has a target within 30 metres.
3. **Pressure ramps itself.** The character starts wet in 6 degC drizzle, so core temperature
   is already falling and something must be done inside two minutes. Hunger and thirst are the
   second wave, hours later.
4. **Two readable items** are the only concession to explicit instruction: an in-world notepad
   listing controls, and the condition card. See [08](08-audio-and-ui.md), section 8.6.

## 2.7 Failure, death, and what carries over

- **Death is permanent.** The save is deleted. Pillar 4 makes this bearable: the player keeps
  the map in their head and in their own notes.
- **A new character in the same world** is not in the slice's scope, but the save architecture
  must not forbid it. See [09](09-technical-architecture.md), section 9.7.
- **The run summary** reports facts only, no score: days survived, distance travelled, zombies
  killed, lowest core temperature, hours spent wet, cause of death, and the last three things
  the character ate. Flat, forensic, no celebration.

## 2.8 Loop-level success criteria for the vertical slice

The slice works when a playtester, with no instruction, does all of the following inside 90
minutes.

1. Identifies being wet and cold as the first threat, and dries off without being told to.
2. Chooses a quiet entry over a loud one at least once, and can state why.
3. Abandons loot at a container because of encumbrance.
4. Dies, and can explain what they should have done differently.
5. Immediately starts a second run.

Criterion 5 is the only one that matters. The other four are how it gets earned.
