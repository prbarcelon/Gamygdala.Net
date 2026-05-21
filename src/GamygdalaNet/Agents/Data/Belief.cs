using System;
using GamygdalaNet.Types;

namespace GamygdalaNet.Agents.Data
{
    /// <summary>
    ///     Stores one Belief for an agent. A belief is created and fed into a Gamygdala instance
    ///     <see cref="Gamygdala.Appraise" /> method for evaluation.
    /// </summary>
    public readonly struct Belief
    {
        /// <summary>
        ///     Stores one Belief for an agent. A belief is created and fed into a Gamygdala instance
        ///     <see cref="Gamygdala.Appraise" /> method for evaluation.
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
        public Belief(Likelihood likelihood, string causalAgentName, string[] affectedGoalNames,
            GoalCongruence[] goalCongruences)
        {
            if (affectedGoalNames.Length != goalCongruences.Length)
                throw new ArgumentOutOfRangeException(nameof(goalCongruences),
                    $"The lengths of {nameof(affectedGoalNames)} and {nameof(goalCongruences)} must be equal.");

            Likelihood = likelihood;
            CausalAgentName = causalAgentName;

            // Copy arrays.
            var length = affectedGoalNames.Length;
            AffectedGoalNames = new string[length];
            Array.Copy(affectedGoalNames, AffectedGoalNames, length);
            GoalCongruences = new GoalCongruence[length];
            Array.Copy(goalCongruences, GoalCongruences, length);
        }

        public Likelihood Likelihood { get; }
        public string CausalAgentName { get; }
        public string[] AffectedGoalNames { get; }
        public GoalCongruence[] GoalCongruences { get; }

        public override string ToString()
        {
            var numberOfGoals = AffectedGoalNames.Length;
            var goalsAndCongruences = new string[numberOfGoals];
            for (var i = 0; i < numberOfGoals; i++)
            {
                var affectedGoal = $"{AffectedGoalNames[i]}({GoalCongruences[i].Value:0.00})";
                goalsAndCongruences[i] = affectedGoal;
            }

            var affectedGoals = string.Join(", ", goalsAndCongruences);

            return
                $"Belief: {affectedGoals}; likelihood={Likelihood.Value:0.00}, causalAgent={CausalAgentName}";
        }

        public Belief CopyButWithNewLikelihood(Likelihood likelihood)
        {
            return new Belief(likelihood, CausalAgentName, AffectedGoalNames, GoalCongruences);
        }
    }
}
