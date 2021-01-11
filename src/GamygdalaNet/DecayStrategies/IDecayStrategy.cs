namespace GamygdalaNet.DecayStrategies
{
    public interface IDecayStrategy
    {
        /// <summary>
        ///     Decay function for emotion intensity.
        /// </summary>
        /// <param name="initial">Initial emotion intensity.</param>
        /// <param name="millisPassed">Milliseconds since last decay function execution.</param>
        /// <returns>New emotion intensity.</returns>
        double Decay(double initial, long millisPassed);
    }
}