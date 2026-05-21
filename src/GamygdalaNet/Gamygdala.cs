using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GamygdalaNet.Agents;
using GamygdalaNet.Agents.Data;
using GamygdalaNet.DecayStrategies;
using GamygdalaNet.RelationLikeStrategies;
using GamygdalaNet.Types;

namespace GamygdalaNet
{
    public static class StringHelpers
    {
        public static string Join(this IEnumerable<string> items, string separator)
        {
            return string.Join(separator, items);
        }

        public static void AppendLineTo(this string str, StringBuilder sb)
        {
            sb.AppendLine(str);
        }
    }

    public partial class Gamygdala
    {
        private void Log(Relation relation)
        {
            if (PrintDebug) Debug.WriteLine(relation.ToString());
        }

        private void Log(Belief belief)
        {
            if (PrintDebug) Debug.WriteLine(belief.ToString());
        }

        private void Log(string message)
        {
            if (PrintDebug) Debug.WriteLine(message);
        }

        private void LogAllEmotions(bool useGain = false)
        {
            if (!PrintDebug)
                return;

            var sb = new StringBuilder();

            foreach (var agent in _agents)
            {
                sb.Append($"{agent.Value.Name} feels ");
                agent.Value
                    .GetEmotionalState(useGain)
                    .Select(emotion => $"{emotion.Name}:{emotion.Intensity.Value:0.00}")
                    .Join(", ")
                    .AppendLineTo(sb);

                sb.AppendLine($"{agent.Value.Name} has the following sentiments:");
                var relations = agent.Value.GetRelations();
                relations
                    .Select(relation =>
                    {
                        var emotions = relation
                            .EmotionsList
                            .Select(emotion => $"{emotion.Name}({emotion.Intensity.Value:0.00})")
                            .Join(", ");

                        return $"{emotions} for {relation.TargetAgentName}";
                    })
                    .Join(", and \n")
                    .AppendLineTo(sb);
            }
        }
    }

    public partial class Gamygdala
    {
        private readonly Dictionary<string, Agent> _agents;
        private readonly IDecayStrategy _decayStrategy;
        private readonly Dictionary<string, Goal> _goals;
        private readonly IRelationLikeStrategy _relationLikeStrategy;
        private long _lastMillis;
        private long _millisPassed;

        /// <summary>
        /// </summary>
        /// <param name="decayStrategy"></param>
        /// <param name="relationLikeStrategy"></param>
        public Gamygdala(IDecayStrategy decayStrategy, IRelationLikeStrategy relationLikeStrategy)
        {
            _agents = new Dictionary<string, Agent>();
            _goals = new Dictionary<string, Goal>();
            _decayStrategy = decayStrategy;
            _relationLikeStrategy = relationLikeStrategy;
            _lastMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _millisPassed = 0L;
        }

        public bool PrintDebug { get; set; }

        public double DecayFunction(EmotionIntensity intensity)
        {
            return _decayStrategy.Decay(intensity, _millisPassed);
        }

        /// <summary>
        ///     Creates a new <see cref="Agent" /> and registers it
        ///     with this engine. Returns the existing instance if an
        ///     agent with the same name is already registered.
        /// </summary>
        /// <param name="agentName">Identifier for the new agent.</param>
        /// <returns>The newly created (or pre-existing) agent.</returns>
        public Agent CreateAgent(string agentName)
        {
            if (_agents.TryGetValue(agentName, out var agent))
            {
                Log($"Warning: Agent with name \"{agentName}\" already exists, so returning it.");
                return agent;
            }

            agent = new Agent(agentName);
            RegisterAgent(agent);
            return agent;
        }

