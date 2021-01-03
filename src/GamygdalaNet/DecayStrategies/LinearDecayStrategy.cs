namespace GamygdalaNet.DecayStrategies
{
    /// <summary>
    ///     Calculate the decay by a linear model.
    /// </summary>
    public class LinearDecayStrategy : DecayStrategyBase
    {
        public LinearDecayStrategy(double decayFactor) : base(decayFactor)
        {
        }

        /// <summary>
        ///     Returns the decay by linear model.
        /// </summary>
        /// <param name="initial">Initial emotion intensity.</param>
        /// <param name="millisPassed">Milliseconds since last decay function execution.</param>
        /// <returns>The decay by linear model.</returns>
        public override double Decay(double initial, long millisPassed)
        {
            return initial - DecayFactor * (millisPassed / 1000.0);
        }
    }
}