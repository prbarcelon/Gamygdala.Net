using System;
using GamygdalaNet.Types;

namespace GamygdalaNet.Agents.Data
{
    /// <summary>
    ///     Stores an emotion with its intensity.
    /// </summary>
    public readonly struct Emotion
    {
        /// <summary>
        ///     Stores an emotion with its intensity.
        /// </summary>
        /// <param name="name">The name of the emotion</param>
        /// <param name="intensity">The initial intensity of the emotion</param>
        public Emotion(string name, DoubleZeroToOneInclusive intensity)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException($"{nameof(name)} cannot be null or empty.");

            Name = name;
            Intensity = intensity;
        }

        public string Name { get; }
        public DoubleZeroToOneInclusive Intensity { get; }

        public Emotion CopyButReplace(DoubleZeroToOneInclusive intensity)
        {
            return new Emotion(Name, intensity);
        }

        public Emotion Copy()
        {
            return new Emotion(Name, Intensity);
        }

        public override string ToString()
        {
            return $"Emotion: {Name}={Intensity.Value:0.00}";
        }
    }
}