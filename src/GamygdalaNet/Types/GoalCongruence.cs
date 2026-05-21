using System;

namespace GamygdalaNet.Types
{
    /// <summary>
    ///     A <see cref="Agents.Data.Belief" />'s congruence with one
    ///     of its affected goals, in <c>[-1, +1]</c>. Positive values
    ///     mean the belief facilitates the goal (an event that helps
    ///     the goal become true); negative values mean the belief
    ///     blocks the goal. The magnitude is how strongly the belief
    ///     affects that goal.
    /// </summary>
    public readonly struct GoalCongruence
    {
        public GoalCongruence(double value)
        {
            if (value > 1 || value < -1)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{nameof(value)} must be between -1 and 1, inclusive.");

            Value = value;
        }

        public double Value { get; }

        public static implicit operator double(GoalCongruence x)
        {
            return x.Value;
        }

        public static implicit operator GoalCongruence(double x)
        {
            return new GoalCongruence(x);
        }

        public override string ToString()
        {
            return Value.ToString("0.00");
        }
    }
}
