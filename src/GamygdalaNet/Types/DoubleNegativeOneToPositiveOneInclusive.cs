using System;

namespace GamygdalaNet.Types
{
    public readonly struct DoubleNegativeOneToPositiveOneInclusive
    {
        public DoubleNegativeOneToPositiveOneInclusive(double value)
        {
            if (value > 1 || value < -1)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{nameof(value)} must be between -1 and 1, inclusive.");

            Value = value;
        }

        public double Value { get; }

        public static implicit operator double(DoubleNegativeOneToPositiveOneInclusive x)
        {
            return x.Value;
        }

        public static implicit operator DoubleNegativeOneToPositiveOneInclusive(double x)
        {
            return new DoubleNegativeOneToPositiveOneInclusive(x);
        }
        
        public static DoubleNegativeOneToPositiveOneInclusive Clamp(double unclampedValue)
        {
            return new DoubleNegativeOneToPositiveOneInclusive(Math.Min(1, Math.Max(-1, unclampedValue)));
        }

        public override string ToString()
        {
            return Value.ToString("0.00");
        }
    }
}