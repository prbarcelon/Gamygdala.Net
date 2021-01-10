using FluentAssertions;
using GamygdalaNet.Agents.Data;
using GamygdalaNet.DecayStrategies;
using GamygdalaNet.Types;
using Xunit;

namespace GamygdalaNet.Test
{
    /// <summary>
    /// Unit tests based on listings from gamygdala paper: Popescu, A., Broekens, J., &amp; Van Someren, M. (2013). Gamygdala: An emotion engine for games. IEEE Transactions on Affective Computing, 5, 32-44.
    /// </summary>
    public class GamygdalaShould
    {
        private readonly Gamygdala _gamygdala;

        public GamygdalaShould()
        {
            _gamygdala = new Gamygdala(new ExponentialDecayStrategy(0.8));
        }

        /// <summary>
        /// Scenario taken from Listing 2 of gamygdala paper.
        /// </summary>
        [Fact]
        public void CalculateExpectedInternalStateOfGivenAgent_WhenGivenReliefScenario()
        {
            const double precision = 1e-2;

            const long millisPassedForDecayStep = 3000;
            const string agentName = "hero";
            const string goalName = "village destroyed";
            const double goalUtility = -0.9;
            const string causalAgent = null;
            const double beliefCongruenceWithGoal = 1;
            const double initialVillageSurroundedBeliefLikelihood = 0.6;
            const double finalVillageSurroundedBeliefLikelihood = 0;
            var villageSurroundedBelief = new Belief(initialVillageSurroundedBeliefLikelihood, causalAgent,
                new[]
                {
                    goalName
                },
                new[]
                {
                    new DoubleNegativeOneToPositiveOneInclusive(beliefCongruenceWithGoal)
                });
            var updatedVillageSurroundedBelief =
                villageSurroundedBelief.CopyButWithNewLikelihood(finalVillageSurroundedBeliefLikelihood);
            const double expectedInitialFearIntensityValue = 0.72;
            const double expectedFinalFearIntensityValue = 0.36;
            const double expectedReliefIntensityValue = 0.27;
            const double expectedJoyIntensityValue = 0.27;

            _gamygdala.PrintDebug = true;
            var agent = _gamygdala.CreateAgent(agentName);
            var goal = _gamygdala.CreateGoalForAgent(agentName, goalName, goalUtility);
            goal.Name.Should().Be(goalName);
            goal.Utility.Value.Should().BeApproximately(goalUtility, precision);

            _gamygdala.DecayAll(millisPassedForDecayStep);
            _gamygdala.Appraise(villageSurroundedBelief, agent);
            var emotionsInitial = agent.GetEmotionalState();
            emotionsInitial.Length.Should().Be(1);
            emotionsInitial[0].Name.Should().Be(EmotionNames.Fear);
            emotionsInitial[0].Intensity.Value.Should().BeApproximately(expectedInitialFearIntensityValue, precision);

            _gamygdala.DecayAll(millisPassedForDecayStep);
            _gamygdala.Appraise(updatedVillageSurroundedBelief);
            var emotionsFinal = agent.GetEmotionalState();
            emotionsFinal.Length.Should().Be(3);
            emotionsFinal[0].Name.Should().Be(EmotionNames.Fear);
            emotionsFinal[0].Intensity.Value.Should().BeApproximately(expectedFinalFearIntensityValue, precision);
            emotionsFinal[1].Name.Should().Be(EmotionNames.Relief);
            emotionsFinal[1].Intensity.Value.Should().BeApproximately(expectedReliefIntensityValue, precision);
            emotionsFinal[2].Name.Should().Be(EmotionNames.Joy);
            emotionsFinal[2].Intensity.Value.Should().BeApproximately(expectedJoyIntensityValue, precision);
        }
    }
}