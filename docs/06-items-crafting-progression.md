# 06 — Items, Crafting, Progression

All values are first-pass tuning targets held in `ScriptableObject` assets; see
[09](09-technical-architecture.md), section 9.6.

## 6.1 Item model

One `ItemDefinition` asset per item type. Runtime instances carry only mutable state.

### Definition fields (immutable, authored)

| Field | Type | Notes |
|---|---|---|
| `id` | string | Stable, never reused; the save format references this |
| `displayName` | localized string | Shown in the inventory and the examine panel |
| `category` | enum | Food, Drink, Medical, Tool, Weapon, Clothing, Material, Fuel, Light, Container, Misc |
| `weightKg` | float | 0.01-12.0. The only capacity currency; there is no grid or volume |
| `bulk` | float | 0.0-1.0, clothing only; see [03](03-survival-systems.md), 3.7 |
| `stackable` | bool | Ammunition, nails, pills, matches |
| `maxStack` | int | - |
| `maxCondition` | float | 0 for items with no condition (a can of beans) |
| `twoHanded` | bool | Blocks the off-hand slot |
| `tags` | string set | Drives recipes and interactions: `sharp`, `blunt`, `heatSource`, `waterproof`, `tinder`, `sewing`, `prying`, `antifungal` |
| `containerCapacityKg` | float | Non-zero makes the item a container |
| `spoilHours` | float | Food only, at 20 degC; scales with ambient |
| `calories`, `hydration` | float | Food and drink |
| `insulation`, `windproof`, `waterproof`, `biteResistance`, `coverage` | - | Clothing only |

### Runtime instance state

`condition`, `wetness`, `remainingUses`, `spoilProgress`, `frozen`, `contents` (for containers),
`ammoLoaded`. Nothing else. An item is small enough to serialize cheaply, because the save holds
thousands of them.

### Item examine
Holding an item up (a separate input from using it) shows its real numbers. **No hidden stats.**
The player is allowed to know that this jacket has 0.21 insulation and 0.8 waterproofing,
because the whole design asks them to make numeric trade-offs and hiding the inputs would make
that guesswork.

## 6.2 Inventory and encumbrance

Weight-based, Project Zomboid lineage. No grid, no tetris.

| Source | Capacity |
|---|---|
| Base (pockets and hands) | 8.0 kg |
| School backpack | +14.0 kg, bulk 0.15 |
| Hiking pack | +24.0 kg, bulk 0.35 |
| Duffel bag | +20.0 kg, occupies both hands when carried, droppable instantly |
| Garbage bag | +10.0 kg, 0.4 kg itself, tears at 60% wet |

### Encumbrance curve

| Load | Effect |
|---|---|
| 0-70% | None |
| 70-90% | -15% sprint speed, +25% sprint stamina cost |
| 90-100% | -30% sprint speed, -10% walk speed, +60% sprint stamina cost, +0.2 s swing wind-up |
| 100-130% (overloaded) | Cannot sprint, -35% walk speed, stamina drains while walking |
| Above 130% | Cannot move |

Overloaded is reachable deliberately, which makes the "one more trip or risk it" decision real.
Dropping the pack is a single instant input, and leaving a full pack behind to run is one of the
game's best moments.

### Containers in the world
Every searchable container is a real volume with real contents: kitchen cabinets, bathroom
cabinets, dressers, garage shelving, chest freezers, truck beds, mailboxes, lockers, first-aid
boxes, toolboxes. Searching is a timed action (1.5-6 s by container size) with an audible
rummage (noise radius 5 m) and it can be interrupted.

**No loot tables rolled at open time.** Contents are generated once when the district first
streams in, then persisted, so a container the player saw through a window and came back for
holds what it held.

### Caching
Any world container can hold the player's items permanently. Caching is the intended answer to
encumbrance, and the map is authored with cache-worthy spots along each spoke: the boat house,
the lookout tower, the roadblock cruiser, a culvert under the highway.

