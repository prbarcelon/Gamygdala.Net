using System;

namespace GamygdalaNet.Types
{
    /// <summary>
    ///     A <see cref="Agents.Data.Goal" />'s utility in
    ///     <c>[-1, +1]</c>. Positive values mean the NPC wants the
    ///     goal to be true ("kill the monster", utility 1); negative
    ///     values mean the NPC wants the goal NOT to be true ("self
    ///     dies", utility -1). The magnitude is the strength of the
    ///     preference.
    /// </summary>
    public readonly struct GoalUtility
    {
        public GoalUtility(double value)
        {
            if (value > 1 || value < -1)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{nameof(value)} must be between -1 and 1, inclusive.");

            Value = value;
        }

        public double Value { get; }

        public static implicit operator double(GoalUtility x)
        {
            return x.Value;
        }

        public static implicit operator GoalUtility(double x)
        {
            return new GoalUtility(x);
        }

        public override string ToString()
        {
            return Value.ToString("0.00");
        }
    }
}