        /// <summary>
        ///     Creates a <see cref="Goal" />, registers it with the
        ///     engine, and adds it to the named agent. Fine when each
        ///     goal has a single owner registered up front. For
        ///     multi-owner goals, or anything that needs control over
        ///     the goal's lifecycle, build the <see cref="Goal" />
        ///     yourself and call <see cref="RegisterGoal" /> plus
        ///     <see cref="Agent.AddGoal" /> directly.
        /// </summary>
        /// <param name="agentName">Name of the agent that owns the new goal.</param>
        /// <param name="goalName">Identifier for the new goal.</param>
        /// <param name="goalUtility">The goal's utility in [-1, +1].</param>
        /// <param name="isMaintenanceGoal">
        ///     False (default) for an achievement goal, which settles once its stored likelihood reaches 0 or 1.
        ///     True for a maintenance goal, which can cycle indefinitely.
        /// </param>
        /// <returns>The newly created goal.</returns>
        public Goal CreateGoalForAgent(string agentName, string goalName,
            GoalUtility goalUtility, bool isMaintenanceGoal = false)
        {
            if (_agents.TryGetValue(agentName, out var agent))
            {
                if (_goals.TryGetValue(goalName, out var goal))
                {
                    Log(
                        $"Warning: I cannot make a new goal with the same name {goalName} as one is registered" +
                        " already. I assume the goal is a common goal and will add the already known goal with that" +
                        $" name to the agent {agentName}. If isMaintenanceGoal is different between the two goals, I" +
                        " will defer to the existing goal.");
                }
                else
                {
                    goal = new Goal(goalName, goalUtility, isMaintenanceGoal);
                    RegisterGoal(goal);
                }

                agent.AddGoal(goal);
                return goal;
            }

            Log($"Error: agent with name \"{agentName}\" does not exist, so I cannot create a goal for it.");
            return null;
        }

        /// <summary>
        ///     Sets a relation from <paramref name="sourceAgentName" />
        ///     to <paramref name="targetAgentName" />. Both agents must
        ///     already be registered with this engine; if either name
        ///     is unknown, logs a warning and returns without changing
        ///     state.
        /// </summary>
        /// <param name="sourceAgentName">The agent that holds the relation.</param>
        /// <param name="targetAgentName">The agent that the relation points at.</param>
        /// <param name="relation">How strongly the source likes the target, from -1 (disliked) to +1 (liked).</param>
        public void CreateRelation(string sourceAgentName, string targetAgentName,
            RelationLike relation)
        {
            if (_agents.TryGetValue(sourceAgentName, out var source)
                && _agents.TryGetValue(targetAgentName, out var target))
                source.UpdateRelation(targetAgentName, relation);
            else
                Log(
                    $"Error: cannot relate {sourceAgentName} to {targetAgentName} with intensity {relation.Value:0.00}");
        }

        /// <summary>
        ///     Constructs a <see cref="Belief" /> from the supplied
        ///     arguments and appraises it for every registered agent.
        ///     Equivalent to <c>Appraise(new Belief(...))</c>; provided
        ///     so callers can keep all Gamygdala interaction internal
        ///     to this class without referencing <see cref="Belief" />
        ///     directly. See the <see cref="Belief" /> constructor for
        ///     the meaning of each argument.
        /// </summary>
        public void AppraiseBelief(Likelihood likelihood, string causalAgentName,
            string[] affectedGoalNames, GoalCongruence[] goalCongruences)
        {
            var tempBelief = new Belief(likelihood, causalAgentName, affectedGoalNames, goalCongruences);
            Appraise(tempBelief);
        }

