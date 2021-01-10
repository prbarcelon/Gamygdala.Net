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

        public override string ToString()
        {
            return $"PAD: p={Pleasure.Value:0.00}, a={Arousal.Value:0.00}, d={Dominance.Value:0.00}";
        }
    }
}