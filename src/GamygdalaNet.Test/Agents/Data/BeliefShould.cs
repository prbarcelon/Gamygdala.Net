using System;
using FluentAssertions;
using GamygdalaNet.Agents.Data;
using GamygdalaNet.Types;
using Xunit;

namespace GamygdalaNet.Test.Agents.Data
{
    public class BeliefShould
    {
        private readonly string _agentName;
        private readonly Belief _belief;
        private readonly GoalCongruence[] _congruences;
        private readonly string[] _goals;

        public BeliefShould()
        {
            _agentName = "agent";
            _goals = new[] {"goal"};
            _congruences = new[] {new GoalCongruence(.5)};
            _belief = new Belief(.8, _agentName, _goals, _congruences);
        }

        [Fact]
        public void ThrowArgumentOutOfRangeException_WhenGivenGoalsAndCongruenceOfDifferentLengthsDuringConstruction()
        {
            var emptyCongruences = new GoalCongruence[0];
            Action act = () => new Belief(.8, _agentName, _goals, emptyCongruences);

            act.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage(
                    "The lengths of affectedGoalNames and goalCongruences must be equal. (Parameter 'goalCongruences')");
        }

        [Fact]
        public void ReturnExpectedLikelihood()
        {
            _belief.Likelihood.Value.Should().BeApproximately(0.8, double.Epsilon);
        }

        [Fact]
        public void ReturnExpectedGoalCongruences()
        {
            _belief.GoalCongruences.Should().BeEquivalentTo(_congruences);
        }
        
        [Fact]
        public void ReturnExpectedGoals()
        {
            _belief.AffectedGoalNames.Should().BeEquivalentTo(_goals);
        }

        [Fact]
        public void ReturnExpectedCausalAgent()
        {
            _belief.CausalAgentName.Should().Be(_agentName);
        }

        [Fact]
        public void ReturnExpectedBeliefAsString()
        {
            _belief.ToString().Should().Be("Belief: goal(0.50); likelihood=0.80, causalAgent=agent");
        }
    }
}