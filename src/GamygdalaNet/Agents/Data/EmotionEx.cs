using System;
using System.Collections.Generic;
using System.Linq;
using GamygdalaNet.Types;

namespace GamygdalaNet.Agents.Data
{
    public static class EmotionEx
    {
        public static void AddOrUpdateEmotionIntensity(this Dictionary<string, Emotion> emotions, Emotion emotion)
        {
            Emotion newEmotion;

            if (emotions.ContainsKey(emotion.Name))
            {
                // update existing emotion
                var existingEmotion = emotions[emotion.Name];
                var unclampedNewIntensity = existingEmotion.Intensity + emotion.Intensity;
                newEmotion = existingEmotion.CopyButReplace(EmotionIntensity.Clamp(unclampedNewIntensity));
            }
            else
            {
                // add emotion
                newEmotion = emotion.Copy();
            }

            emotions[emotion.Name] = newEmotion;
        }

        /// <summary>
        ///     Decays an emotion map toward zero (no defaults supplied) or
        ///     toward per-emotion defaults (when <paramref name="defaults" />
        ///     is non-null and non-empty). The decay strategy on
        ///     <paramref name="gamygdala" /> operates on the absolute
        ///     distance from each emotion's default; the sign is
        ///     preserved so an emotion below its default rises toward
        ///     it while an emotion above its default falls toward it.
        ///     Emotions whose default is zero are removed when they
        ///     decay below zero (matches the historical behavior); any
        ///     emotion with a positive default is held at the default
        ///     instead.
        /// </summary>
        public static void Decay(this Dictionary<string, Emotion> emotions, Gamygdala gamygdala,
            IReadOnlyDictionary<string, EmotionIntensity> defaults = null)
        {
            var emotionsToRemove = new Queue<string>();
            foreach (var emotion in emotions.ToArray())
            {
                var current = emotion.Value.Intensity.Value;
                var defaultIntensity = defaults != null && defaults.TryGetValue(emotion.Key, out var d)
                    ? d.Value
                    : 0.0;

                // Decay the distance from the default toward zero,
                // preserving sign, so current gravitates monotonically
                // toward default.
                var deltaFromDefault = current - defaultIntensity;
                var sign = deltaFromDefault >= 0 ? 1.0 : -1.0;
                var decayedMagnitude = gamygdala.DecayFunction(
                    EmotionIntensity.Clamp(Math.Abs(deltaFromDefault)));
                if (decayedMagnitude < 0)
                    // Linear decay overshoots when the delta is
                    // smaller than the per-tick step; the overshoot
                    // means we've crossed the default. Clamp.
                    decayedMagnitude = 0;
                var newIntensity = defaultIntensity + sign * decayedMagnitude;

                if (newIntensity <= 0 && defaultIntensity <= 0)
                {
                    // Classic decay-to-zero with removal when there's
                    // no default to hold the emotion.
                    emotionsToRemove.Enqueue(emotion.Key);
                }
                else
                {
                    var clampedIntensity = EmotionIntensity.Clamp(newIntensity);
                    emotions[emotion.Key] = emotion.Value.CopyButReplace(clampedIntensity);
                }
            }

            while (emotionsToRemove.Count > 0)
            {
                var emotionToRemove = emotionsToRemove.Dequeue();
                emotions.Remove(emotionToRemove);
            }
        }
    }
}