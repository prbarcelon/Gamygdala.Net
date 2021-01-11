using System;

namespace GamygdalaNet.Types
{
    public readonly struct Gain
    {
        /// <summary>
        /// Gain used to fine-tune the intensity of agent responses.
        /// </summary>
        /// <param name="value">The gain value, from 0 to 20 inclusive.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Gain(double value)
        {
            if (value <= 0 || value > 20)
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "Error: gain factor for appraisal integration must be between 0 and 20, inclusive.");

            Value = value;
        }

        public double Value { get; }

        public static implicit operator double(Gain x)
        {
            return x.Value;
        }

        public static implicit operator Gain(double x)
        {
            return new Gain(x);
        }
    }
}