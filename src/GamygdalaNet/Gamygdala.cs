using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GamygdalaNet.Agents;
using GamygdalaNet.Agents.Data;
using GamygdalaNet.DecayStrategies;
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
        private long _lastMillis;
        private long _millisPassed;

        /// <summary>
        /// </summary>
        /// <param name="decayStrategy"></param>
        public Gamygdala(IDecayStrategy decayStrategy)
        {
            _agents = new Dictionary<string, Agent>();
            _goals = new Dictionary<string, Goal>();
            _decayStrategy = decayStrategy;
            _lastMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _millisPassed = 0L;
        }

        public bool PrintDebug { get; set; }

        public double DecayFunction(DoubleZeroToOneInclusive intensity)
        {
            return _decayStrategy.Decay(intensity, _millisPassed);
        }

        /// <summary>
        ///     A facilitator method that creates a new Agent and registers it for you.
        /// </summary>
        /// <param name="agentName"></param>
        /// <returns>An agent reference to the newly created agent.</returns>
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
        ///     A facilitator method to create a goal for a particular agent, that also registers the goal to the agent and
        ///     gamygdala. This method is thus handy if you want to keep all gamygdala logic internal to Gamygdala. However, if you
        ///     want to do more sophisticated stuff (e.g., goals for multiple agents, keep track of your own list of goals to also
        ///     remove them, appraise events per agent without the need for gamygdala to keep track of goals, etc...) this method
        ///     will probably be doing too much.
        /// </summary>
        /// <param name="agentName">The agent's name to which the newly created goal has to be added.</param>
        /// <param name="goalName">The goal's name.</param>
        /// <param name="goalUtility">The goal's utility.</param>
        /// <param name="isMaintenanceGoal">
        ///     Defines if the goal is a maintenance goal or not [optional]. The default is that the
        ///     goal is an achievement goal, i.e., a goal that once it's likelihood reaches true (1) or false (-1) stays that way.
        /// </param>
        /// <returns>A goal reference to the newly created goal.</returns>
        public Goal CreateGoalForAgent(string agentName, string goalName,
            DoubleNegativeOneToPositiveOneInclusive goalUtility, bool isMaintenanceGoal = false)
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
        ///     A facilitator method to create a relation between two agents. Both source and target have to exist and be
        ///     registered with this Gamygdala instance. This method is thus handy if you want to keep all gamygdala logic internal
        ///     to Gamygdala.
        /// </summary>
        /// <param name="sourceAgentName">The agent who has the relation (the source)</param>
        /// <param name="targetAgentName">The agent who is the target of the relation (the target)</param>
        /// <param name="relation">The relation, or how much the target agent is liked, from -1 (disliked) to 1 (liked).</param>
        public void CreateRelation(string sourceAgentName, string targetAgentName,
            DoubleNegativeOneToPositiveOneInclusive relation)
        {
            if (_agents.TryGetValue(sourceAgentName, out var source)
                && _agents.TryGetValue(targetAgentName, out var target))
                source.UpdateRelation(targetAgentName, relation);
            else
                Log(
                    $"Error: cannot relate {sourceAgentName} to {targetAgentName} with intensity {relation.Value:0.00}");
        }

        /// <summary>
        ///     A facilitator method to appraise an event. It takes in the same as what the new Belief(...) takes in, creates a
        ///     belief and appraises it for all agents that are registered. This method is thus handy if you want to keep all
        ///     gamygdala logic internal to Gamygdala.
        /// </summary>
        /// <param name="likelihood">
        ///     The likelihood of this belief to be true, from 0 (disconfirmed) to 1 (confirmed).
        /// </param>
        /// <param name="causalAgentName">The agent's name of the causal agent of this belief.</param>
        /// <param name="affectedGoalNames">An array of affected goals' names to be copied.</param>
        /// <param name="goalCongruences">
        ///     An array of the affected goals' congruences (-1 to 1 inclusive) to be copied. The extent to which this
        ///     event is good (1) or bad (-1) for a goal.
        /// </param>
        /// <param name="isIncremental">
        ///     Incremental evidence enforces gamygdala to see this event as incremental evidence for (or
        ///     against) the list of goals provided, i.e, it will add or subtract this belief's likelihood*congruence
        ///     from the goal likelihood instead of using the belief as "state" defining the absolute likelihood.
        ///     Incremental evidence enforces gamygdala to use the likelihood as delta, i.e, it will add or subtract this belief's
        ///     likelihood from the goal likelihood instead of using the belief as "state" defining the absolute likelihood
        /// </param>
        public void AppraiseBelief(DoubleZeroToOneInclusive likelihood, string causalAgentName,
            string[] affectedGoalNames, DoubleNegativeOneToPositiveOneInclusive[] goalCongruences,
            bool isIncremental = false)
        {
            var tempBelief = new Belief(likelihood, causalAgentName, affectedGoalNames, goalCongruences, isIncremental);
            Appraise(tempBelief);
        }

        /// <summary>
        ///     Facilitator to set the gain for the whole set of agents known to Gamygdala. For more realistic, complex games, you
        ///     would typically set the gain for each agent type separately to fine-tune the intensity of the response.
        /// </summary>
        /// <param name="gain">The gain value, from 0 to 20 inclusive.</param>
        private void SetGain(Gain gain)
        {
            foreach (var agent in _agents) agent.Value.SetGain(gain);
        }

        public void StartDecay()
        {
            throw new NotImplementedException();
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
            // TODO - rename to TryAppraise.

            if (affectedAgent == null)
            {
                // Check all
                Log(belief);
                if (belief.GoalCongruences.Length != belief.AffectedGoalNames.Length
                ) // TODO - Should this be guaranteed by Belief?
                {
                    Log("Error: the congruence list was not of the same length as the affected goal list");
                    return false;
                }

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
                    var currentGoal = _goals[affectedGoalName];
                    var deltaLikelihood = CalculateDeltaLikelihood(currentGoal,
                        belief.GoalCongruences[i], belief.Likelihood, belief.IsIncremental);
                    var utility = currentGoal.Utility;
                    var desirability = deltaLikelihood * utility;
                    Log(
                        $"Evaluated goal: {currentGoal.Name}(u={utility.Value:0.00},dL={deltaLikelihood.Value:0.00})");

                    // Now find the owners, and update their emotional states.
                    var agentsWithGoal = _agents
                        .Where(x => x.Value.HasGoal(currentGoal.Name))
                        .Select(x => x.Value);

                    foreach (var owner in agentsWithGoal)
                    {
                        Log($"...owned by {owner.Name}");
                        EvaluateInternalEmotion(utility, deltaLikelihood, currentGoal.Likelihood, owner);
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
                // Check only affectedAgent (which can be much faster)
                for (var i = 0; i < belief.AffectedGoalNames.Length; i++)
                {
                    // Loop through every goal in the list of affected goals by this event.
                    var affectedGoalName = belief.AffectedGoalNames[i];
                    if (!_goals.ContainsKey(affectedGoalName))
                        continue;
                    var currentGoal = _goals[affectedGoalName];
                    var deltaLikelihood = CalculateDeltaLikelihood(currentGoal,
                        belief.GoalCongruences[i], belief.Likelihood, belief.IsIncremental);
                    var utility = currentGoal.Utility;
                    var desirability = deltaLikelihood * utility;

                    // Assume affectedAgent is the only owner to be considered in this appraisal round.
                    var owner = affectedAgent;

                    EvaluateInternalEmotion(utility, deltaLikelihood, currentGoal.Likelihood, owner);
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
        ///     Decays the emotional state and relations for all registered agents. It performs the decay according to the time
        ///     passed, so longer intervals between consecutive calls result in bigger clunky steps. Typically this is called
        ///     automatically when you use <see cref="StartDecay" />, but you can use it yourself if you want to manage the timing.
        ///     This function is keeping track of the millis passed since the last call, and will (try to) keep the decay close to
        ///     the desired decay factor, regardless of the time passed. You can call this any time you want, such as within a game
        ///     loop. Further, if you want to tweak the emotional intensity decay of individual agents, you should tweak the
        ///     decayFactor per agent not the "frame rate" of the decay (as this doesn't change the rate).
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
            return double.IsNaN(oldLikelihood)
                ? newLikelihood
                : new DoubleNegativeOneToPositiveOneInclusive(newLikelihood - oldLikelihood);
        }

        /// <summary>
        ///     Evaluates the event in terms of internal emotions that do not need relations to exist, such as hope, fear, etc..
        /// </summary>
        /// <param name="utility"></param>
        /// <param name="deltaLikelihood"></param>
        /// <param name="likelihood"></param>
        /// <param name="agent"></param>
        private static void EvaluateInternalEmotion(DoubleNegativeOneToPositiveOneInclusive utility,
            DoubleNegativeOneToPositiveOneInclusive deltaLikelihood, DoubleZeroToOneInclusive likelihood, Agent agent)
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