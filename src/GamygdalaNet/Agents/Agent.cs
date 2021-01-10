using System;
using System.Collections.Generic;
using System.Linq;
using GamygdalaNet.Agents.Data;
using GamygdalaNet.Pad;
using GamygdalaNet.Types;

namespace GamygdalaNet.Agents
{
    /// <summary>
    ///     The emotion agent class taking care of emotion management for one entity.
    /// </summary>
    public class Agent
    {
        private static readonly Dictionary<string, PadVector> MapPad = new Dictionary<string, PadVector>
        {
            [EmotionNames.Distress] = new PadVector(-0.61, 0.28, -0.36),
            [EmotionNames.Fear] = new PadVector(-0.64, 0.6, -0.43),
            [EmotionNames.Hope] = new PadVector(0.51, 0.23, 0.14),
            [EmotionNames.Joy] = new PadVector(0.76, .48, 0.35),
            [EmotionNames.Satisfaction] = new PadVector(0.87, 0.2, 0.62),
            [EmotionNames.FearConfirmed] = new PadVector(-0.61, 0.06, -0.32), //defeated
            [EmotionNames.Disappointment] = new PadVector(-0.61, -0.15, -0.29),
            [EmotionNames.Relief] = new PadVector(0.29, -0.19, -0.28),
            [EmotionNames.HappyFor] = new PadVector(0.64, 0.35, 0.25),
            [EmotionNames.Resentment] = new PadVector(-0.35, 0.35, 0.29),
            [EmotionNames.Pity] = new PadVector(-0.52, 0.02, -0.21), //regretful
            [EmotionNames.Gloating] = new PadVector(-0.45, 0.48, 0.42), //cruel
            [EmotionNames.Gratitude] = new PadVector(0.64, 0.16, -0.21), //grateful
            [EmotionNames.Anger] = new PadVector(-0.51, 0.59, 0.25),
            [EmotionNames.Gratification] = new PadVector(0.69, 0.57, 0.63), //triumphant
            [EmotionNames.Remorse] = new PadVector(-0.57, 0.28, -0.34) //guilty
        };

        private readonly Dictionary<string, Relation> _currentRelations;
        private readonly Dictionary<string, Goal> _goals;
        private readonly Dictionary<string, Emotion> _internalState; // Trading space for lookup speed.

        private Gain _gain;

        /// <summary>
        ///     The emotion agent class taking care of emotion management for one entity.
        /// </summary>
        /// <param name="name">The name of the agent to be created. This name is used as ref throughout the appraisal engine.</param>
        public Agent(string name)
        {
            Name = name;
            _goals = new Dictionary<string, Goal>();
            _currentRelations = new Dictionary<string, Relation>();
            _internalState = new Dictionary<string, Emotion>();
            _gain = 1.0;
        }

        public string Name { get; }

        /// <summary>
        ///     Adds a goal to this agent (so this agent becomes an owner of the goal).
        /// </summary>
        /// <param name="goal">The goal to be added.</param>
        /// <exception cref="ArgumentNullException"><see cref="goal" /> must not be null.</exception>
        public void AddGoal(Goal goal)
        {
            if (goal == null)
                throw new ArgumentNullException(nameof(goal));

            // We are not copying the goal because we need to keep the reference. One goal can be shared between agents
            // so that changes to the goal are reflected in the emotions of all agents sharing the same goal.
            _goals[goal.Name] = goal;
        }

        /// <summary>
        ///     Removes a goal from this agent.
        /// </summary>
        /// <param name="goalName">The name of the goal to be added. </param>
        /// <returns>True if the goal could be removed, false otherwise.</returns>
        /// ///
        /// <exception cref="ArgumentException"><see cref="goalName" /> must not be null or empty.</exception>
        public bool TryRemoveGoal(string goalName)
        {
            if (string.IsNullOrEmpty(goalName))
                throw new ArgumentException($"{nameof(goalName)} cannot be null or empty.");

            if (!HasGoal(goalName))
                return false;

            _goals.Remove(goalName);
            return true;
        }

        /// <summary>
        ///     Checks if this agent owns a goal.
        /// </summary>
        /// <param name="goalName">The name of the goal to be checked.</param>
        /// <returns>True if this agent owns the goal, false otherwise.</returns>
        /// <exception cref="ArgumentException"><see cref="goalName" /> must not be null or empty.</exception>
        public bool HasGoal(string goalName)
        {
            if (string.IsNullOrEmpty(goalName))
                throw new ArgumentException($"{nameof(goalName)} cannot be null or empty.");

            return _goals.ContainsKey(goalName);
        }

        /// <summary>
        ///     Sets the gain for this agent.
        /// </summary>
        /// <param name="gain">The gain value, from 0 to 20 inclusive.</param>
        public void SetGain(Gain gain)
        {
            _gain = gain;
        }

        /// <summary>
        ///     A facilitating method to be able to appraise one event only from the perspective of the current agent (this).
        /// </summary>
        /// <param name="belief">The belief to be appraised.</param>
        /// <param name="gamygdala">
        ///     A reference to the gamygdala instance with the desired appraisal function, so you could use
        ///     different gamygdala instances to manage different groups of agents.
        /// </param>
        public void Appraise(Belief belief, Gamygdala gamygdala)
        {
            gamygdala.Appraise(belief, this);
        }

        public void UpdateEmotionalState(Emotion emotion)
        {
            // Appraisals simply add to the old value of the emotion, so repeated appraisals without decay will result in the sum of the appraisals over time.
            _internalState.AddOrUpdateEmotionIntensity(emotion);
        }

