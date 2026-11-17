using System.Collections.Generic;

namespace Coldfall.Sim
{
    /// <summary>
    /// Turns simulation state into the words on the condition card of docs/08-audio-and-ui.md,
    /// 8.5.
    /// <para>
    /// <b>The rule is that there are no numbers.</b> Docs/08 is unambiguous: no health bar, no
    /// temperature gauge, no hunger icon. The card lists everything in words, which is what
    /// forces the design to be legible rather than merely correct. A model that cannot be said
    /// in words is a model the player cannot act on.
    /// </para>
    /// <para>
    /// The word bands are deliberately coarse. "Cold" covers a degree and a half of core
    /// temperature, because the player does not need to know they are at 35.8: they need to know
    /// that they are cold, that it is getting worse, and roughly how much trouble they are in.
    /// Precision here would be a lie about what a person can feel.
    /// </para>
    /// </summary>
    public static class ConditionCard
    {
        /// <summary>Builds the card for the current state.</summary>
        /// <param name="core">Core temperature and its stage.</param>
        /// <param name="needs">Calories, hydration and fatigue.</param>
        /// <param name="garments">What they are wearing, for the wetness lines.</param>
        /// <param name="clock">For the timestamp at the top of the card.</param>
        public static ConditionReport Build(
            CoreTemperature core, SurvivalNeeds needs, IReadOnlyList<Garment> garments, SimClock clock)
        {
            return Build(core, needs, garments, clock, body: null, illnesses: null);
        }

        /// <summary>Builds the card including wounds and illness.</summary>
        /// <param name="core">Core temperature and its stage.</param>
        /// <param name="needs">Calories, hydration and fatigue.</param>
        /// <param name="garments">What they are wearing, for the wetness lines.</param>
        /// <param name="clock">For the timestamp at the top of the card.</param>
        /// <param name="body">Wounds, bleeding and blood loss. May be null.</param>
        /// <param name="illnesses">Active illnesses. May be null.</param>
        public static ConditionReport Build(
            CoreTemperature core, SurvivalNeeds needs, IReadOnlyList<Garment> garments,
            SimClock clock, Body body, Illnesses illnesses)
        {
            var report = new ConditionReport
            {
                Timestamp = Timestamp(clock),
            };

            var bodyWords = new List<string>();

            var warmth = WarmthWord(core);
            if (warmth != null)
            {
                bodyWords.Add(warmth);
            }

            var restWord = RestWord(needs.FatiguePercent);
            if (needs.FatiguePercent >= 60f)
            {
                bodyWords.Add(needs.FatiguePercent >= 80f ? "exhausted" : "tired");
            }

            if (needs.IsStarving)
            {
                bodyWords.Add("weak with hunger");
            }

            if (body != null && body.Blood < 70f)
            {
                bodyWords.Add(body.Blood < 45f ? "grey and faint" : "light-headed");
            }

            if (illnesses != null && illnesses.HasFever || body != null && body.HasFever)
            {
                bodyWords.Add("feverish");
            }

            if (bodyWords.Count == 0)
            {
                bodyWords.Add("fine");
            }

            report.Body.Add(new ConditionLine(
                "Body", bodyWords, core.Stage >= HypothermiaStage.Mild));

            AddWounds(report, body);

            AddWetZone(report, garments, BodyZone.Torso, "Torso");
            AddWetZone(report, garments, BodyZone.Hands, "Hands");
            AddWetZone(report, garments, BodyZone.Legs, "Legs");
            AddWetZone(report, garments, BodyZone.Feet, "Feet");

            AddIllnesses(report, illnesses);

            report.Needs.Add(new ConditionLine(
                "Hunger", new[] { HungerWord(needs.Calories) }, needs.IsStarving));
            report.Needs.Add(new ConditionLine(
                "Thirst", new[] { ThirstWord(needs.HydrationPercent) }, needs.HydrationPercent < 15f));
            report.Needs.Add(new ConditionLine(
                "Rest", new[] { restWord }, needs.FatiguePercent >= 80f));

            report.MostUrgent = MostUrgent(core, needs, body, illnesses);
            return report;
        }

