using System;
using System.Collections.Generic;
using GamygdalaNet.Agents;
using GamygdalaNet.Agents.Data;
using GamygdalaNet.DecayStrategies;
using GamygdalaNet.Types;

namespace GamygdalaNet
{
    public class Gamygdala
    {
        private readonly Dictionary<string, Agent> _agents;
        private readonly List<Goal> _goals;
        private IDecayStrategy _decayStrategy;
        private long _lastMillis;
        private long _millisPassed;

        public Gamygdala(IDecayStrategy decayStrategy)
        {
            _agents = new Dictionary<string, Agent>();
            _goals = new List<Goal>();
            _decayStrategy = decayStrategy;
            _lastMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _millisPassed = 0L;
        }

        public double DecayFunction(DoubleZeroToOneInclusive intensity)
        {
            throw new NotImplementedException();
        }

        public void Appraise(Belief belief, Agent agent)
        {
            throw new NotImplementedException();
        }

        public void StartDecay()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Defines the change in a goal's likelihood due to the congruence and likelihood of a current event. We cope with two
        ///     types of beliefs: incremental and absolute beliefs. Incremental beliefs have their likelihood added to the goal.
        ///     Absolute define the current likelihood of the goal. There are two types of goals: maintenance and achievement. If
        ///     an achievement goal (the default) is -1 or 1, we can't change it any more (unless externally and explicitly by
        ///     changing the goal's likelihood).
        /// </summary>
        /// <param name="goal"></param>
        /// <param name="congruence"></param>
        /// <param name="likelihood"></param>
        /// <param name="isIncremental"></param>
        /// <returns></returns>
        private static DoubleNegativeOneToPositiveOneInclusive CalculateDeltaLikelihood(Goal goal,
            DoubleNegativeOneToPositiveOneInclusive congruence, DoubleZeroToOneInclusive likelihood, bool isIncremental)
        {
            if (goal == null)
                throw new ArgumentNullException(nameof(goal));

            var oldLikelihood = goal.Likelihood;

            if (!goal.IsMaintenanceGoal && (oldLikelihood >= 1 || oldLikelihood <= -1))
                // Goal has already been achieved.
                return 0;

            DoubleZeroToOneInclusive newLikelihood;
            if (goal.HasCustomLikelihoodCalculation)
            {
                // If the goal has an associated function to calculate the likelihood that the goal is true, then use that function. 
                newLikelihood = goal.CustomLikelihoodCalculation();
            }
            else
            {
                // Otherwise, use the event encoded updates.
                if (isIncremental)
                {
                    var unclampedNewLikelihood = oldLikelihood + likelihood * congruence;
                    newLikelihood = DoubleZeroToOneInclusive.Clamp(unclampedNewLikelihood);
                }
                else
                {
                    var unclampedNewLikelihood = (congruence * likelihood + 1.0) / 2.0;
                    newLikelihood = DoubleZeroToOneInclusive.Clamp(unclampedNewLikelihood);
                }
            }

            goal.Likelihood = newLikelihood; // TODO - this function isn't pure because we are setting the likelihood.
            return newLikelihood - oldLikelihood;
        }

