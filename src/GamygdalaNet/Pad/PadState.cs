using GamygdalaNet.Types;

namespace GamygdalaNet.Pad
{
    public readonly struct PadState
    {
        public PadState(DoubleNegativeOneToPositiveOneInclusive pleasure,
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
            return $"PAD State: p={Pleasure.ToString()}, a={Arousal.ToString()}, d={Dominance.ToString()}";
        }
    }
}