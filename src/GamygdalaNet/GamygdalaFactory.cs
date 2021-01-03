﻿using GamygdalaNet.DecayStrategies;

namespace GamygdalaNet
{
    public static class GamygdalaFactory
    {
        public static Gamygdala CreateWithDefaultSettings()
        {
            const double decayFactor = 0.8;
            var decayStrategy = new ExponentialDecayStrategy(decayFactor);
            var gamygdala = new Gamygdala(decayStrategy);
            return gamygdala;
        }
    }
}