**After day 20, an outdoor cache is also a freezer** (see [03](03-survival-systems.md), 3.9) —
and a hazard, because the antifungal doses are ruined below -5 degC.

## 6.3 Item condition, repair, and degradation

| Item class | Degrades from | Repair |
|---|---|---|
| Melee weapon | Each hit; much faster on walls and metal | Duct tape, twine, or a sharpening stone. Each repair lowers the maximum condition ceiling by 8% |
| Clothing | Wear over time, combat damage, climbing through windows | Needle and thread plus a cloth patch. Patched garments lose 10% of their insulation permanently |
| Tool | Uses | Partial, same ceiling rule |
| Firearm | Shots fired, wetness | Gun oil and a cleaning kit |
| Container (fabric) | Wetness and load | Not repairable; it tears |

The condition ceiling rule means **nothing is maintained forever**. The crowbar that has carried
the player since day 1 visibly becomes a worse crowbar, and the search for its replacement is a
goal the world provides without a quest.

## 6.4 Crafting

### Principles
1. **No crafting menu as a shopping list.** Crafting happens through context: at a fire, at a
   workbench, at a window you are boarding, on an item in hand.
2. **About 60 recipes in the slice.** Enough to matter, few enough to balance.
3. **Every recipe serves the three axes in [02](02-core-loop.md), 2.2.** A recipe that produces
   a number with no cost does not ship.
4. **Tool requirements, not just ingredients.** A recipe needs tags in reach (`sharp`,
   `heatSource`, `sewing`), which is what makes a workbench and a fire worth returning to.

### Recipe groups

| Group | Examples | Enables |
|---|---|---|
| Fire and fuel | Tinder bundle, split firewood, improvised torch, char cloth | The warmth economy |
| Water | Boil water, rain-barrel setup, snow-melting pot, improvised filter | The water economy |
| Food | Cook, roast, dry meat strips, open a can without an opener | Calorie preservation |
| Medical | Sterile rag, boiled bandage, splint, herbal poultice, suture kit from fishing line | Wound treatment without a pharmacy |
| Clothing | Patch a garment, sew two garments into a lined coat, improvised rain poncho from sheeting, dry socks from a towel | The wetness and insulation answer |
| Fortification | Board a window (1-4 planks, escalating), barricade a door, nail a furniture brace, build a noise trap from cans and twine | The defensive layer |
| Weapons | Spear from a broom and a knife, nail bat, sharpen, wrap a grip | Reach and durability |
| Trapping | Snare, fishing line, ice auger handle repair | Late-game calories |
| Mechanical | Siphon fuel, splice a generator cable, repair the dam generator | The mid-game infrastructure puzzle |

### Recipe knowledge
Three sources, in ascending order of how good it feels:

1. **Known from the start** — the obvious ones. Boil water, board a window, bandage a wound.
   Roughly 20 recipes. The player is never stuck at minute one.
2. **Found in print** — a manual, a magazine, a recipe card pinned in the diner kitchen, the
   generator's service binder. About 30 recipes. Reading takes 4-20 game minutes and must happen
   somewhere safe, which makes a book a reason to go home.
3. **Inferred by the player** — a handful of recipes are never written down anywhere and are
   discovered by trying a sensible combination. Deliberately few, deliberately obvious in
   hindsight, never required for survival.

## 6.5 The antifungal economy

Specified in full in [03](03-survival-systems.md), section 3.6. Summarized here because it is an
item-economy decision as much as a medical one.

| Fact | Value |
|---|---|
| Doses in the region | **4**, at fixed authored locations |
| Doses per cure | **3**, one every 24 h |
| Therefore | One complete cure, and one spare dose |
| Dose weight | 0.15 kg each; weight is never the obstacle |
| Ruined by | Storage below -5 degC, or condition reaching 0 from a hard fall |
| Craftable | **No.** Nothing in the recipe list produces or extends a dose |
| Substitutable | **No.** No partial cure, no two-dose cure, no herbal alternative |

