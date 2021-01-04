using System;
using System.Globalization;

namespace GamygdalaNet.Types
{
    public readonly struct DoubleZeroToOneInclusive
    {
        public DoubleZeroToOneInclusive(double value)
        {
            if (value > 1 || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{nameof(value)} must be between 0 and 1, inclusive.");

            Value = value;
        }

        public double Value { get; }

        public static implicit operator double(DoubleZeroToOneInclusive x)
        {
            return x.Value;
        }

        public static implicit operator DoubleZeroToOneInclusive(double x)
        {
            return new DoubleZeroToOneInclusive(x);
        }

        public static DoubleZeroToOneInclusive Clamp(double unclampedValue)
        {
            return new DoubleZeroToOneInclusive(Math.Min(1, Math.Max(0, unclampedValue)));
        }
    }
}