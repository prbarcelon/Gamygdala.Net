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
                newEmotion = existingEmotion.CopyButReplace(DoubleZeroToOneInclusive.Clamp(unclampedNewIntensity));
            }
            else
            {
                // add emotion
                newEmotion = emotion.Copy();
            }

            emotions[emotion.Name] = newEmotion;
        }

        public static void Decay(this Dictionary<string, Emotion> emotions, Gamygdala gamygdala)
        {
            // TODO - Parallelize.
            var emotionsToRemove = new Queue<string>();
            foreach (var emotion in emotions.ToArray())
            {
                var newIntensity = gamygdala.DecayFunction(emotion.Value.Intensity);

                if (newIntensity < 0)
                {
                    // This emotion has decayed below zero, so we need to remove it.
                    emotionsToRemove.Enqueue(emotion.Key);
                }
                else
                {
                    var clampedIntensity = DoubleZeroToOneInclusive.Clamp(newIntensity);
                    emotions[emotion.Key] = emotion.Value.CopyButReplace(clampedIntensity);
                }
            }

            // Remove decayed emotions.
            while (emotionsToRemove.Count > 0)
            {
                var emotionToRemove = emotionsToRemove.Dequeue();
                emotions.Remove(emotionToRemove);
            }
        }
    }
}