        /// <summary>
        /// The most pressing problem, in a few words, or empty if nothing is pressing.
        /// <para>
        /// Ordered by how quickly it kills. Cold first, because it is the fastest and because
        /// it is the pillar the whole design rests on; then thirst at eight hours; then hunger
        /// at thirty-six.
        /// </para>
        /// </summary>
        public static string MostUrgent(CoreTemperature core, SurvivalNeeds needs)
        {
            return MostUrgent(core, needs, body: null, illnesses: null);
        }

        /// <summary>
        /// The most pressing problem including wounds and illness.
        /// <para>
        /// Bleeding comes first, above everything. A serious bleed kills inside the hour while
        /// hypothermia takes most of one and thirst takes eight, so a card that led with the
        /// cold while the character emptied onto the floor would be worse than useless.
        /// </para>
        /// </summary>
        public static string MostUrgent(
            CoreTemperature core, SurvivalNeeds needs, Body body, Illnesses illnesses)
        {
            if (body != null && body.BleedPerHour > 0f)
            {
                // Anything that empties the character inside two hours is the only thing worth
                // saying.
                if (body.BleedPerHour >= 50f)
                {
                    return "bleeding badly";
                }

                if (body.Blood < 60f)
                {
                    return "still bleeding";
                }
            }

            if (illnesses != null && illnesses.Has(IllnessKind.FungalInfection))
            {
                return "infected by the bite";
            }

            return MostUrgentWithoutInjury(core, needs);
        }

        private static string MostUrgentWithoutInjury(CoreTemperature core, SurvivalNeeds needs)
        {
            if (core.Stage >= HypothermiaStage.Moderate)
            {
                return "freezing to death";
            }

            if (core.Stage >= HypothermiaStage.Mild)
            {
                return "dangerously cold";
            }

            if (needs.HydrationPercent < 15f)
            {
                return "need water badly";
            }

            if (needs.IsStarving)
            {
                return "need food badly";
            }

            if (core.Stage >= HypothermiaStage.Cold)
            {
                return "getting cold";
            }

            return needs.FatiguePercent >= 80f ? "need to sleep" : string.Empty;
        }

        /// <summary>The warmth word, or null when the body has nothing to report.</summary>
        public static string WarmthWord(CoreTemperature core)
        {
            return core.Stage switch
            {
                HypothermiaStage.Cold => "cold",
                HypothermiaStage.Mild => "shivering hard",
                // Shivering has stopped by now, which is why the word changes character.
                HypothermiaStage.Moderate => "numb and clumsy",
                HypothermiaStage.Severe => "strangely warm",
                HypothermiaStage.Dead => "gone",
                _ => null,
            };
        }

        /// <summary>The hunger word.</summary>
        public static string HungerWord(float calories)
        {
            if (calories < SurvivalNeeds.StarvingCalories)
            {
                return "starving";
            }

            if (calories < 1100f)
            {
                return "hungry";
            }

            if (calories < 1800f)
            {
                return "peckish";
            }

            return calories < 2600f ? "fine" : "full";
        }

        /// <summary>The thirst word.</summary>
        public static string ThirstWord(float hydrationPercent)
        {
            if (hydrationPercent < 15f)
            {
                return "parched";
            }

            if (hydrationPercent < 35f)
            {
                return "thirsty";
            }

            if (hydrationPercent < 60f)
            {
                return "dry";
            }

            return "fine";
        }

        /// <summary>The rest word.</summary>
        public static string RestWord(float fatiguePercent)
        {
            if (fatiguePercent >= 80f)
            {
                return "spent";
            }

            if (fatiguePercent >= 60f)
            {
                return "poor";
            }

            if (fatiguePercent >= 35f)
            {
                return "fair";
            }

            return fatiguePercent >= 15f ? "good" : "rested";
        }

        /// <summary>The wetness word for a zone, or null when it is dry enough not to mention.</summary>
        public static string WetnessWord(float wetness)
        {
            if (wetness >= 0.75f)
            {
                return "soaked";
            }

            if (wetness >= 0.4f)
            {
                return "wet";
            }

            return wetness >= 0.15f ? "damp" : null;
        }

