using System;

namespace GamygdalaNet.DecayStrategies
{
    /// <summary>
    ///     Calculate exponential decay function.
    /// </summary>
    public class ExponentialDecayStrategy : DecayStrategyBase
    {
        public ExponentialDecayStrategy(double decayFactor) : base(decayFactor)
        {
        }

        /// <summary>
        ///     Returns the decay outcome.
        /// </summary>
        /// <param name="initial">Initial emotion intensity.</param>
        /// <param name="millisPassed">Milliseconds since last decay function execution.</param>
        /// <returns>The decay outcome of the function.</returns>
        public override double Decay(double initial, long millisPassed)
        {
            return initial * Math.Pow(DecayFactor, millisPassed / 1000.0);
        }
    }
}