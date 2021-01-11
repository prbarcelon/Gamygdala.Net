using GamygdalaNet.DecayStrategies;
using GamygdalaNet.RelationLikeStrategies;

namespace GamygdalaNet
{
    public static class GamygdalaFactory
    {
        public static Gamygdala CreateWithDefaultSettings()
        {
            const double decayFactor = 0.8;
            var decayStrategy = new ExponentialDecayStrategy(decayFactor);
            var additiveRelationLikeStrategy = new AdditiveRelationLikeStrategy();
            var gamygdala = new Gamygdala(decayStrategy, additiveRelationLikeStrategy)
            {
                PrintDebug = true
            };
            return gamygdala;
        }
    }
}