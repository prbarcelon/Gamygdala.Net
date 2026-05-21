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
    /// <summary>
    ///     Tests corresponding to examples in the README. Each test method
    ///     documents a working code sample that appears in the documentation.
    /// </summary>
    public class ReadmeExamplesShould
    {
        /// <summary>
        ///     Minimal flow: create engine, agent, goal, appraise a belief, read emotions.
        /// </summary>
        [Fact]
        public void Quickstart()
        {
            var gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());

            var blacksmith = gamygdala.CreateAgent("blacksmith");
            var stayAliveGoal = gamygdala.CreateGoalForAgent("blacksmith", "stay alive", goalUtility: -1.0);

            var damageBelief = new Belief(
                likelihood: 0.8,
                causalAgentName: null,
                affectedGoalNames: new[] { "stay alive" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(damageBelief, blacksmith);

            var emotions = blacksmith.GetEmotionalState();
            emotions.Length.Should().Be(1);
            emotions[0].Name.Should().Be(EmotionNames.Fear);
            emotions[0].Intensity.Value.Should().BeGreaterThan(0);
        }

        /// <summary>
        ///     Two agents that share a Goal reference keep their own
        ///     per-agent likelihood state on Agent; one appraisal does
        ///     not alias the other.
        /// </summary>
        [Fact]
        public void AgentsAndGoals_SharedGoalHasIndependentLikelihood()
        {
            var gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());

            var alice = gamygdala.CreateAgent("alice");
            var bob = gamygdala.CreateAgent("bob");

            var treasureGoal = gamygdala.CreateGoalForAgent("alice", "find treasure", goalUtility: 1.0);
            bob.AddGoal(treasureGoal);

            var treasureFoundBelief = new Belief(
                likelihood: 1.0,
                causalAgentName: "alice",
                affectedGoalNames: new[] { "find treasure" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(treasureFoundBelief, alice);
            gamygdala.Appraise(treasureFoundBelief, bob);

            alice.TryGetGoalLikelihood("find treasure", out var aliceLikelihood).Should().BeTrue();
            bob.TryGetGoalLikelihood("find treasure", out var bobLikelihood).Should().BeTrue();

            aliceLikelihood.Value.Should().BeApproximately(1.0, 0.01);
            bobLikelihood.Value.Should().BeApproximately(1.0, 0.01);

            var aliceEmotions = alice.GetEmotionalState();
            var bobEmotions = bob.GetEmotionalState();

            // Both agents fire Joy (internal) + Gratitude (social,
            // toward the named causal agent "alice"). Each agent
            // stores its own likelihood for the shared goal, so both
            // see the full mood movement from this event.
            aliceEmotions.Should().Contain(e => e.Name == EmotionNames.Joy);
            bobEmotions.Should().Contain(e => e.Name == EmotionNames.Joy);
        }

        /// <summary>
        ///     Positive congruence (goal is facilitated) produces Joy.
        /// </summary>
        [Fact]
        public void PositiveAppraisalProducesJoy()
        {
            var gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());

            var healer = gamygdala.CreateAgent("healer");
            gamygdala.CreateGoalForAgent("healer", "heal patient", goalUtility: 0.9);

            var patientHealing = new Belief(
                likelihood: 1.0,
                causalAgentName: "healer",
                affectedGoalNames: new[] { "heal patient" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(patientHealing, healer);

            // Causal agent equals the affected agent (the healer
            // healed themselves), so AgentActions does not fire the
            // social Gratitude path. Just internal Joy.
            var emotions = healer.GetEmotionalState();
            emotions.Should().HaveCount(1);
            emotions[0].Name.Should().Be(EmotionNames.Joy);
            emotions[0].Intensity.Value.Should().BeApproximately(0.9, 0.01);
        }

        /// <summary>
        ///     Negative congruence (goal is blocked) produces Distress.
        /// </summary>
        [Fact]
        public void NegativeAppraisalProducesDistress()
        {
            var gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());

            var defender = gamygdala.CreateAgent("defender");
            gamygdala.CreateGoalForAgent("defender", "village survives", goalUtility: -0.8);

            var villageAttacked = new Belief(
                likelihood: 0.9,
                causalAgentName: "invader",
                affectedGoalNames: new[] { "village survives" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(villageAttacked, defender);

            // Internal emotion (Fear from negative-utility goal moving
            // away from achievement) plus a social emotion (Anger
            // toward the named causal agent "invader").
            var emotions = defender.GetEmotionalState();
            emotions.Should().Contain(e => e.Name == EmotionNames.Fear);
            emotions.Should().Contain(e => e.Name == EmotionNames.Anger);
        }

        /// <summary>
        ///     PAD aggregation via log-sum-exp (Reilly, equation 5).
        ///     Multiple emotions combine logarithmically rather than additively.
        /// </summary>
        [Fact]
        public void PadOutputAggregatesEmotionsLogarithmically()
        {
            var gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());

            var merchant = gamygdala.CreateAgent("merchant");
            gamygdala.CreateGoalForAgent("merchant", "sell goods", goalUtility: 0.7);
            gamygdala.CreateGoalForAgent("merchant", "avoid theft", goalUtility: -0.6);

            var goodsSold = new Belief(
                likelihood: 1.0,
                causalAgentName: "customer",
                affectedGoalNames: new[] { "sell goods" },
                goalCongruences: new GoalCongruence[] { 1 });

            var theftThreats = new Belief(
                likelihood: 0.4,
                causalAgentName: "bandit",
                affectedGoalNames: new[] { "avoid theft" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(goodsSold, merchant);
            gamygdala.Appraise(theftThreats, merchant);

            // After appraisal merchant carries four emotions:
            //   Joy 0.70 + Gratitude 0.70 (from goodsSold; Case 1 yields Gratitude toward "customer"),
            //   Fear 0.42 + Anger 0.42 (from theftThreats; Case 1 yields Anger toward "bandit").
            // Equation 5 (Reilly log-sum-exp) folds these into:
            //   P = 0.1 * log2(Σ 2^(10 * P_e * intensity_e)) ≈ 0.597
            //   A = 0.1 * log2(Σ 2^(10 * A_e * intensity_e)) ≈ 0.457
            //   D = 0.1 * log2(Σ 2^(10 * D_e * intensity_e)) ≈ 0.303
            const double precision = 1e-2;
            var padState = merchant.GetPadState();
            padState.Pleasure.Value.Should().BeApproximately(0.597, precision);
            padState.Arousal.Value.Should().BeApproximately(0.457, precision);
            padState.Dominance.Value.Should().BeApproximately(0.303, precision);
        }

        /// <summary>
        ///     Social emotions: when one agent causes an event affecting another's goals,
        ///     the observer feels emotions based on the relationship and desirability.
        /// </summary>
        [Fact]
        public void RelationsAndSocialEmotions_GratefulTowardsBenefactor()
        {
            var gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());

            var alice = gamygdala.CreateAgent("alice");
            var bob = gamygdala.CreateAgent("bob");

            gamygdala.CreateGoalForAgent("alice", "stay healthy", goalUtility: 0.9);
            gamygdala.CreateRelation("alice", "bob", relation: 1.0);

            var bobHealsAlice = new Belief(
                likelihood: 1.0,
                causalAgentName: "bob",
                affectedGoalNames: new[] { "stay healthy" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(bobHealsAlice, alice);

            var emotions = alice.GetEmotionalState();
            emotions.Should().Contain(e => e.Name == EmotionNames.Joy);
            emotions.Should().Contain(e => e.Name == EmotionNames.Gratitude);
            var gratitude = emotions.First(e => e.Name == EmotionNames.Gratitude);
            gratitude.Intensity.Value.Should().BeApproximately(0.9, 0.01);

            alice.TryGetRelation("bob", out var relationWithBob).Should().BeTrue();
            relationWithBob.EmotionsList.Should().Contain(e => e.Name == EmotionNames.Gratitude);
        }

        /// <summary>
        ///     Decay reduces emotion intensity over time according to the decay strategy.
        /// </summary>
        [Fact]
        public void DecayReducesEmotionIntensityOverTime()
        {
            var gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());

            var warrior = gamygdala.CreateAgent("warrior");
            gamygdala.CreateGoalForAgent("warrior", "defeat enemy", goalUtility: 1.0);

            var enemyDefeated = new Belief(
                likelihood: 1.0,
                causalAgentName: null,
                affectedGoalNames: new[] { "defeat enemy" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(enemyDefeated, warrior);
            var emotionsInitial = warrior.GetEmotionalState();
            var initialJoyIntensity = emotionsInitial[0].Intensity.Value;

            gamygdala.DecayAll(millisPassed: 3000);
            var emotionsAfterDecay = warrior.GetEmotionalState();
            var joyAfterDecay = emotionsAfterDecay[0].Intensity.Value;

            joyAfterDecay.Should().BeLessThan(initialJoyIntensity);
        }

        /// <summary>
        ///     Maintenance goals can fire repeatedly. Achievement
        ///     goals saturate once confirmed or disconfirmed.
        /// </summary>
        [Fact]
        public void MaintenanceGoalCanFireRepeatedly()
        {
            var gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());

            var animal = gamygdala.CreateAgent("animal");
            gamygdala.CreateGoalForAgent(
                "animal",
                "stay fed",
                goalUtility: 0.7,
                isMaintenanceGoal: true);

            var foodAvailable1 = new Belief(
                likelihood: 1.0,
                causalAgentName: null,
                affectedGoalNames: new[] { "stay fed" },
                goalCongruences: new GoalCongruence[] { 1 });

            var foodAvailable2 = new Belief(
                likelihood: 1.0,
                causalAgentName: null,
                affectedGoalNames: new[] { "stay fed" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(foodAvailable1, animal);
            var firstEmotions = animal.GetEmotionalState();
            firstEmotions.Should().HaveCountGreaterThan(0);
            firstEmotions[0].Name.Should().Be(EmotionNames.Joy);

            gamygdala.DecayAll(millisPassed: 5000);

            gamygdala.Appraise(foodAvailable2, animal);
            var secondEmotions = animal.GetEmotionalState();
            secondEmotions.Should().HaveCountGreaterThan(0);
            secondEmotions.Should().Contain(e => e.Name == EmotionNames.Joy);
        }

        /// <summary>
        ///     Achievement goal that reaches certainty (likelihood 1 or 0)
        ///     no longer changes. Maintenance goals can cycle indefinitely.
        /// </summary>
        [Fact]
        public void AchievementGoalSaturesAchievementGoalStabilizes()
        {
            var gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());

            var knight = gamygdala.CreateAgent("knight");
            gamygdala.CreateGoalForAgent(
                "knight",
                "slay dragon",
                goalUtility: 1.0,
                isMaintenanceGoal: false);

            var dragonSlain = new Belief(
                likelihood: 1.0,
                causalAgentName: "knight",
                affectedGoalNames: new[] { "slay dragon" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(dragonSlain, knight);
            var emotionsAfterKill = knight.GetEmotionalState();
            var joyAfterKill = emotionsAfterKill[0].Intensity.Value;

            gamygdala.DecayAll(millisPassed: 2000);

            var dragonSlainAgain = new Belief(
                likelihood: 1.0,
                causalAgentName: "knight",
                affectedGoalNames: new[] { "slay dragon" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(dragonSlainAgain, knight);
            var emotionsAfterSecondAppraisal = knight.GetEmotionalState();

            var dragonSlayedGoal = emotionsAfterSecondAppraisal
                .FirstOrDefault(e => e.Name == EmotionNames.Joy);
            dragonSlayedGoal.Should().NotBeNull();
            dragonSlayedGoal.Intensity.Value.Should().BeLessOrEqualTo(joyAfterKill);
        }

        /// <summary>
        ///     Two agents with the same goal name maintain independent likelihood state.
        ///     One agent's appraisal does not affect the other's emotional state.
        /// </summary>
        [Fact]
        public void TwoAgentsSharingGoalNameHaveIndependentState()
        {
            var gamygdala = new Gamygdala(
                new ExponentialDecayStrategy(0.8),
                new StaticRelationLikeStrategy());

            var alice = gamygdala.CreateAgent("alice");
            var bob = gamygdala.CreateAgent("bob");

            var sharedGoal = new Goal("shared goal", utility: 1.0);
            gamygdala.RegisterGoal(sharedGoal);

            alice.AddGoal(sharedGoal);
            bob.AddGoal(sharedGoal);

            var aliceFavorableBelief = new Belief(
                likelihood: 1.0,
                causalAgentName: "alice",
                affectedGoalNames: new[] { "shared goal" },
                goalCongruences: new GoalCongruence[] { 1 });

            gamygdala.Appraise(aliceFavorableBelief, alice);

            alice.TryGetGoalLikelihood("shared goal", out var aliceLikelihood).Should().BeTrue();
            bob.TryGetGoalLikelihood("shared goal", out var bobLikelihood).Should().BeFalse();

            aliceLikelihood.Value.Should().BeApproximately(1.0, 0.01);
            bobLikelihood.Value.Should().BeApproximately(0.0, 0.01);

            var aliceEmotions = alice.GetEmotionalState();
            var bobEmotions = bob.GetEmotionalState();

            aliceEmotions.Should().HaveCount(1);
            aliceEmotions[0].Name.Should().Be(EmotionNames.Joy);
            bobEmotions.Should().HaveCount(0);
        }
    }
}
