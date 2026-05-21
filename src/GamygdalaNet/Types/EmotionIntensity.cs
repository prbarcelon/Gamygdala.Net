using System;

namespace GamygdalaNet.Types
{
    /// <summary>
    ///     An emotion's intensity in <c>[0, 1]</c>. 0 is "this emotion
    ///     is absent"; 1 is "maximally felt". Distinct from
    ///     <see cref="Likelihood" /> (also <c>[0, 1]</c>) so the
    ///     compiler catches accidentally passing a likelihood where
    ///     an intensity is expected.
    /// </summary>
    public readonly struct EmotionIntensity
    {
        public EmotionIntensity(double value)
        {
            if (value > 1 || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{nameof(value)} must be between 0 and 1, inclusive.");

            Value = value;
        }

        public double Value { get; }

        public static implicit operator double(EmotionIntensity x)
        {
            return x.Value;
        }

        public static implicit operator EmotionIntensity(double x)
        {
            return new EmotionIntensity(x);
        }

        public static EmotionIntensity Clamp(double unclampedValue)
        {
            return new EmotionIntensity(Math.Min(1, Math.Max(0, unclampedValue)));
        }

        public override string ToString()
        {
            return Value.ToString("0.00");
        }
    }
}
