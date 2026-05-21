using System;

namespace GamygdalaNet.Types
{
    /// <summary>
    ///     The strength of one agent's "like" toward another, in
    ///     <c>[-1, +1]</c>. Positive values mean the source agent
    ///     likes the target ("alice likes bob", relation +1);
    ///     negative values mean the source dislikes the target. The
    ///     magnitude is the strength of the sentiment. Social
    ///     emotions (Gratitude, Pity, Resentment, Gloating, HappyFor)
    ///     scale by this value during appraisal.
    /// </summary>
    public readonly struct RelationLike
    {
        public RelationLike(double value)
        {
            if (value > 1 || value < -1)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{nameof(value)} must be between -1 and 1, inclusive.");

            Value = value;
        }

        public double Value { get; }

        public static implicit operator double(RelationLike x)
        {
            return x.Value;
        }

        public static implicit operator RelationLike(double x)
        {
            return new RelationLike(x);
        }

        public override string ToString()
        {
            return Value.ToString("0.00");
        }
    }
}