        /// <summary>
        ///     Evaluates the event in terms of internal emotions that do not need relations to exist, such as hope, fear, etc..
        /// </summary>
        /// <param name="utility"></param>
        /// <param name="deltaLikelihood"></param>
        /// <param name="likelihood"></param>
        /// <param name="agent"></param>
        private static void EvaluateInternalEmotion(DoubleNegativeOneToPositiveOneInclusive utility,
            DoubleZeroToOneInclusive deltaLikelihood, DoubleZeroToOneInclusive likelihood, Agent agent)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));

            var emotions = new List<string>();

            if (Math.Abs(likelihood - 1) < double.Epsilon) // likelihood == 1
            {
                if (utility >= 0)
                {
                    if (deltaLikelihood < 0.5) emotions.Add(EmotionNames.Satisfaction);

                    emotions.Add(EmotionNames.Joy);
                }
                else
                {
                    if (deltaLikelihood < 0.5) emotions.Add(EmotionNames.FearConfirmed);

                    emotions.Add(EmotionNames.Distress);
                }
            }
            else if (likelihood < double.Epsilon) // likelihood == 0
            {
                if (utility >= 0)
                {
                    if (deltaLikelihood < 0.5) emotions.Add(EmotionNames.Disappointment);

                    emotions.Add(EmotionNames.Distress);
                }
                else
                {
                    if (deltaLikelihood < 0.5) emotions.Add(EmotionNames.Relief);

                    emotions.Add(EmotionNames.Joy);
                }
            }
            else // 0 < likelihood < 1
            {
                var isPositive = utility >= 0
                    ? deltaLikelihood >= 0
                    : deltaLikelihood < 0;
                emotions.Add(isPositive ? EmotionNames.Hope : EmotionNames.Fear);
            }

            var intensity = Math.Abs(utility * deltaLikelihood);
            if (intensity < double.Epsilon)
                return;

            foreach (var emotion in emotions)
                agent.UpdateEmotionalState(new Emotion(emotion, intensity));
        }

        /// <summary>
        ///     Used to evaluate the happy-for, pity, gloating, or resentment emotions that arise when we evaluate events that
        ///     affect goals of others.
        /// </summary>
        /// <param name="utility"></param>
        /// <param name="desirability">The desirability from the goal owner's perspective.</param>
        /// <param name="deltaLikelihood">
        ///     The change in a goal's likelihood due to the congruence and likelihood of a current
        ///     belief (event).
        /// </param>
        /// <param name="relation">Relation between the agent being evaluated and the goal owner of the affected goal.</param>
        /// <param name="agent">The agent getting evaluated (the agent that gets the social emotion added to its emotional state).</param>
        private static void EvaluateSocialEmotion(DoubleNegativeOneToPositiveOneInclusive utility,
            DoubleNegativeOneToPositiveOneInclusive desirability,
            DoubleNegativeOneToPositiveOneInclusive deltaLikelihood,
            Relation relation, Agent agent)
        {
            if (relation == null)
                throw new ArgumentNullException(nameof(relation));

            if (agent == null)
                throw new ArgumentNullException(nameof(agent));

            var emotionName = desirability >= 0
                ? relation.Like >= 0
                    ? EmotionNames.HappyFor
                    : EmotionNames.Resentment
                : relation.Like >= 0
                    ? EmotionNames.Pity
                    : EmotionNames.Gloating;

            var intensity = Math.Abs(utility * deltaLikelihood * relation.Like);
            if (intensity < double.Epsilon)
                return;

            var emotion = new Emotion(emotionName, intensity);
            relation.AddEmotion(emotion);
            agent.UpdateEmotionalState(emotion); // Also add relation emotion the emotion to the emotional state.
        }

        private void AgentActions(string affectedAgentName, string causalAgentName, string selfName,
            DoubleNegativeOneToPositiveOneInclusive desirability, DoubleNegativeOneToPositiveOneInclusive utility,
            DoubleNegativeOneToPositiveOneInclusive deltaLikelihood)
        {
            if (string.IsNullOrEmpty(affectedAgentName))
                throw new ArgumentException($"{nameof(affectedAgentName)} cannot be null or empty.");

            if (string.IsNullOrEmpty(selfName))
                throw new ArgumentException($"{nameof(selfName)} cannot be null or empty.");

            if (string.IsNullOrEmpty(causalAgentName))
                // If the causal agent is null or empty, then we we assume the event was not caused by an agent.
                return;

            // There are three cases here:
            // Case 1: The affected agent is SELF and causal agent is OTHER.
            // Case 2: The affected agent is SELF and causal agent is SELF.
            // Case 3: The affected agent is OTHER and causal agent is SELF.
            if (affectedAgentName.Equals(selfName) && !selfName.Equals(causalAgentName))
            {
                // Case 1
                var emotionName = desirability >= 0 ? EmotionNames.Gratitude : EmotionNames.Anger;
                var intensity = Math.Abs(utility * deltaLikelihood);
                var self = _agents[selfName]; // TODO - possible error here if selfName doesn't exist.

                if (!self.HasRelationWith(causalAgentName))
                    self.UpdateRelation(causalAgentName, 0);

                if (!self.TryGetRelation(causalAgentName, out var relation))
                    throw new InvalidOperationException(); // This should never happen.

                var emotion = new Emotion(emotionName, intensity);
                relation.AddEmotion(emotion);
                self.UpdateEmotionalState(emotion);
            }
            else if (affectedAgentName.Equals(selfName) && selfName.Equals(causalAgentName))
            {
                // Case 2
                // This case is not yet included in Gamygdala.
                // This should include pride and shame
            }
            else if (!affectedAgentName.Equals(selfName) && causalAgentName.Equals(selfName))
            {
                // Case 3
                var causalAgent =
                    _agents[causalAgentName]; // TODO - possible error here if causalAgentName doesn't exist.

                if (!causalAgent.TryGetRelation(affectedAgentName, out var relation))
                    return;

                if (relation.Like < 0)
                    return;

                var emotionName = desirability >= 0
                    ? EmotionNames.Gratification
                    : EmotionNames.Remorse;

                var intensity = Math.Abs(utility * deltaLikelihood * relation.Like);
                var emotion = new Emotion(emotionName, intensity);
                causalAgent.UpdateEmotionalState(emotion);
            }
            else
            {
                // We should never reach this.
                throw new NotImplementedException();
            }
        }
    }
}