using GamygdalaNet.Types;

namespace GamygdalaNet.Pad
{
    public readonly struct PadVector
    {
        public PadVector(DoubleNegativeOneToPositiveOneInclusive pleasure,
            DoubleNegativeOneToPositiveOneInclusive arousal,
            DoubleNegativeOneToPositiveOneInclusive dominance)
        {
            Pleasure = pleasure;
            Arousal = arousal;
            Dominance = dominance;
        }

        public DoubleNegativeOneToPositiveOneInclusive Pleasure { get; }
        public DoubleNegativeOneToPositiveOneInclusive Arousal { get; }
        public DoubleNegativeOneToPositiveOneInclusive Dominance { get; }
    }
}