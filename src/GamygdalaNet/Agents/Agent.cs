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
        private readonly Dictionary<string, EmotionIntensity> _defaultEmotionalState;
        private readonly Dictionary<string, Goal> _goals;
        private readonly Dictionary<string, double> _goalLikelihoods;
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
            _goalLikelihoods = new Dictionary<string, double>();
            _currentRelations = new Dictionary<string, Relation>();
            _internalState = new Dictionary<string, Emotion>();
            _defaultEmotionalState = new Dictionary<string, EmotionIntensity>();
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
        /// <param name="goalName">The name of the goal to remove.</param>
        /// <returns>True if the goal was removed, false if the agent did not own it.</returns>
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
        ///     Looks up the agent's per-name <see cref="Goal" /> definition.
        ///     Used by <see cref="Gamygdala.Appraise(Belief, Agent)" /> to
        ///     read the agent's private goal template before consulting
        ///     <see cref="GetGoalLikelihood" /> for the current state.
        /// </summary>
        public bool TryGetGoal(string goalName, out Goal goal)
        {
            return _goals.TryGetValue(goalName, out goal);
        }

        /// <summary>
        ///     Returns the agent's recorded likelihood for the named
        ///     goal, or false if no appraisal against the goal has
        ///     run yet. Per Popescu §3.4.2 the initial value is
        ///     "Unknown", not a numeric prior; on first appraisal
        ///     the delta IS the post-appraisal likelihood (the
        ///     magnitudes in Listings 1, 3, 4 only line up under
        ///     this convention).
        /// </summary>
        public bool TryGetGoalLikelihood(string goalName, out Likelihood likelihood)
        {
            if (string.IsNullOrEmpty(goalName))
                throw new ArgumentException($"{nameof(goalName)} cannot be null or empty.");

            if (_goalLikelihoods.TryGetValue(goalName, out var value))
            {
                likelihood = new Likelihood(value);
                return true;
            }

            likelihood = new Likelihood(0);
            return false;
        }

        /// <summary>
        ///     Records the agent's current likelihood for the named
        ///     goal. Called by
        ///     <see cref="Gamygdala.Appraise(Belief, Agent)" /> after
        ///     calculating the per-belief delta. After this call
        ///     <see cref="HasGoalLikelihood" /> returns true and
        ///     <see cref="TryGetGoalLikelihood" /> recovers the
        ///     stored value.
        /// </summary>
        public void SetGoalLikelihood(string goalName, Likelihood likelihood)
        {
            if (string.IsNullOrEmpty(goalName))
                throw new ArgumentException($"{nameof(goalName)} cannot be null or empty.");

            _goalLikelihoods[goalName] = likelihood;
        }

        /// <summary>
        ///     True when the agent has recorded any likelihood for
        ///     the named goal. Distinguishes "never appraised"
        ///     (returns false) from "appraised, current value happens
        ///     to be 0.5" (returns true). Existence-based via the
        ///     underlying dictionary; no NaN sentinels.
        /// </summary>
        public bool HasGoalLikelihood(string goalName)
        {
            return _goalLikelihoods.ContainsKey(goalName);
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
        ///     Appraises a single belief for this agent. Same as
        ///     <c>gamygdala.Appraise(belief, this)</c>; saves you
        ///     from threading the engine back through.
        /// </summary>
        /// <param name="belief">The belief to appraise.</param>
        /// <param name="gamygdala">The engine to appraise against; different engine instances can manage different agent populations.</param>
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
        ///     Sets the agent's resting intensity for a named emotion.
        ///     Per Popescu §3.4.6 the NPC's emotional state decays
        ///     toward this default rather than toward zero. Defaults
        ///     stay opt-in: an unset emotion decays to zero (the
        ///     historical behavior). The substrate accepts the default
        ///     but does not compute it; consumers wire personality
        ///     models (e.g. OCEAN via AlmaNet) into this seam.
        ///     Setting a default also seeds the current intensity at
        ///     that value if the emotion is not already in the agent's
        ///     state, so a freshly constructed agent reads its
        ///     defaults out of <see cref="GetEmotionalState" /> before
        ///     any appraisal has run.
        /// </summary>
        public void SetDefaultEmotionIntensity(string emotionName, EmotionIntensity defaultIntensity)
        {
            if (string.IsNullOrEmpty(emotionName))
                throw new ArgumentException($"{nameof(emotionName)} cannot be null or empty.");

            _defaultEmotionalState[emotionName] = defaultIntensity;
            if (!_internalState.ContainsKey(emotionName))
                _internalState[emotionName] = new Emotion(emotionName, defaultIntensity);
        }

        /// <summary>
        ///     Reads the agent's resting intensity for a named
        ///     emotion. Returns false when no default has been set
        ///     (the emotion decays to zero in that case).
        /// </summary>
        public bool TryGetDefaultEmotionIntensity(string emotionName, out EmotionIntensity defaultIntensity)
        {
            return _defaultEmotionalState.TryGetValue(emotionName, out defaultIntensity);
        }

        /// <summary>
        ///     Returns the agent's currently held emotions. When
        ///     <paramref name="useGain" /> is true the raw intensities
        ///     go through a <c>g·x / (g·x + 1)</c> compression; tune
        ///     <c>g</c> with <see cref="SetGain" />. High gain makes
        ///     small or rare appraisals more visible; low gain
        ///     dampens large or frequent ones. Gain is a port-specific
        ///     extension; leave <paramref name="useGain" /> false for
        ///     paper-faithful output.
        /// </summary>
        /// <param name="useGain">If true, apply the gain compression to each emotion's intensity.</param>
        /// <returns>The agent's emotions and their intensities.</returns>
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
        ///     Computes the Pleasure-Arousal-Dominance mapping of the
        ///     agent's emotional state via Reilly's logarithmic
        ///     aggregator (equation 5 from Popescu, Broekens, van
        ///     Someren, "GAMYGDALA: An Emotion Engine for Games"):
        ///     <code>PAD ← 0.1 × log₂(Σ_e 2^(10 × PAD(e) × intensity(e)))</code>
        ///     Per §3.4.6 this aggregator is not strictly additive,
        ///     uses all active emotions rather than only the strongest,
        ///     and stays at least as intense as the most intense
        ///     contributor. A defensive clamp to <c>[-1, +1]</c> keeps
        ///     <see cref="PadState" /> construction safe when multiple
        ///     same-polarity contributors push the result slightly
        ///     outside the natural range.
        ///     The optional <paramref name="useGain" /> applies the
        ///     classical Gamygdala gain compression on top, useful
        ///     when the consumer wants additional dampening for
        ///     high-frequency appraisal scenarios.
        /// </summary>
        /// <param name="useGain">Whether to apply the optional gain function on top of the log-sum-exp aggregation.</param>
        /// <returns>The calculated PAD state.</returns>
        public PadState GetPadState(bool useGain = false)
        {
            if (_internalState.Count == 0) return new PadState(0.0, 0.0, 0.0);

            var sumPleasure = 0.0;
            var sumArousal = 0.0;
            var sumDominance = 0.0;
            foreach (var emotion in _internalState)
            {
                var intensity = emotion.Value.Intensity.Value;
                var pad = MapPad[emotion.Key];
                sumPleasure += System.Math.Pow(2.0, 10.0 * pad.Pleasure * intensity);
                sumArousal += System.Math.Pow(2.0, 10.0 * pad.Arousal * intensity);
                sumDominance += System.Math.Pow(2.0, 10.0 * pad.Dominance * intensity);
            }

            // Equation 5 outer transform. Empty pool guarded above so
            // the log argument is always positive. Math.Log(x, 2.0)
            // is the netstandard2.0-compatible spelling of log2.
            var pleasure = 0.1 * System.Math.Log(sumPleasure, 2.0);
            var arousal = 0.1 * System.Math.Log(sumArousal, 2.0);
            var dominance = 0.1 * System.Math.Log(sumDominance, 2.0);

            if (useGain)
            {
                double ApplyGain(double value)
                {
                    return value > 0
                        ? _gain * value / (_gain * value + 1)
                        : -_gain * value / (_gain * value - 1);
                }

                pleasure = ApplyGain(pleasure);
                arousal = ApplyGain(arousal);
                dominance = ApplyGain(dominance);
            }

            // PadState's domain type rejects values outside [-1, +1];
            // clamp here so the natural log-sum-exp overshoot from
            // multiple high-intensity contributors doesn't throw on
            // construction.
            return new PadState(
                Clamp(pleasure),
                Clamp(arousal),
                Clamp(dominance));
        }

        private static double Clamp(double value)
        {
            // Math.Clamp is .NET Standard 2.1+; this method targets 2.0.
            if (value < -1.0) return -1.0;
            if (value > 1.0) return 1.0;
            return value;
        }

        /// <summary>
        ///     Sets the relation this agent has with the target agent. If the relation does not exist, it will be created,
        ///     otherwise it will be updated.
        /// </summary>
        /// <param name="targetAgentName">The agent who is the target of the relation.</param>
        /// <param name="like">The relation, or how much the target agent is liked, from -1 (disliked) to 1 (liked).</param>
        /// <param name="isLikeAdditive">If true, <see cref="like" /> is added to the existing relation's like value.</param>
        public void UpdateRelation(string targetAgentName, RelationLike like,
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
                RelationLike finalLike = isLikeAdditive
                    ? new RelationLike(Math.Min(1, Math.Max(-1, initialLike + like)))
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
        ///     Decays this agent's emotional state and relations
        ///     according to the decay strategy on the supplied
        ///     <paramref name="gamygdala" /> instance. The caller
        ///     drives the cadence; the engine does not start its own
        ///     decay loop. <see cref="Gamygdala.DecayAll" /> applies
        ///     this to every registered agent.
        /// </summary>
        /// <param name="gamygdala">
        ///     A reference to the gamygdala instance with the desired decay function, so you could use
        ///     different gamygdala instances to manage different groups of agents.
        /// </param>
        public void Decay(Gamygdala gamygdala)
        {
            _internalState.Decay(gamygdala, _defaultEmotionalState);
            foreach (var relation in _currentRelations) relation.Value.Decay(gamygdala);
            DecayGoalLikelihoods();
        }

        /// <summary>
        ///     Glides each stored goal likelihood back toward the
        ///     "Unknown" prior (0.5) by the goal's
        ///     <see cref="Goal.LikelihoodDecayRate" />. Goals without a
        ///     decay rate (the default) are left alone. Models the
        ///     intuition that social-needs goals like "be liked" or
        ///     "be respected" require ongoing reinforcement: a single
        ///     positive confirmation should not lock the goal at 1.0
        ///     forever. Port-specific extension; the Popescu paper
        ///     does not specify likelihood decay.
        /// </summary>
        private void DecayGoalLikelihoods()
        {
            // Snapshot keys so the in-place mutation below does not
            // invalidate enumeration.
            var keys = _goalLikelihoods.Keys.ToArray();
            foreach (var goalName in keys)
            {
                if (!_goals.TryGetValue(goalName, out var goal)) continue;
                var rate = goal.LikelihoodDecayRate;
                if (rate <= 0) continue;
                var current = _goalLikelihoods[goalName];
                const double UnknownPrior = 0.5;
                _goalLikelihoods[goalName] = current + rate * (UnknownPrior - current);
            }
        }

        public Relation[] GetRelations()
        {
            return _currentRelations.Values.ToArray();
        }
    }
}