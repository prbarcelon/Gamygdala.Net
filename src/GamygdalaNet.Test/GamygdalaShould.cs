using FluentAssertions;
using GamygdalaNet.Agents;
using GamygdalaNet.Agents.Data;
using GamygdalaNet.DecayStrategies;
using GamygdalaNet.RelationLikeStrategies;
using GamygdalaNet.Types;
using Xunit;

namespace GamygdalaNet.Test
{
    public static class EmotionTestEx
    {
        public static void ShouldBe(this Emotion actualEmotion, string emotionName, double expectedIntensityValue,
            double precision)
        {
            actualEmotion.Name.Should().Be(emotionName);
            actualEmotion.Intensity.Value.Should().BeApproximately(expectedIntensityValue, precision,
                $"{actualEmotion.Name} should be {expectedIntensityValue:0.00}");
        }
    }

    /// <summary>
    ///     Tests based on listings from gamygdala paper: Popescu, A., Broekens, J., &amp; Van Someren, M. (2013).
    ///     Gamygdala: An emotion engine for games. IEEE Transactions on Affective Computing, 5, 32-44.
    /// </summary>
    public class GamygdalaShould
    {
        private readonly Gamygdala _gamygdala;

        public GamygdalaShould()
        {
            _gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());
        }

        /// <summary>
        ///     Scenario taken from Listing 2 of gamygdala paper.
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
                new[] {goalName},
                new DoubleNegativeOneToPositiveOneInclusive[] {beliefCongruenceWithGoal});
            var updatedVillageSurroundedBelief =
                villageSurroundedBelief.CopyButWithNewLikelihood(finalVillageSurroundedBeliefLikelihood);
            const double expectedInitialFearIntensityValue = 0.72;
            const double expectedFinalFearIntensityValue = 0.36;
            const double expectedReliefIntensityValue = 0.27;
            const double expectedJoyIntensityValue = 0.27;

            var agent = _gamygdala.CreateAgent(agentName);
            var goal = _gamygdala.CreateGoalForAgent(agentName, goalName, goalUtility);
            goal.Name.Should().Be(goalName);
            goal.Utility.Value.Should().BeApproximately(goalUtility, precision);

            _gamygdala.DecayAll(millisPassedForDecayStep);
            _gamygdala.Appraise(villageSurroundedBelief, agent);
            var emotionsInitial = agent.GetEmotionalState();
            emotionsInitial.Length.Should().Be(1);
            emotionsInitial[0].ShouldBe(EmotionNames.Fear, expectedInitialFearIntensityValue, precision);

            _gamygdala.DecayAll(millisPassedForDecayStep);
            _gamygdala.Appraise(updatedVillageSurroundedBelief);
            var emotionsFinal = agent.GetEmotionalState();
            emotionsFinal.Length.Should().Be(3);
            emotionsFinal[0].ShouldBe(EmotionNames.Fear, expectedFinalFearIntensityValue, precision);
            emotionsFinal[1].ShouldBe(EmotionNames.Relief, expectedReliefIntensityValue, precision);
            emotionsFinal[2].ShouldBe(EmotionNames.Joy, expectedJoyIntensityValue, precision);
        }

        /// <summary>
        ///     Scenario taken from Listing 3 of gamygdala paper.
        /// </summary>
        [Fact]
        public void CalculateExpectedEmotionalStateOfGivenAgent_WhenGivenPrideScenario()
        {
            const double precision = 1e-2;
            const long millisPassedForDecayStep = 3000;

            var villageAgent = new Agent("village");
            var blacksmithAgent = new Agent("blacksmith");
            var toLiveGoal = new Goal("to live", 0.7);
            var villageDestroyedGoal = new Goal("village destroyed", -1);
            var villageProvidesHouseBelief = new Belief(1, villageAgent.Name, new[] {toLiveGoal.Name},
                new DoubleNegativeOneToPositiveOneInclusive[] {1});
            var villageIsUnarmedBelief = new Belief(0.7, null, new[] {villageDestroyedGoal.Name},
                new DoubleNegativeOneToPositiveOneInclusive[] {1});
            var blacksmithProvideWeaponsBelief = new Belief(1, blacksmithAgent.Name, new[] {villageDestroyedGoal.Name},
                new DoubleNegativeOneToPositiveOneInclusive[] {-1});

            // Village provides blacksmith a place to live, so blacksmith likes village.
            _gamygdala.RegisterAgent(villageAgent);
            _gamygdala.RegisterAgent(blacksmithAgent);
            _gamygdala.RegisterGoal(toLiveGoal);
            blacksmithAgent.AddGoal(toLiveGoal);
            _gamygdala.Appraise(villageProvidesHouseBelief);
            _gamygdala.CreateRelation(blacksmithAgent.Name, villageAgent.Name, 1);
            var initialEmotions = blacksmithAgent.GetEmotionalState();
            initialEmotions.Length.Should().Be(2);
            initialEmotions[0].ShouldBe(EmotionNames.Joy, 0.7, precision);
            initialEmotions[1].ShouldBe(EmotionNames.Gratitude, 0.7, precision);
            var relations = blacksmithAgent.GetRelations();
            relations.Length.Should().Be(1);
            relations[0].TargetAgentName.Should().Be(villageAgent.Name);
            relations[0].EmotionsList.Length.Should().Be(1);
            relations[0].EmotionsList[0].ShouldBe(EmotionNames.Gratitude, 0.7, precision);
            _gamygdala.DecayAll(millisPassedForDecayStep);

            // Blacksmith believes the village is in danger because it lacks weapons.
            _gamygdala.RegisterGoal(villageDestroyedGoal);
            villageAgent.AddGoal(villageDestroyedGoal);
            _gamygdala.Appraise(villageIsUnarmedBelief);
            var emotionsAfterVillageIsUnarmed = blacksmithAgent.GetEmotionalState();
            emotionsAfterVillageIsUnarmed.Length.Should().Be(3);
            var relationsAfterVillageIsUnarmed = blacksmithAgent.GetRelations();
            relationsAfterVillageIsUnarmed.Length.Should().Be(1);
            relationsAfterVillageIsUnarmed[0].TargetAgentName.Should().Be(villageAgent.Name);
            relationsAfterVillageIsUnarmed[0].EmotionsList.Length.Should().Be(2);
            relationsAfterVillageIsUnarmed[0].EmotionsList[1].ShouldBe(EmotionNames.Pity, 0.85, precision);
            _gamygdala.DecayAll(millisPassedForDecayStep);

            // Blacksmith provides weapons to the village, so the village likes the blacksmith
            _gamygdala.Appraise(blacksmithProvideWeaponsBelief);
            var emotionsAfterWeaponsProvided = blacksmithAgent.GetEmotionalState();
            emotionsAfterWeaponsProvided.Length.Should().Be(5);
            emotionsAfterWeaponsProvided[0].ShouldBe(EmotionNames.Joy, 0.18, precision);
            emotionsAfterWeaponsProvided[4].ShouldBe(EmotionNames.Gratification, 0.85, precision);
            var relationsAfterWeaponsProvided = blacksmithAgent.GetRelations();
            relationsAfterWeaponsProvided.Length.Should().Be(1);
            relationsAfterWeaponsProvided[0].TargetAgentName.Should().Be(villageAgent.Name);
            relationsAfterWeaponsProvided[0].EmotionsList.Length.Should().Be(3);
            var emotionsTowardsVillages = relationsAfterWeaponsProvided[0].EmotionsList;
            emotionsTowardsVillages[0].ShouldBe(EmotionNames.Gratitude, 0.18, precision);
            emotionsTowardsVillages[1].ShouldBe(EmotionNames.Pity, 0.44, precision);
            emotionsTowardsVillages[2].ShouldBe(EmotionNames.HappyFor, 0.85, precision);
        }

        /// <summary>
        ///     Scenario taken from Listing 4 of gamygdala paper.
        /// </summary>
        [Fact]
        public void CalculateExpectedEmotionalStateOfGivenAgent_WhenGivenRtsScenario()
        {
            const double precision = 1e-2;
            const long millisPassedForDecayStep = 1000;

            var npcAgent = new Agent("npc");
            var dieGoal = new Goal("die", -1);
            var winGoal = new Goal("win", 1) {Likelihood = 0.1875};
            const double health = 0.3; // Health as percentage of max hit points.
            const double dyingLikelihood = 1.0 - health;
            var woundedBelief = new Belief(dyingLikelihood, npcAgent.Name, new[] {dieGoal.Name},
                new DoubleNegativeOneToPositiveOneInclusive[] {1});
            var enemyBuildingsDownBelief = new Belief(0.5, null, new[] {winGoal.Name},
                new DoubleNegativeOneToPositiveOneInclusive[] {1});

            _gamygdala.RegisterAgent(npcAgent);
            _gamygdala.RegisterGoal(dieGoal);
            _gamygdala.RegisterGoal(winGoal);
            npcAgent.AddGoal(dieGoal);
            npcAgent.AddGoal(winGoal);
            _gamygdala.Appraise(woundedBelief);
            var initialEmotions = npcAgent.GetEmotionalState();
            initialEmotions.Length.Should().Be(1);
            initialEmotions[0].ShouldBe(EmotionNames.Fear, 0.85, precision);
            _gamygdala.DecayAll(millisPassedForDecayStep);

            _gamygdala.Appraise(enemyBuildingsDownBelief);
            var finalEmotions = npcAgent.GetEmotionalState();
            finalEmotions.Length.Should().Be(2);
            finalEmotions[0].ShouldBe(EmotionNames.Fear, 0.69, precision);
            finalEmotions[1].ShouldBe(EmotionNames.Hope, 0.56, precision);
        }
    }
}