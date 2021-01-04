using System;
using GamygdalaNet.Types;

namespace GamygdalaNet.Agents.Data
{
    /// <summary>
    ///     Stores a goal with its utility and likelihood of being achieved. This is used as basis for interpreting Beliefs.
    /// </summary>
    public class Goal
    {
        /// <summary>
        ///     The likelihood is unknown at the start so it starts in the middle between disconfirmed (0) and confirmed (1).
        /// </summary>
        private const double DefaultLikelihood = 0.5;

        /// <summary>
        ///     Stores a goal with its utility and likelihood of being achieved. This is used as basis for interpreting Beliefs.
        /// </summary>
        /// <param name="name">The name of the goal</param>
        /// <param name="utility">
        ///     The utility of the goal, -1 to 1 inclusive. Utility is the value the NPC attributes to this goal
        ///     becoming true where a negative value means the NPC does not want this to happen.
        /// </param>
        /// <param name="isMaintenanceGoal">
        ///     Defines if the goal is a maintenance goal or not. The default is that the goal is an
        ///     achievement goal, i.e., a goal that once its likelihood reaches true (1) or false (-1) it stays that way.
        ///     There are maintenance and achievement goals. When an achievement goal is reached (or not), this is definite (e.g.,
        ///     to get the promotion or not). A maintenance goal can become true/false indefinitely (e.g., to be well-fed)
        /// </param>
        /// <param name="customLikelihoodCalculation">
        ///     If provided, gamygdala uses this function for calculating the goal likelihood instead of calculating the goal
        ///     likelihood from beliefs (game events).
        /// </param>
        public Goal(string name, DoubleNegativeOneToPositiveOneInclusive utility, bool isMaintenanceGoal,
            Func<DoubleZeroToOneInclusive> customLikelihoodCalculation = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException($"{nameof(name)} cannot be null or empty.");

            Name = name;
            Utility = utility;
            Likelihood = double.NaN; // TODO - We need to better track when a goal's likelihood hasn't been calculated yet.
            IsMaintenanceGoal = isMaintenanceGoal;
            CustomLikelihoodCalculation = customLikelihoodCalculation;

            // This is set to false, in which case gamygdala assumes beliefs (events) will be used to calculate the goal
            // likelihood by calculateDeltaLikelihood method. If set to true, instead gamygdala assumes this property
            // is function that calculates the likelihood.
            HasCustomLikelihoodCalculation = customLikelihoodCalculation != null;
        }

        public string Name { get; }
        public DoubleNegativeOneToPositiveOneInclusive Utility { get; }
        public DoubleZeroToOneInclusive Likelihood { get; set; }
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
    }
}