        /// <summary>
        /// One line per hurt body part, in the words a person would use. A wound that is
        /// bleeding says so first, because that is the thing that kills within the hour.
        /// </summary>
        private static void AddWounds(ConditionReport report, Body body)
        {
            if (body == null)
            {
                return;
            }

            foreach (BodyPart part in System.Enum.GetValues(typeof(BodyPart)))
            {
                var words = new List<string>();
                var bleeding = false;

                foreach (var wound in body.Wounds)
                {
                    if (wound.Part != part || wound.IsHealed)
                    {
                        continue;
                    }

                    if (wound.BleedPerHour > 0f)
                    {
                        bleeding = true;
                    }

                    words.Add(WoundWord(wound));
                }

                if (words.Count == 0)
                {
                    continue;
                }

                if (bleeding)
                {
                    words.Insert(0, "bleeding");
                }

                report.Body.Add(new ConditionLine(PartLabel(part), words, bleeding));
            }
        }

        /// <summary>The words for one wound, including whether it has been seen to.</summary>
        public static string WoundWord(Wound wound)
        {
            var noun = wound.Type switch
            {
                WoundType.Scratch => "scratched",
                WoundType.Laceration => "cut open",
                WoundType.DeepWound => "deep wound",
                WoundType.Bite => "bitten",
                WoundType.Fracture => "broken",
                WoundType.Burn => "burned",
                WoundType.Frostbite => "frostbitten",
                WoundType.GlassShard => "glass in it",
                WoundType.TrenchFoot => "sore and soft",
                _ => "hurt",
            };

            if (wound.Type == WoundType.Fracture)
            {
                return wound.IsSplinted ? noun + " (splinted)" : noun;
            }

            if (wound.IsSutured)
            {
                return noun + " (stitched)";
            }

            if (wound.IsBandaged)
            {
                return noun + (wound.IsCleaned ? " (bandaged, clean)" : " (bandaged)");
            }

            return noun;
        }

        /// <summary>One line naming every active illness, in plain words.</summary>
        private static void AddIllnesses(ConditionReport report, Illnesses illnesses)
        {
            if (illnesses == null || illnesses.Active.Count == 0)
            {
                return;
            }

            var words = new List<string>();
            var serious = false;

            foreach (var illness in illnesses.Active)
            {
                words.Add(IllnessWord(illness.Kind));

                if (illness.Definition.IsTerminal)
                {
                    serious = true;
                }
            }

            report.Body.Add(new ConditionLine("Illness", words, serious));
        }

        /// <summary>The word for an illness, as the character would describe it.</summary>
        public static string IllnessWord(IllnessKind kind)
        {
            return kind switch
            {
                IllnessKind.ChestInfection => "a cough you cannot stop",
                IllnessKind.FoodPoisoning => "guts churning",
                IllnessKind.Dysentery => "the runs",
                IllnessKind.WoundInfection => "a wound gone bad",
                IllnessKind.FungalInfection => "something growing in you",
                _ => "unwell",
            };
        }

        private static string PartLabel(BodyPart part)
        {
            return part switch
            {
                BodyPart.Head => "Head",
                BodyPart.Torso => "Torso",
                BodyPart.LeftArm => "Left arm",
                BodyPart.RightArm => "Right arm",
                BodyPart.LeftLeg => "Left leg",
                BodyPart.RightLeg => "Right leg",
                _ => part.ToString(),
            };
        }

        private static void AddWetZone(
            ConditionReport report, IReadOnlyList<Garment> garments, BodyZone zone, string label)
        {
            var insulation = ClothingInsulation.ForZone(garments, zone);
            var word = WetnessWord(insulation.Wetness);

            if (word == null)
            {
                return;
            }

            // Wet feet are the quiet one. Docs/03 makes trench foot a day's inattention, so the
            // card calls them out before the illness exists rather than after.
            var warning = zone == BodyZone.Feet && insulation.Wetness >= 0.75f;
            report.Body.Add(new ConditionLine(label, new[] { word }, warning));
        }

        private static string Timestamp(SimClock clock)
        {
            if (clock == null)
            {
                return string.Empty;
            }

            return $"DAY {clock.Day}, {clock.HourOfDay:00}:{clock.MinuteOfHour:00}";
        }
    }
}