        /// <summary>
        ///     This function returns either the state as is (gain=false) or a state based on gained limiter (limited between 0 and
        ///     1), of which the gain can be set by using <see cref="SetGain" />. A high gain factor works well when appraisals are
        ///     small
        ///     and rare, and you want to see the effect of these appraisals. A low gain factor (close to 0 but in any case below
        ///     1) works well for high frequency and/or large appraisals, so that the effect of these is dampened.
        /// </summary>
        /// <param name="useGain">Whether to use the gain function or not.</param>
        /// <returns>An array of emotions.</returns>
        public Emotion[] GetEmotionalState(bool useGain = false)
        {
            var state = !useGain
                ? _internalState.Values.ToArray()
                : _internalState
                    .Select(emotion =>
                    {
                        var gainEmotion = _gain * emotion.Value.Intensity / (_gain * emotion.Value.Intensity + 1);
                        return new Emotion(emotion.Key, gainEmotion);
                    })
                    .ToArray();

            return state;
        }

        /// <summary>
        ///     This function returns a summation-based Pleasure Arousal Dominance mapping of the emotional state as is
        ///     (gain=false), or a PAD mapping based on a gained limiter (limited between 0 and 1), of which the gain can be set by
        ///     using <see cref="SetGain" />. It sums over all emotions the equivalent PAD values of each emotion (i.e.,
        ///     [P,A,D]=SUM(Emotion_i([P,A,D]))), which is then gained or not. A high gain factor works well when appraisals are
        ///     small and rare, and you want to see the effect of these appraisals. A low gain factor (close to 0 but in any case
        ///     below 1) works well for high frequency and/or large appraisals, so that the effect of these is dampened.
        /// </summary>
        /// <param name="useGain">Whether to use the gain function or not.</param>
        /// <returns>The calculated PAD state.</returns>
        public PadState GetPadState(bool useGain = false)
        {
            var pleasure = 0.0;
            var arousal = 0.0;
            var dominance = 0.0;

            foreach (var emotion in _internalState)
            {
                pleasure += emotion.Value.Intensity * MapPad[emotion.Key].Pleasure;
                arousal += emotion.Value.Intensity * MapPad[emotion.Key].Arousal;
                dominance += emotion.Value.Intensity * MapPad[emotion.Key].Dominance;
            }

            if (useGain)
            {
                double ApplyGain(double value)
                {
                    var result = value > 0
                        ? _gain * value / (_gain * value + 1)
                        : -_gain * value / (_gain * value - 1);
                    return result;
                }

                pleasure = ApplyGain(pleasure);
                arousal = ApplyGain(arousal);
                dominance = ApplyGain(dominance);
            }

            return new PadState(pleasure, arousal, dominance);
        }

        /// <summary>
        ///     Sets the relation this agent has with the target agent. If the relation does not exist, it will be created,
        ///     otherwise it will be updated.
        /// </summary>
        /// <param name="targetAgentName">The agent who is the target of the relation.</param>
        /// <param name="like">The relation, or how much the target agent is liked, from -1 (disliked) to 1 (liked).</param>
        /// <param name="isLikeAdditive">If true, <see cref="like" /> is added to the existing relation's like value.</param>
        public void UpdateRelation(string targetAgentName, DoubleNegativeOneToPositiveOneInclusive like,
            bool isLikeAdditive = false)
        {
            if (!HasRelationWith(targetAgentName))
            {
                _currentRelations[targetAgentName] = new Relation(targetAgentName, like);
            }
            else
            {
                // Not in original code but reflective of gamygdala paper.
                // We want to either change like over time or set it to a static value.
                var initialLike = _currentRelations[targetAgentName].Like;
                var finalLike = isLikeAdditive
                    ? DoubleNegativeOneToPositiveOneInclusive.Clamp(initialLike + like)
                    : like;
                _currentRelations[targetAgentName].Like = finalLike;
            }
        }

        /// <summary>
        ///     Checks if this agent has a relation with the target agent.
        /// </summary>
        /// <param name="targetAgentName">The target agent of the relation.</param>
        /// <returns>True if the relation exists, otherwise false.</returns>
        public bool HasRelationWith(string targetAgentName)
        {
            return _currentRelations.ContainsKey(targetAgentName);
        }

        /// <summary>
        ///     Returns the relation (or null) and true if a relation exists between this agent and the target agent, false
        ///     otherwise.
        /// </summary>
        /// <param name="targetAgentName">The name of the relation's target agent.</param>
        /// <param name="relation">The relation this agent has with the target agent.</param>
        /// <returns>True if a relation exists between this agent and the target agent, false otherwise.</returns>
        public bool TryGetRelation(string targetAgentName, out Relation relation)
        {
            if (HasRelationWith(targetAgentName))
            {
                relation = _currentRelations[targetAgentName];
                return true;
            }

            relation = null;
            return false;
        }

        /// <summary>
        ///     This method decays the emotional state and relations according to the decay factor and function defined in
        ///     <see cref="gamygdala" />. Typically, this is called automatically when you use Gamygdala.
        ///     <see cref="Gamygdala.StartDecay" />, but you can use it yourself if you want to manage the timing.
        /// </summary>
        /// <param name="gamygdala">
        ///     A reference to the gamygdala instance with the desired decay function, so you could use
        ///     different gamygdala instances to manage different groups of agents.
        /// </param>
        public void Decay(Gamygdala gamygdala)
        {
            _internalState.Decay(gamygdala);
            foreach (var relation in _currentRelations) relation.Value.Decay(gamygdala);
        }

        public Relation[] GetRelations()
        {
            return _currentRelations.Values.ToArray();
        }
    }
}