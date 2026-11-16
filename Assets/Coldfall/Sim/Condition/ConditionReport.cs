using System.Collections.Generic;
using System.Text;

namespace Coldfall.Sim
{
    /// <summary>
    /// One line of the condition card: a label and the words describing it.
    /// </summary>
    public readonly struct ConditionLine
    {
        /// <summary>What this line is about: "Body", "Feet", "Hunger".</summary>
        public string Label { get; }

        /// <summary>The words. Never numbers.</summary>
        public IReadOnlyList<string> Words { get; }

        /// <summary>True if this line is the most urgent thing on the card.</summary>
        public bool IsWarning { get; }

        /// <summary>Creates a line.</summary>
        public ConditionLine(string label, IReadOnlyList<string> words, bool isWarning = false)
        {
            Label = label;
            Words = words;
            IsWarning = isWarning;
        }

        /// <summary>The words joined for display.</summary>
        public string Text => Words == null || Words.Count == 0 ? "fine" : string.Join(", ", Words);
    }

    /// <summary>
    /// What the condition card says right now.
    /// <para>
    /// Produced by <see cref="ConditionCard"/> and rendered by the view. Keeping it a plain data
    /// object means the mapping from simulation state to words is unit tested, including the
    /// rule that matters most: there are no numbers on it.
    /// </para>
    /// </summary>
    public sealed class ConditionReport
    {
        /// <summary>The date and time written at the top of the card.</summary>
        public string Timestamp { get; set; } = string.Empty;

        /// <summary>Lines about the body and its parts.</summary>
        public List<ConditionLine> Body { get; } = new List<ConditionLine>();

        /// <summary>Lines about hunger, thirst and rest.</summary>
        public List<ConditionLine> Needs { get; } = new List<ConditionLine>();

        /// <summary>
        /// The single most urgent thing, or empty. The player should be able to glance at the
        /// card and find the thing that is going to kill them first.
        /// </summary>
        public string MostUrgent { get; set; } = string.Empty;

        /// <summary>Renders the card as the player reads it.</summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append("CONDITION").Append("                        ").AppendLine(Timestamp);
            builder.AppendLine();

            foreach (var line in Body)
            {
                Append(builder, line);
            }

            builder.AppendLine();

            foreach (var line in Needs)
            {
                Append(builder, line);
            }

            return builder.ToString();
        }

        private static void Append(StringBuilder builder, ConditionLine line)
        {
            builder.Append("  ").Append(line.Label.PadRight(12)).Append(line.Text);

            if (line.IsWarning)
            {
                builder.Append("   <-");
            }

            builder.AppendLine();
        }
    }
}
