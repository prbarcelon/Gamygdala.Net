using System.Collections.Generic;
using System.Linq;
using GamygdalaNet.Agents.Data;
using GamygdalaNet.Types;

namespace GamygdalaNet.Agents
{
    /// <summary>
    ///     Represents a relation one agent has with other agents. Its main role is to store and manage the emotions felt for a
    ///     target agent (e.g. angry at or pity for). Each agent maintains a list of relations, one relation for each target
    ///     agent.
    /// </summary>
    public class Relation
    {
        private readonly Dictionary<string, Emotion> _emotions; // Trading space for lookup speed.

        /// <summary>
        ///     Represents a relation one agent has with other agents. Its main role is to store and manage the emotions felt for a
        ///     target agent (e.g. angry at or pity for). Each agent maintains a list of relations, one relation for each target
        ///     agent.
        /// </summary>
        /// <param name="targetAgentName">The name of the target agent for this relation.</param>
        /// <param name="like">The relation, or how much the target agent is liked, from -1 (disliked) to 1 (liked).</param>
        public Relation(string targetAgentName, DoubleNegativeOneToPositiveOneInclusive like)
        {
            TargetAgentName = targetAgentName;
            Like = like;
            _emotions = new Dictionary<string, Emotion>();
        }

        public string TargetAgentName { get; }
        public DoubleNegativeOneToPositiveOneInclusive Like { get; set; }
        public Emotion[] EmotionsList => _emotions.Values.ToArray();

        public void AddEmotion(in Emotion emotion)
        {
            _emotions.AddOrUpdateEmotionIntensity(emotion);
        }

        public void Decay(Gamygdala gamygdala)
        {
            _emotions.Decay(gamygdala);
        }

        public override string ToString()
        {
            return $"Relation: targetAgent={TargetAgentName}, like={Like.Value:0.00}";
        }
    }
}