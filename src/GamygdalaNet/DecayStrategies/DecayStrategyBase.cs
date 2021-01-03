namespace GamygdalaNet.DecayStrategies
{
    /// <summary>
    ///     Interface for emotion intensity decay functions.
    /// </summary>
    public abstract class DecayStrategyBase : IDecayStrategy
    {
        /// <summary>
        /// </summary>
        /// <param name="decayFactor">The decay factor used. A factor of 1 means no decay.</param>
        protected DecayStrategyBase(double decayFactor)
        {
            DecayFactor = decayFactor;
        }

        public double DecayFactor { get; }

        /// <summary>
        ///     Decay function for emotion intensity.
        /// </summary>
        /// <param name="initial">Initial emotion intensity.</param>
        /// <param name="millisPassed">Milliseconds since last decay function execution.</param>
        /// <returns>New emotion intensity.</returns>
        public abstract double Decay(double initial, long millisPassed);
    }
}