Design consequences, all intended:

- The doses are the only items in the game with a **known global count**, which makes them the
  only items the player can reason about exhaustively. "There are four of these in the world and
  I have two" is a different kind of thought from any other inventory decision.
- Finding all four becomes a self-assigned mid-game goal with no quest attached.
- Carrying them is a dilemma: on your body they are safe from the cold and lost with your
  corpse; in an outdoor cache they are ruined; in an unheated indoor cache in deep winter they
  are ruined too. **The correct answer is the heated shelter you have to maintain anyway**, which
  quietly ties the most valuable items in the game to the fuel economy.
- A careless bite on day 6 is unrecoverable not because the doses are missing but because the
  stockpile is: the course needs three days of food, water, and fuel the player does not have
  yet. The scarcity is in the calendar, not only in the shelf.

## 6.6 Proficiency — learn by doing, low ceiling

Pillar 4 requires that the character never become powerful. Proficiency therefore exists only to
remove early-game fumbling, not to grant capability.

### The six proficiencies

| Proficiency | Raised by | What levels change |
|---|---|---|
| **Carpentry** | Boarding, barricading, repairing, splitting wood | Barricade health, planks consumed per board, action speed |
| **First aid** | Bandaging, suturing, splinting, administering doses | Treatment success, bandage effectiveness, bleed stop speed, pain from treatment |
| **Cooking** | Cooking, preserving, boiling | Calorie yield, burn chance, preserved-food spoil timers |
| **Blunt** | Melee hits with blunt weapons | Swing speed, stamina per swing, knockback. **Not damage** |
| **Blade** | Melee hits with bladed weapons | Swing speed, stamina per swing, durability loss. **Not damage** |
| **Outdoorsman** | Snaring, fishing, foraging, navigating in bad weather | Trap yield, forage yield, wind-chill resistance by a small margin |

### The ceiling

| Level | Practice required (cumulative) | Cumulative effect |
|---|---|---|
| 0 | 0 | Baseline: clumsy, slow, wasteful |
| 1 | 25 actions | +6% |
| 2 | 80 | +12% |
| 3 | 200 | +18% |
| 4 | 450 | +24% |
| 5 | 900 | **+30%, hard cap** |

**+30% is the entire progression budget of the game.** No proficiency raises damage, and no
proficiency unlocks a recipe, an action, or an interaction. A level 5 character swings faster and
wastes fewer planks; they do not hit harder or survive a bite. There are no trees, no perks, no
specializations, no respec, and no XP number displayed anywhere — the player reads their skill
from the fiction (the condition card lists a word, not a number: *clumsy / practised /
competent*).

### Manuals
A found manual doubles practice gain for that proficiency for the next 40 actions. It is a
multiplier on effort, never a substitute for it, and it is the only reason to carry 0.6 kg of
paperback.

### Starting traits
One choice at character creation, from five flat options, each a small bias rather than a build:
**Carpenter** (Carpentry 2), **Nurse** (First aid 2), **Line cook** (Cooking 2), **Hunter**
(Outdoorsman 2), or **Nothing in particular** (all zero, plus 10% faster practice gain
everywhere). Five buttons, no points to spend, no build optimization. Sixty seconds from the
main menu to the world.

## 6.7 Permadeath consequences

- On death the save is deleted. There is no inheritance, no meta-currency, no unlock, and no
  "next run starts with" anything.
- **What actually carries over is the map in the player's head**: the four dose locations, which
  houses have fireplaces, which route floods, where the generator part is, which window in the
  pharmacy is already broken. The region is fixed and hand-authored precisely so that this
  knowledge is worth having.
- The run summary is forensic and unscored (see [02](02-core-loop.md), 2.7).
- A second character in a persisted world is not in the slice's scope, but the save format
  separates world state from character state so it remains possible; see
  [09](09-technical-architecture.md), 9.7.
