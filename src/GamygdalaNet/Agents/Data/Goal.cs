using System;
using GamygdalaNet.Types;

namespace GamygdalaNet.Agents.Data
{
    /// <summary>
    ///     An immutable goal definition: name, utility, maintenance
    ///     flag. Each appraising <see cref="Agent" /> tracks its own
    ///     likelihood for the goal via
    ///     <see cref="Agent.TryGetGoalLikelihood" /> and
    ///     <see cref="Agent.SetGoalLikelihood" />, so two agents can
    ///     share a single <see cref="Goal" /> reference without
    ///     aliasing each other's state. Mirrors the Popescu paper's
    ///     Owner | Goal Name | Utility table.
    /// </summary>
    public class Goal
    {
        /// <summary>
        ///     An immutable goal definition. Per-agent likelihood
        ///     state lives on <see cref="Agent" />; this class is just
        ///     the template.
        /// </summary>
        /// <param name="name">The name of the goal.</param>
        /// <param name="utility">
        ///     The utility of the goal, -1 to 1 inclusive. Utility is the value the NPC attributes to this goal
        ///     becoming true where a negative value means the NPC does not want this to happen.
        /// </param>
        /// <param name="isMaintenanceGoal">
        ///     Defines if the goal is a maintenance goal or not. The default is that the goal is an
        ///     achievement goal, i.e., a goal that once its likelihood reaches true (1) or false (-1) it stays that way.
        ///     There are maintenance and achievement goals. When an achievement goal is reached (or not), this is definite (e.g.,
        ///     to get the promotion or not). A maintenance goal can become true/false indefinitely (e.g., to be well-fed).
        /// </param>
        /// <param name="customLikelihoodCalculation">
        ///     When supplied, replaces equation 2 for this goal: each appraisal calls this function to compute the new
        ///     likelihood instead of deriving it from the belief. The certainty-saturation snap is also suppressed, so
        ///     a custom calc return of 0.5 against a certain-but-facilitating belief stores 0.5 (not 1). Per-agent
        ///     likelihood state is still recorded on <see cref="Agent" />. The achievement-goal short-circuit still
        ///     applies, so once an achievement goal's stored likelihood reaches 0 or 1 the custom function stops being
        ///     called (subsequent appraisals are no-ops). Port-specific feature; the Popescu paper specifies only
        ///     equation 2.
        /// </param>
        /// <param name="likelihoodDecayRate">
        ///     Per-tick fraction by which each agent's stored likelihood for this goal glides back toward the
        ///     "Unknown" prior of 0.5. Default 0 (no decay, paper-faithful). Useful for social-needs goals like
        ///     "be liked" or "be respected" where a single positive confirmation should not lock the goal at
        ///     1.0 forever: a small positive rate keeps the goal responsive to fresh validation. Applied by
        ///     <see cref="Agent.Decay" /> alongside the existing emotion + relation decay. Range [0, 1]; 0
        ///     leaves the likelihood untouched, 1 jumps to 0.5 in a single tick. Port-specific feature; the
        ///     Popescu paper does not specify likelihood decay.
        /// </param>
        public Goal(string name, GoalUtility utility, bool isMaintenanceGoal = false,
            Func<Likelihood> customLikelihoodCalculation = null,
            double likelihoodDecayRate = 0.0)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException($"{nameof(name)} cannot be null or empty.");
            if (likelihoodDecayRate < 0.0 || likelihoodDecayRate > 1.0)
                throw new ArgumentOutOfRangeException(nameof(likelihoodDecayRate), likelihoodDecayRate,
                    $"{nameof(likelihoodDecayRate)} must be in [0, 1].");

            Name = name;
            Utility = utility;
            IsMaintenanceGoal = isMaintenanceGoal;
            CustomLikelihoodCalculation = customLikelihoodCalculation;
            LikelihoodDecayRate = likelihoodDecayRate;

            // This is set to false, in which case gamygdala assumes beliefs (events) will be used to calculate the goal
            // likelihood by calculateDeltaLikelihood method. If set to true, instead gamygdala assumes this property
            // is function that calculates the likelihood.
            HasCustomLikelihoodCalculation = customLikelihoodCalculation != null;
        }

        public string Name { get; }
        public GoalUtility Utility { get; }
        public bool IsMaintenanceGoal { get; }
        public Func<Likelihood> CustomLikelihoodCalculation { get; }
        public double LikelihoodDecayRate { get; }

        /// <summary>
        ///     Assigned on construction. If false, gamygdala assumes beliefs (game events) will be used to calculate the goal
        ///     likelihood. If true, gamygdala uses <see cref="CustomLikelihoodCalculation" /> instead.
        /// </summary>
        public bool HasCustomLikelihoodCalculation { get; }

        public override bool Equals(object obj)
        {
            return obj is Goal goal && Equals(goal);
        }

        public bool Equals(Goal other)
        {
            return Name == other.Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return
                $"Goal: name={Name}, utility={Utility.Value:0.00}, isMaintenance={IsMaintenanceGoal}";
        }
    }
}
