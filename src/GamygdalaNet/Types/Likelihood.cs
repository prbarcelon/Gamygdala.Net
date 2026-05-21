using System;

namespace GamygdalaNet.Types
{
    /// <summary>
    ///     A probability in <c>[0, 1]</c>. Used for the certainty of
    ///     a <see cref="Agents.Data.Belief" /> and for the likelihood
    ///     an <see cref="Agents.Agent" /> records against one of its
    ///     <see cref="Agents.Data.Goal" />s. 0 is disconfirmed, 1 is
    ///     confirmed. Per Popescu §3.4.2 *Goal Likelihood* the
    ///     initial value is "Unknown" (no recorded estimate), not a
    ///     numeric prior.
    /// </summary>
    public readonly struct Likelihood
    {
        public Likelihood(double value)
        {
            if (value > 1 || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{nameof(value)} must be between 0 and 1, inclusive.");

            Value = value;
        }

        public double Value { get; }

        public static implicit operator double(Likelihood x)
        {
            return x.Value;
        }

        public static implicit operator Likelihood(double x)
        {
            return new Likelihood(x);
        }

        public static Likelihood Clamp(double unclampedValue)
        {
            return new Likelihood(Math.Min(1, Math.Max(0, unclampedValue)));
        }

        public override string ToString()
        {
            return Value.ToString("0.00");
        }
    }
}
