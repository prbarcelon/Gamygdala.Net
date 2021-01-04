using FluentAssertions;
using GamygdalaNet.Agents.Data;
using GamygdalaNet.DecayStrategies;
using GamygdalaNet.Types;
using Xunit;

namespace GamygdalaNet.Test
{
    public class GamygdalaShould
    {
        private readonly Gamygdala _gamygdala;

        public GamygdalaShould()
        {
            _gamygdala = new Gamygdala(new ExponentialDecayStrategy(0.8));
        }

        [Fact]
        public void CalculateExpectedInternalStateOfGivenAgent()
        {
            const string agentName = "hero";
            const string goalName = "village destroyed";
            const double goalUtility = -0.9;
            const double tolerance = 1e-2;
            const long millisPassed = 3000;
            
            _gamygdala.PrintDebug = true;
            var agent = _gamygdala.CreateAgent(agentName);
            var goal = _gamygdala.CreateGoalForAgent(agentName, goalName, goalUtility);
            goal.Name.Should().Be(goalName);
            goal.Utility.Value.Should().BeApproximately(goalUtility, tolerance);
            var villageSurroundedBelief = new Belief(0.6, null, new[] {goalName},
                new[] {new DoubleNegativeOneToPositiveOneInclusive(1)});
            _gamygdala.Appraise(villageSurroundedBelief, agent);

            var emotionsInitial = agent.GetEmotionalState();
            emotionsInitial.Length.Should().Be(1);
            emotionsInitial[0].Name.Should().Be(EmotionNames.Fear);
            emotionsInitial[0].Intensity.Value.Should().BeApproximately(0.72, tolerance);

            _gamygdala.DecayAll(millisPassed);
            var updatedVillageSurroundedBelief = villageSurroundedBelief.CopyButWithNewLikelihood(0);
            _gamygdala.Appraise(updatedVillageSurroundedBelief);

            var emotionsFinal = agent.GetEmotionalState();
            emotionsFinal.Length.Should().Be(2);
            emotionsFinal[0].Name.Should().Be(EmotionNames.Fear);
            emotionsFinal[0].Intensity.Value.Should().BeApproximately(0.36, tolerance);
            emotionsFinal[1].Name.Should().Be(EmotionNames.Hope);
            emotionsFinal[1].Intensity.Value.Should().BeApproximately(0.27, tolerance);
        }
    }
}