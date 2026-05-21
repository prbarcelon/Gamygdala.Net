using System.Linq;
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
        ///     Scenario taken from Listing 1 of the gamygdala paper.
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
                new GoalCongruence[] {beliefCongruenceWithGoal});
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
                new GoalCongruence[] {1});
            var villageIsUnarmedBelief = new Belief(0.7, null, new[] {villageDestroyedGoal.Name},
                new GoalCongruence[] {1});
            var blacksmithProvideWeaponsBelief = new Belief(1, blacksmithAgent.Name, new[] {villageDestroyedGoal.Name},
                new GoalCongruence[] {-1});

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
            var winGoal = new Goal("win", 1);
            const double health = 0.3; // Health as percentage of max hit points.
            const double dyingLikelihood = 1.0 - health;
            var woundedBelief = new Belief(dyingLikelihood, npcAgent.Name, new[] {dieGoal.Name},
                new GoalCongruence[] {1});
            var enemyBuildingsDownBelief = new Belief(0.5, null, new[] {winGoal.Name},
                new GoalCongruence[] {1});

            _gamygdala.RegisterAgent(npcAgent);
            _gamygdala.RegisterGoal(dieGoal);
            _gamygdala.RegisterGoal(winGoal);
            npcAgent.AddGoal(dieGoal);
            npcAgent.AddGoal(winGoal);
            npcAgent.SetGoalLikelihood(winGoal.Name, 0.1875);
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

        /// <summary>
        ///     Pins the Disappointment quadrant of EvaluateInternalEmotion:
        ///     belief.likelihood == 0 with utility >= 0 and |delta| &lt; 0.5
        ///     fires Disappointment + Distress. Counterpart to the
        ///     Listing 1 Relief quadrant which exercises utility &lt; 0.
        /// </summary>
        [Fact]
        public void CalculateExpectedDisappointmentAndDistress_WhenPositiveUtilityGoalIsDisconfirmed()
        {
            const double precision = 1e-2;
            const string agentName = "hopeful";
            const string goalName = "win the lottery";
            const double goalUtility = 0.8;
            const double priorLikelihood = 0.9;
            const double beliefLikelihood = 0.0;
            const double beliefCongruence = 1.0;
            // Per equation 2: newLikelihood unsnapped = (1*0+1)/2 = 0.5.
            // delta = 0.5 - 0.9 = -0.4 (|delta| < 0.5 fires Disappointment).
            // intensity = |0.8 * -0.4| = 0.32.
            const double expectedIntensity = 0.32;

            var agent = _gamygdala.CreateAgent(agentName);
            _gamygdala.CreateGoalForAgent(agentName, goalName, goalUtility);
            agent.SetGoalLikelihood(goalName, priorLikelihood);

            var belief = new Belief(beliefLikelihood, null, new[] { goalName },
                new GoalCongruence[] { beliefCongruence });
            _gamygdala.Appraise(belief);

            var emotions = agent.GetEmotionalState();
            emotions.Length.Should().Be(2);
            emotions[0].ShouldBe(EmotionNames.Disappointment, expectedIntensity, precision);
            emotions[1].ShouldBe(EmotionNames.Distress, expectedIntensity, precision);
        }

        /// <summary>
        ///     Covers AgentActions Case 3 (affected=other, causal=self,
        ///     relation.Like &gt;= 0, desirability &lt; 0): the causal
        ///     agent feels Remorse. Companion to Listing 3, which
        ///     exercises the desirability &gt;= 0 sibling (Gratification).
        /// </summary>
        [Fact]
        public void CalculateExpectedRemorse_WhenCausalAgentHarmsLikedAgentsGoal()
        {
            const double precision = 1e-2;
            // Knight likes the village; knight causes a belief that harms
            // the village's positive-utility goal. Case 3 fires Remorse on
            // knight (the causal agent) toward village.
            var villageAgent = new Agent("village");
            var knightAgent = new Agent("knight");
            var stayAliveGoal = new Goal("stay alive", 1);

            _gamygdala.RegisterAgent(villageAgent);
            _gamygdala.RegisterAgent(knightAgent);
            _gamygdala.RegisterGoal(stayAliveGoal);
            villageAgent.AddGoal(stayAliveGoal);
            villageAgent.SetGoalLikelihood(stayAliveGoal.Name, 0.85);
            _gamygdala.CreateRelation(knightAgent.Name, villageAgent.Name, 1);

            // Knight believes its action disconfirms the village's stay-alive goal.
            // newLikelihood unsnapped = (-1*0.5+1)/2 = 0.25; delta = 0.25 - 0.85 = -0.6.
            // desirability = -0.6 * 1 = -0.6 (Case 3: like >= 0 && desirability < 0 → Remorse).
            // intensity = |1 * -0.6 * 1| = 0.6.
            var harmfulBelief = new Belief(0.5, knightAgent.Name, new[] { stayAliveGoal.Name },
                new GoalCongruence[] { -1 });
            _gamygdala.Appraise(harmfulBelief);

            var knightEmotions = knightAgent.GetEmotionalState();
            knightEmotions.Should().ContainSingle(e => e.Name == EmotionNames.Remorse)
                .Which.Intensity.Value.Should().BeApproximately(0.6, precision);
        }

        /// <summary>
        ///     Covers the four corners of the saturation snap (Popescu
        ///     §3.4.2 + Listings 1, 3). When the belief is at certainty
        ///     (likelihood == 0 or 1) the STORED goal likelihood
        ///     saturates to whichever boundary the congruence sign
        ///     points at. Snapping on belief.likelihood alone (without
        ///     considering congruence) would store the wrong boundary
        ///     when a certain-but-blocking belief disconfirms a goal.
        /// </summary>
        [Theory]
        [InlineData(1.0, 1.0, 1.0)]   // certain-happening + facilitating  → goal achieves (1)
        [InlineData(1.0, -1.0, 0.0)]  // certain-happening + blocking      → goal disconfirmed (0)
        [InlineData(0.0, 1.0, 0.0)]   // certain-not-happening + facilitating → goal disconfirmed (0)
        [InlineData(0.0, -1.0, 1.0)]  // certain-not-happening + blocking    → goal achieves (1)
        [InlineData(1.0, 0.0, 0.5)]   // certain belief but congruence == 0 → no snap (equation 2's 0.5)
        [InlineData(0.0, 0.0, 0.5)]   // certain-not-happening + congruence == 0 → no snap
        public void SnapStoredGoalLikelihoodToTheBoundaryConsistentWithCongruence(
            double beliefLikelihood, double congruence, double expectedStoredLikelihood)
        {
            const string agentName = "agent";
            const string goalName = "saturation goal";
            var agent = _gamygdala.CreateAgent(agentName);
            _gamygdala.CreateGoalForAgent(agentName, goalName, 1);

            var certainBelief = new Belief(beliefLikelihood, null, new[] { goalName },
                new GoalCongruence[] { congruence });
            _gamygdala.Appraise(certainBelief, agent);

            agent.TryGetGoalLikelihood(goalName, out var stored).Should().BeTrue();
            stored.Value.Should().BeApproximately(expectedStoredLikelihood, 1e-9);
        }

        /// <summary>
        ///     Achievement goals settle at either boundary. Once an
        ///     achievement goal's stored likelihood reaches 0 or 1,
        ///     subsequent beliefs do not move it. Maintenance goals
        ///     are exempt. The paper's §3.2 distinction uses signed
        ///     likelihood; this port's [0, 1] domain maps the failure
        ///     boundary to 0 and the achievement boundary to 1.
        /// </summary>
        [Fact]
        public void NotMoveAchievementGoalAfterReachingDisconfirmedBoundary()
        {
            const string agentName = "agent";
            const string goalName = "shield holds";
            var agent = _gamygdala.CreateAgent(agentName);
            _gamygdala.CreateGoalForAgent(agentName, goalName, 1);
            // Drive the goal to the disconfirmed boundary (0) via a
            // certain-blocking belief.
            var disconfirmingBelief = new Belief(1, null, new[] { goalName },
                new GoalCongruence[] { -1 });
            _gamygdala.Appraise(disconfirmingBelief, agent);
            agent.TryGetGoalLikelihood(goalName, out var afterFirst).Should().BeTrue();
            afterFirst.Value.Should().BeApproximately(0, 1e-9);

            // A subsequent facilitating belief must NOT move the goal:
            // achievement goals settle at the boundary.
            var recoveringBelief = new Belief(0.8, null, new[] { goalName },
                new GoalCongruence[] { 1 });
            _gamygdala.Appraise(recoveringBelief, agent);
            agent.TryGetGoalLikelihood(goalName, out var afterSecond).Should().BeTrue();
            afterSecond.Value.Should().BeApproximately(0, 1e-9);
        }

        /// <summary>
        ///     A goal with a custom likelihood calculation overrides
        ///     equation 2. The certainty-snap must also be suppressed:
        ///     a custom calc that returns 0.5 against a certain
        ///     facilitating belief must store 0.5, not snap to 1. The
        ///     API exists to let callers fully replace equation 2; the
        ///     snap subverting that defeats the purpose.
        /// </summary>
        [Fact]
        public void NotSnapStoredGoalLikelihood_WhenGoalHasCustomLikelihoodCalculation()
        {
            const string agentName = "agent";
            const string goalName = "custom goal";
            // Custom calc returns 0.5 unconditionally. A certain
            // facilitating belief would normally snap stored to 1; the
            // exemption preserves the custom calc's return value.
            var customGoal = new Goal(goalName, 1, customLikelihoodCalculation: () => new Likelihood(0.5));
            _gamygdala.RegisterGoal(customGoal);
            var agent = _gamygdala.CreateAgent(agentName);
            agent.AddGoal(customGoal);

            var certainFacilitatingBelief = new Belief(1, null, new[] { goalName },
                new GoalCongruence[] { 1 });
            _gamygdala.Appraise(certainFacilitatingBelief, agent);

            agent.TryGetGoalLikelihood(goalName, out var stored).Should().BeTrue();
            stored.Value.Should().BeApproximately(0.5, 1e-9);
        }

        /// <summary>
        ///     A goal with a positive <c>LikelihoodDecayRate</c>
        ///     glides its stored likelihood back toward the "Unknown"
        ///     prior (0.5) on each engine tick. Port-specific seam for
        ///     social-needs goals: "be liked" should not stay at 1.0
        ///     forever after a single compliment lands; one decay tick
        ///     pulls it back proportional to the configured rate.
        /// </summary>
        [Fact]
        public void GlideGoalLikelihoodTowardUnknownPriorAtConfiguredRate()
        {
            const string agentName = "agent";
            const string goalName = "be liked";
            var agent = _gamygdala.CreateAgent(agentName);
            var decayingGoal = new Goal(goalName, 0.6, isMaintenanceGoal: true, likelihoodDecayRate: 0.1);
            _gamygdala.RegisterGoal(decayingGoal);
            agent.AddGoal(decayingGoal);
            agent.SetGoalLikelihood(goalName, 1.0);

            // One tick: lerp(1.0, 0.5, 0.1) = 0.95.
            _gamygdala.DecayAll(1000);
            agent.TryGetGoalLikelihood(goalName, out var afterOne).Should().BeTrue();
            afterOne.Value.Should().BeApproximately(0.95, 1e-9);

            // After many ticks, the value settles at the "Unknown" prior 0.5.
            for (var i = 0; i < 200; i++) _gamygdala.DecayAll(1000);
            agent.TryGetGoalLikelihood(goalName, out var settled).Should().BeTrue();
            settled.Value.Should().BeApproximately(0.5, 1e-3);
        }

        /// <summary>
        ///     The default <c>LikelihoodDecayRate</c> of 0 leaves goal
        ///     likelihoods untouched across decay ticks (paper-faithful
        ///     behavior). Regression guard so the opt-in nature of the
        ///     seam never silently flips on.
        /// </summary>
        [Fact]
        public void LeaveGoalLikelihoodUnchanged_WhenDecayRateIsZero()
        {
            const string agentName = "agent";
            const string goalName = "be confirmed";
            var agent = _gamygdala.CreateAgent(agentName);
            _gamygdala.CreateGoalForAgent(agentName, goalName, 1);
            agent.SetGoalLikelihood(goalName, 1.0);

            for (var i = 0; i < 100; i++) _gamygdala.DecayAll(1000);

            agent.TryGetGoalLikelihood(goalName, out var stored).Should().BeTrue();
            stored.Value.Should().Be(1.0);
        }

        /// <summary>
        ///     End-to-end: after a goal's likelihood decays back toward
        ///     0.5, the next facilitating belief produces a meaningful
        ///     delta (and therefore a meaningful emotion intensity)
        ///     instead of the near-zero delta you'd get from a goal
        ///     pinned at 1.0. Models the "compliments need ongoing
        ///     reinforcement" behavior the user asked for.
        /// </summary>
        [Fact]
        public void ProduceMeaningfulDeltaOnNextAppraisal_AfterLikelihoodDecaysBackTowardUnknown()
        {
            const string agentName = "agent";
            const string goalName = "be liked";
            const double precision = 1e-2;
            var agent = _gamygdala.CreateAgent(agentName);
            var decayingGoal = new Goal(goalName, 0.6, isMaintenanceGoal: true, likelihoodDecayRate: 0.05);
            _gamygdala.RegisterGoal(decayingGoal);
            agent.AddGoal(decayingGoal);

            // Saturate the goal: belief.likelihood=1, congruence=+1 ->
            // stored snaps to 1, agent emits Joy with non-trivial
            // intensity from delta = newLikelihood (no prior).
            var firstCompliment = new Belief(1, null, new[] { goalName }, new GoalCongruence[] { 1 });
            _gamygdala.Appraise(firstCompliment, agent);
            var firstJoy = agent.GetEmotionalState().Single(e => e.Name == EmotionNames.Joy).Intensity.Value;

            // Immediately repeat the same belief: goal is at 1, delta
            // is now zero, Joy intensity does not increase.
            _gamygdala.Appraise(firstCompliment, agent);
            var secondJoy = agent.GetEmotionalState().Single(e => e.Name == EmotionNames.Joy).Intensity.Value;
            secondJoy.Should().BeApproximately(firstJoy, precision,
                "the second compliment with no decay in between produces no extra Joy");

            // Now let the likelihood decay back toward 0.5 across many
            // ticks. Emotion state also decays in parallel; reset it
            // so we can measure the next appraisal's contribution
            // cleanly.
            for (var i = 0; i < 200; i++) _gamygdala.DecayAll(1000);
            agent.TryGetGoalLikelihood(goalName, out var afterDecay).Should().BeTrue();
            afterDecay.Value.Should().BeApproximately(0.5, 1e-2);

            // Fire a third compliment. Goal is at 0.5; the new
            // appraisal's unsnapped newLikelihood = (1*1+1)/2 = 1,
            // delta = 1 - 0.5 = 0.5, intensity = |0.6 * 0.5| = 0.30.
            // The certainty-snap saturates the stored value back to
            // 1, but the returned delta uses the unsnapped value
            // (matching the Listing 1 / 3 / 4 magnitudes the engine
            // already pins). Emotion state has fully decayed by
            // now (exponential factor 0.8^200), so the new Joy
            // intensity is approximately the per-appraisal
            // contribution.
            _gamygdala.Appraise(firstCompliment, agent);
            var thirdJoy = agent.GetEmotionalState().Single(e => e.Name == EmotionNames.Joy).Intensity.Value;
            thirdJoy.Should().BeApproximately(0.30, precision,
                "after decay, a fresh compliment produces a meaningful Joy bump again");
        }

        /// <summary>
        ///     Per Popescu §3.4.6 the NPC's emotional state decays
        ///     toward a default ("personality-derived") state rather
        ///     than toward zero. The substrate accepts per-emotion
        ///     defaults via <see cref="Agent.SetDefaultEmotionIntensity" />.
        ///     A freshly-set default seeds the agent's emotional state
        ///     so the value is read back immediately.
        /// </summary>
        [Fact]
        public void SeedDefaultEmotionIntensityIntoEmotionalState_WhenSetBeforeAnyAppraisal()
        {
            var agent = _gamygdala.CreateAgent("baseline-anxious");
            agent.SetDefaultEmotionIntensity(EmotionNames.Fear, 0.2);

            var emotions = agent.GetEmotionalState();
            emotions.Should().ContainSingle(e => e.Name == EmotionNames.Fear)
                .Which.Intensity.Value.Should().BeApproximately(0.2, 1e-9);
        }

        /// <summary>
        ///     Decay interpolates toward each emotion's default rather
        ///     than toward zero. An appraisal that bumps an emotion
        ///     above its default gradually returns to the default
        ///     under repeated decay; the default acts as a floor for
        ///     positive-valence emotions (Popescu §3.4.6).
        /// </summary>
        [Fact]
        public void DecayEmotionTowardItsDefaultIntensityRatherThanZero()
        {
            const string agentName = "optimist";
            const string goalName = "succeed";
            const double precision = 1e-2;

            var agent = _gamygdala.CreateAgent(agentName);
            _gamygdala.CreateGoalForAgent(agentName, goalName, 1);
            agent.SetDefaultEmotionIntensity(EmotionNames.Joy, 0.2);

            // Bump Joy above its default with an appraisal.
            var goodNews = new Belief(1, null, new[] { goalName }, new GoalCongruence[] { 1 });
            _gamygdala.Appraise(goodNews, agent);
            var joyAfterAppraisal = agent.GetEmotionalState()
                .Single(e => e.Name == EmotionNames.Joy).Intensity.Value;
            joyAfterAppraisal.Should().BeGreaterThan(0.2);

            // Decay over time: Joy gravitates toward 0.2, not toward zero.
            for (var step = 0; step < 30; step++)
                _gamygdala.DecayAll(3000);

            var joyAfterLongDecay = agent.GetEmotionalState()
                .Single(e => e.Name == EmotionNames.Joy).Intensity.Value;
            joyAfterLongDecay.Should().BeApproximately(0.2, precision);
        }

        /// <summary>
        ///     Covers the goal-owner side of Listing 3 step 3 (provide
        ///     weapons). The blacksmith disconfirms the village's
        ///     "village destroyed" goal; the village feels Relief and
        ///     Joy at 0.85 each. Paper §4.2 calls this out: "if we
        ///     were to model the village with its own emotional brain
        ///     ... at the end Relief, Joy, and Gratitude towards the
        ///     blacksmith." Pins the saturation-snap fix that stores
        ///     the village's goal likelihood at 0 (disconfirmed)
        ///     rather than at 1 (the prior code path's bug).
        /// </summary>
        [Fact]
        public void CalculateExpectedReliefOnGoalOwner_WhenCertainBeliefDisconfirmsNegativeUtilityGoal()
        {
            const double precision = 1e-2;
            var villageAgent = new Agent("village");
            var blacksmithAgent = new Agent("blacksmith");
            var villageDestroyedGoal = new Goal("village destroyed", -1);

            _gamygdala.RegisterAgent(villageAgent);
            _gamygdala.RegisterAgent(blacksmithAgent);
            _gamygdala.RegisterGoal(villageDestroyedGoal);
            villageAgent.AddGoal(villageDestroyedGoal);
            // Seed prior so we replicate Listing 3 step 3's
            // post-step-2 state (village destroyed goal at 0.85).
            villageAgent.SetGoalLikelihood(villageDestroyedGoal.Name, 0.85);

            // Blacksmith provides weapons: certain belief, blocks the
            // village-destroyed goal. unsnapped = (-1*1+1)/2 = 0;
            // delta = 0 - 0.85 = -0.85; stored saturates to 0
            // (disconfirmed) per the congruence-aware snap.
            var blacksmithProvideWeaponsBelief = new Belief(1, blacksmithAgent.Name,
                new[] { villageDestroyedGoal.Name }, new GoalCongruence[] { -1 });
            _gamygdala.Appraise(blacksmithProvideWeaponsBelief);

            villageAgent.TryGetGoalLikelihood(villageDestroyedGoal.Name, out var storedLikelihood)
                .Should().BeTrue();
            storedLikelihood.Value.Should().BeApproximately(0, 1e-9);

            // Village now sees a negative-utility goal at likelihood 0
            // with |delta| > 0.5: Relief + Joy at |-1 * -0.85| = 0.85.
            var villageEmotions = villageAgent.GetEmotionalState();
            villageEmotions.Should().Contain(e => e.Name == EmotionNames.Relief)
                .Which.Intensity.Value.Should().BeApproximately(0.85, precision);
            villageEmotions.Should().Contain(e => e.Name == EmotionNames.Joy)
                .Which.Intensity.Value.Should().BeApproximately(0.85, precision);
        }

        /// <summary>
        ///     Pins AgentActions Case 2 (affected == self == causal) as
        ///     a no-op. Paper §3.4.4 would route Pride and Guilt here
        ///     (the self-cause-self-effect quadrant) but the engine
        ///     intentionally leaves the case empty (matches the
        ///     upstream Gamygdala.js). A future Pride / Guilt
        ///     implementation must not silently change this without
        ///     updating the test.
        /// </summary>
        [Fact]
        public void NotEmitPrideOrGuiltOrAgentActionEmotions_WhenSelfIsBothAffectedAndCausal()
        {
            const string agentName = "hero";
            const string goalName = "win the tournament";
            var heroAgent = _gamygdala.CreateAgent(agentName);
            _gamygdala.CreateGoalForAgent(agentName, goalName, 1);

            // Hero is the causal agent of a belief affecting its own goal.
            // EvaluateInternalEmotion still produces Hope (utility>=0, 0<L<1, delta>=0).
            // AgentActions Case 2 is the empty branch: no Gratitude / Anger /
            // Gratification / Remorse should appear on hero.
            var selfCausedBelief = new Belief(0.7, agentName, new[] { goalName },
                new GoalCongruence[] { 1 });
            _gamygdala.Appraise(selfCausedBelief);

            var emotions = heroAgent.GetEmotionalState();
            emotions.Should().NotContain(e => e.Name == EmotionNames.Gratitude);
            emotions.Should().NotContain(e => e.Name == EmotionNames.Anger);
            emotions.Should().NotContain(e => e.Name == EmotionNames.Gratification);
            emotions.Should().NotContain(e => e.Name == EmotionNames.Remorse);
        }
    }
}