        /// <summary>
        ///     For every entity in your game (usually NPCs, but can be the player character too) you have to first create an Agent
        ///     object and then register it using this method. Registering the agent makes sure that Gamygdala will be able to
        ///     emotionally interpret incoming Beliefs about the game state for that agent.
        /// </summary>
        /// <param name="agent"></param>
        /// <exception cref="ArgumentNullException"><see cref="agent" /> must not be null.</exception>
        public void RegisterAgent(Agent agent)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));

            if (_agents.ContainsKey(agent.Name))
                Log($"Warning: failed adding an agent with the same name: {agent.Name}");
            else
                _agents[agent.Name] = agent;
        }

        /// <summary>
        ///     For every goal that NPCs or player characters can have, you have to first create a Goal object and then register it
        ///     using this method. Registering the goals makes sure that Gamygdala will be able to find the correct goal references
        ///     when a Beliefs about the game state comes in.
        /// </summary>
        /// <param name="goal">The goal to be registered.</param>
        /// <exception cref="ArgumentNullException"><see cref="goal" /> must not be null.</exception>
        public void RegisterGoal(Goal goal)
        {
            if (goal == null)
                throw new ArgumentNullException(nameof(goal));

            if (_goals.ContainsKey(goal.Name))
                Log($"Warning: failed adding a second goal with the same name: {goal.Name}");
            else
                _goals[goal.Name] = goal;
        }

        /// <summary>
        ///     This method is the main emotional interpretation logic entry point. It performs the complete appraisal of a single
        ///     event (belief) for all agents (if affectedAgent==null) or for only one agent (affectedAgent!=null). If
        ///     affectedAgent is set, then the complete appraisal logic is executed including the effect on relations (possibly
        ///     influencing the emotional state of other agents), but only if the affected agent (the one owning the goal) is the
        ///     affectedAgent. Per-agent appraisal is sometimes needed for efficiency. If you as a game developer know that
        ///     particular agents can never appraise an event, then you can force Gamygdala to only look at a subset of agents.
        ///     Gamygdala assumes that the affectedAgent is indeed the only goal owner affected, that the belief is well-formed,
        ///     and will not perform any checks, nor use Gamygdala's list of known goals to find other agents that share this goal.
        /// </summary>
        /// <param name="belief">The current event, in the form of a Belief, to be appraised.</param>
        /// <param name="affectedAgent">
        ///     The reference to the agent who needs to appraise the event. If given, this is the appraisal
        ///     perspective (see explanation in method summary).
        /// </param>
        /// <returns></returns>
        public bool Appraise(Belief belief, Agent affectedAgent = null)
        {
            if (affectedAgent == null)
            {
                Log(belief);

                if (_goals.Count == 0)
                {
                    Log(
                        "Warning: no goals registered to Gamygdala, all goals to be considered in appraisal need to be registered.");
                    return false;
                }

                for (var i = 0; i < belief.AffectedGoalNames.Length; i++)
                {
                    var affectedGoalName = belief.AffectedGoalNames[i];
                    if (!_goals.ContainsKey(affectedGoalName))
                        continue;
                    var goalDefinition = _goals[affectedGoalName];
                    var utility = goalDefinition.Utility;

                    // Now find the owners, and update their emotional states.
                    // Each owner gets its OWN deltaLikelihood and post-appraisal
                    // likelihood because the likelihood is per-agent state on
                    // each owner; the engine's _goals dictionary is the goal
                    // definition registry, not a likelihood store.
                    var agentsWithGoal = _agents
                        .Where(x => x.Value.HasGoal(goalDefinition.Name))
                        .Select(x => x.Value);

                    foreach (var owner in agentsWithGoal)
                    {
                        Log($"...owned by {owner.Name}");
                        var hasPrior = owner.TryGetGoalLikelihood(goalDefinition.Name,
                            out var ownerPriorLikelihood);
                        var (newLikelihood, deltaLikelihood) = CalculateDeltaLikelihood(
                            goalDefinition, ownerPriorLikelihood, hasPrior,
                            belief.GoalCongruences[i], belief.Likelihood);
                        owner.SetGoalLikelihood(goalDefinition.Name, newLikelihood);
                        var desirability = deltaLikelihood * utility;
                        Log(
                            $"Evaluated goal: {goalDefinition.Name}(u={utility.Value:0.00},dL={deltaLikelihood.Value:0.00})");

                        EvaluateInternalEmotion(utility, deltaLikelihood, newLikelihood, owner);
                        AgentActions(owner.Name, belief.CausalAgentName, owner.Name, desirability, utility,
                            deltaLikelihood);
                        // Now check if anyone has a relation to this goal owner, and update the social emotions accordingly.
                        foreach (var agent in _agents)
                            if (agent.Value.TryGetRelation(owner.Name, out var relation))
                            {
                                Log($"{agent.Value.Name} has a relationship with {owner.Name}");
                                Log(relation);
                                // The agent has relationship with the goal owner which has nonzero utility, so add relational effects to the relations for agent.
                                EvaluateSocialEmotion(utility, desirability, deltaLikelihood, relation, agent.Value);
                                // Also add remorse and gratification if conditions are met within (i.e., agent did something bad/good for owner)
                                AgentActions(owner.Name, belief.CausalAgentName, agent.Value.Name, desirability,
                                    utility, deltaLikelihood);
                            }
                            else
                            {
                                Log($"{agent.Value.Name} has NO relationship with {owner.Name}");
                            }
                    }
                }
            }
            else // TODO - refactor this since there is repeat code.
            {
                // Check only affectedAgent (which can be much faster). Read
                // the goal definition from the affected agent's private
                // template store via TryGetGoal so per-agent goal state
                // (likelihood) does not leak to other agents through the
                // engine-global _goals registry.
                for (var i = 0; i < belief.AffectedGoalNames.Length; i++)
                {
                    var affectedGoalName = belief.AffectedGoalNames[i];
                    if (!affectedAgent.TryGetGoal(affectedGoalName, out var goalDefinition))
                        continue;
                    var hasPrior = affectedAgent.TryGetGoalLikelihood(goalDefinition.Name,
                        out var ownerPriorLikelihood);
                    var (newLikelihood, deltaLikelihood) = CalculateDeltaLikelihood(
                        goalDefinition, ownerPriorLikelihood, hasPrior,
                        belief.GoalCongruences[i], belief.Likelihood);
                    affectedAgent.SetGoalLikelihood(goalDefinition.Name, newLikelihood);
                    var utility = goalDefinition.Utility;
                    var desirability = deltaLikelihood * utility;

                    var owner = affectedAgent;

                    EvaluateInternalEmotion(utility, deltaLikelihood, newLikelihood, owner);
                    AgentActions(owner.Name, belief.CausalAgentName, owner.Name, desirability, utility,
                        deltaLikelihood);

                    // Now check if anyone has a relation to this goal owner, and update the social emotions accordingly.
                    foreach (var agent in _agents)
                        if (agent.Value.TryGetRelation(owner.Name, out var relation))
                        {
                            Log($"{agent.Value.Name} has a relationship with {owner.Name}");
                            Log(relation);
                            // The agent has relationship with the goal owner which has nonzero utility, so add relational effects to the relations for agent.
                            EvaluateSocialEmotion(utility, desirability, deltaLikelihood, relation, agent.Value);
                            // Also add remorse and gratification if conditions are met within (i.e., agent did something bad/good for owner)
                            AgentActions(owner.Name, belief.CausalAgentName, agent.Value.Name, desirability,
                                utility, deltaLikelihood);
                        }
                        else
                        {
                            Log($"{agent.Value.Name} has NO relationship with {owner.Name}");
                        }
                }
            }

            LogAllEmotions();

            return true;
        }

        /// <summary>
        ///     Decays the emotional state and relations for all registered agents. The caller is responsible for invoking
        ///     this on a cadence appropriate to the game (e.g. once per game-loop tick or once per Update). The function
        ///     tracks millis-since-last-call so the decay rate stays close to the configured factor regardless of how
        ///     frequently you call it. Tweak the decay strength via the decay strategy's factor, not the call frequency.
        /// </summary>
        public void DecayAll(long? millisPassed = null)
        {
            // TODO - turn into pure function?

            if (millisPassed.HasValue)
            {
                // Not in the original code. Hack to get dT for deterministic unit tests. 
                _millisPassed = millisPassed.Value;
                _lastMillis = _lastMillis + _millisPassed;
            }
            else
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _millisPassed = now - _lastMillis;
                _lastMillis = now;
            }

            foreach (var agent in _agents) agent.Value.Decay(this);
        }

        /// <summary>
        ///     Pure function: computes the new likelihood for the
        ///     goal via equation 2 and the delta from the agent's
        ///     prior state. The caller writes the new value back via
        ///     <see cref="Agent.SetGoalLikelihood" />.
        ///     <para>
        ///         Equation 2: <c>newLikelihood = (congruence * likelihood + 1) / 2</c>.
        ///         Achievement goals at the 0 or 1 boundary stop
        ///         moving; maintenance goals keep accepting updates.
        ///         A goal with a custom likelihood calculation bypasses
        ///         equation 2 and uses its supplied function instead.
        ///     </para>
        ///     <para>
        ///         Per Popescu §3.4.2 the initial likelihood is
        ///         "Unknown". When <paramref name="hasPriorLikelihood" />
        ///         is false the returned delta IS the post-appraisal
        ///         likelihood (the magnitudes in Listings 1, 3, 4 only
        ///         line up under this convention). Once a prior is
        ///         recorded the delta is the usual
        ///         <c>newLikelihood - oldLikelihood</c>.
        ///     </para>
        ///     Belief.likelihood at 0 or 1 snaps the STORED likelihood
        ///     to that certainty boundary, but the returned delta
        ///     still uses the unsnapped formula result so the
        ///     listings' intensities match exactly.
        /// </summary>
        /// <param name="goal">The goal whose likelihood is being updated.</param>
        /// <param name="oldLikelihood">The agent's prior likelihood for the goal (zero when <paramref name="hasPriorLikelihood"/> is false).</param>
        /// <param name="hasPriorLikelihood">True if the agent has already recorded a likelihood for this goal.</param>
        /// <param name="congruence">How strongly the belief facilitates (positive) or blocks (negative) the goal.</param>
        /// <param name="likelihood">The belief's certainty.</param>
        /// <returns>The new likelihood to store, and the delta against the prior.</returns>
        private static (Likelihood newLikelihood, DoubleNegativeOneToPositiveOneInclusive delta)
            CalculateDeltaLikelihood(
                Goal goal,
                Likelihood oldLikelihood,
                bool hasPriorLikelihood,
                GoalCongruence congruence,
                Likelihood likelihood)
        {
            if (goal == null)
                throw new ArgumentNullException(nameof(goal));

            // Achievement goals settle at either boundary. The
            // paper's §3.2 framing of "achieved or permanently failed"
            // uses signed-likelihood notation where the failure
            // boundary is -1; this port's Likelihood domain is [0, 1]
            // (equation 2's natural output range), so the failure
            // boundary maps to 0. Once an achievement goal is at
            // either boundary, subsequent beliefs do not move it.
            // Maintenance goals are exempt and can cycle indefinitely.
            if (!goal.IsMaintenanceGoal && hasPriorLikelihood
                                        && (oldLikelihood >= 1 || oldLikelihood <= 0))
                return (oldLikelihood,
                    new DoubleNegativeOneToPositiveOneInclusive(0));

            Likelihood newLikelihood;
            if (goal.HasCustomLikelihoodCalculation)
            {
                // If the goal has an associated function to calculate the likelihood that the goal is true, then use that function.
                newLikelihood = goal.CustomLikelihoodCalculation();
            }
            else
            {
                var unclampedNewLikelihood = (congruence * likelihood + 1.0) / 2.0;
                newLikelihood = Likelihood.Clamp(unclampedNewLikelihood);
            }

            // Paper-aligned saturation snap (Popescu §3.4.2 + Listings
            // 1, 3). When the belief is at certainty (likelihood == 0
            // or 1) AND the belief actually affects this goal
            // (congruence != 0), the STORED likelihood saturates to
            // whichever boundary the congruence sign points at:
            //   certain-happening + facilitating  → goal achieves (1)
            //   certain-happening + blocking      → goal disconfirmed (0)
            //   certain-not-happening + facilitating → goal disconfirmed (0)
            //   certain-not-happening + blocking    → goal achieves (1)
            // Two exemptions short-circuit the snap:
            //  - congruence == 0: the belief is unrelated to this
            //    goal, so equation 2's natural 0.5 ("Unknown") stands.
            //  - HasCustomLikelihoodCalculation: a custom calc replaces
            //    equation 2 entirely, so its return value wins over
            //    the snap.
            // The returned delta still uses the UNSNAPPED newLikelihood
            // from the formula above; the paper's Listings 1, 3, 4 pin
            // intensities that only match when the delta is computed
            // against the unsnapped value, then stored as snapped.
            var storedLikelihood = newLikelihood;
            var beliefIsCertainOfHappening = Math.Abs(likelihood - 1) < double.Epsilon;
            var beliefIsCertainOfNonHappening = likelihood < double.Epsilon;
            var beliefAffectsThisGoal = Math.Abs(congruence) > double.Epsilon;
            if (!goal.HasCustomLikelihoodCalculation && beliefAffectsThisGoal &&
                (beliefIsCertainOfHappening || beliefIsCertainOfNonHappening))
            {
                var goalSaturatesToAchieved =
                    (beliefIsCertainOfHappening && congruence > 0) ||
                    (beliefIsCertainOfNonHappening && congruence < 0);
                storedLikelihood = goalSaturatesToAchieved ? new Likelihood(1) : new Likelihood(0);
            }

            var delta = !hasPriorLikelihood
                ? new DoubleNegativeOneToPositiveOneInclusive(newLikelihood)
                : new DoubleNegativeOneToPositiveOneInclusive(newLikelihood - oldLikelihood);
            return (storedLikelihood, delta);
        }

        /// <summary>
        ///     Evaluates the event in terms of internal emotions that do not need relations to exist, such as hope, fear, etc..
        /// </summary>
        /// <param name="utility"></param>
        /// <param name="deltaLikelihood"></param>
        /// <param name="likelihood"></param>
        /// <param name="agent"></param>
        private static void EvaluateInternalEmotion(GoalUtility utility,
            DoubleNegativeOneToPositiveOneInclusive deltaLikelihood, Likelihood likelihood, Agent agent)
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
        private static void EvaluateSocialEmotion(GoalUtility utility,
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
            agent.UpdateEmotionalState(emotion); // Also add relation emotion to the emotional state.
        }

        private void AgentActions(string affectedAgentName, string causalAgentName, string selfName,
            DoubleNegativeOneToPositiveOneInclusive desirability, GoalUtility utility,
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

                _relationLikeStrategy.UpdateRelation(self, causalAgentName, new RelationLike(desirability));

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