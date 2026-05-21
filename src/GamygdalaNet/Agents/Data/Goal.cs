using System;
using GamygdalaNet.Types;

namespace GamygdalaNet.Agents.Data
{
    /// <summary>
    ///     An immutable goal definition: name, utility, and whether it is
    ///     a maintenance goal. The per-agent state of this goal (its
    ///     current likelihood as the agent appraises events against it)
    ///     lives on <see cref="Agent" /> via
    ///     <see cref="Agent.GetGoalLikelihood" /> /
    ///     <see cref="Agent.SetGoalLikelihood" />. Separating "what the
    ///     goal is" from "this agent's state on this goal" lets two
    ///     agents share a single <see cref="Goal" /> reference safely;
    ///     each maintains its own likelihood progress without aliasing
    ///     the other's appraisal state. Matches the Popescu et al.
    ///     paper's data model where the Owner | Goal Name | Utility
    ///     table treats goals as templates an agent adopts.
    /// </summary>
    public class Goal
    {
        /// <summary>
        ///     The default per-agent likelihood when the agent has not
        ///     yet appraised any belief against this goal. Per the
        ///     Popescu paper at §3.2 (line 184) the initial value is
        ///     Unknown; the worked example at line 354 evaluates this
        ///     as a uniform 0.5 prior. The constant is exposed so
        ///     <see cref="Agent.GetGoalLikelihood" /> can return it as
        ///     the fallback when an agent has no recorded state.
        /// </summary>
        public const double DefaultLikelihood = 0.5;

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
        ///     If provided, gamygdala uses this function for calculating the goal likelihood instead of calculating the goal
        ///     likelihood from beliefs (game events). When supplied, per-agent likelihood state is ignored.
        /// </param>
        public Goal(string name, DoubleNegativeOneToPositiveOneInclusive utility, bool isMaintenanceGoal = false,
            Func<DoubleZeroToOneInclusive> customLikelihoodCalculation = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException($"{nameof(name)} cannot be null or empty.");

            Name = name;
            Utility = utility;
            IsMaintenanceGoal = isMaintenanceGoal;
            CustomLikelihoodCalculation = customLikelihoodCalculation;

            // This is set to false, in which case gamygdala assumes beliefs (events) will be used to calculate the goal
            // likelihood by calculateDeltaLikelihood method. If set to true, instead gamygdala assumes this property
            // is function that calculates the likelihood.
            HasCustomLikelihoodCalculation = customLikelihoodCalculation != null;
        }

        public string Name { get; }
        public DoubleNegativeOneToPositiveOneInclusive Utility { get; }
        public bool IsMaintenanceGoal { get; }
        public Func<DoubleZeroToOneInclusive> CustomLikelihoodCalculation { get; }

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
