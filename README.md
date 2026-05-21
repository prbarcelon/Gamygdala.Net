# GAMYGDALA C# Port

GAMYGDALA is an emotional appraisal engine for game NPCs. It implements the OCC (Ortony, Clore, Collins) emotion model. The output is a per-agent discrete emotional state plus a dimensional PAD (Pleasure, Arousal, Dominance) vector.

Port of [Gamygdala](https://github.com/broekens/gamygdala) from JavaScript to C# with inspiration from the [Gamygdala Java Port](https://github.com/tygron-virtual-humans/port-gamygdala).

## Publication

- Original paper: Popescu, A., Broekens, J., & Van Someren, M. (2013). GAMYGDALA: An emotion engine for games. IEEE Transactions on Affective Computing, 5, 32-44.
- http://ii.tudelft.nl/~joostb/gamygdala/
- https://www.researchgate.net/publication/262150526_GAMYGDALA_An_emotion_engine_for_games
- https://ieeexplore.ieee.org/document/6636311

## Table of Contents

1. [Quickstart](#quickstart)
2. [Agents and Goals](#agents-and-goals)
3. [Beliefs and Appraisal](#beliefs-and-appraisal)
4. [PAD Output](#pad-output)
5. [Relations and Social Emotions](#relations-and-social-emotions)
6. [Decay](#decay)
7. [Maintenance vs Achievement Goals](#maintenance-vs-achievement-goals)
8. [Per-Agent Goal State](#per-agent-goal-state)
9. [Default Emotional State (Personality Baseline)](#default-emotional-state-personality-baseline)

## Quickstart

Create an engine, add an agent with a goal, appraise a belief, and read the resulting emotions:

```csharp
// Engine takes a decay strategy (controls how emotion intensity
// fades over time) and a relation-like strategy (controls how
// relationships update from events).
var gamygdala = new Gamygdala(
    new ExponentialDecayStrategy(0.8),
    new StaticRelationLikeStrategy());

// Create the NPC and a goal it cares about. Negative utility
// means the NPC wants this NOT to happen (dying is undesirable).
var blacksmith = gamygdala.CreateAgent("blacksmith");
var stayAliveGoal = gamygdala.CreateGoalForAgent("blacksmith", "stay alive", goalUtility: -1.0);

// A belief is a game event: how certain it is (likelihood), who
// caused it, which goals it affects, and how strongly it pushes
// each goal toward/away from achievement (congruence).
var damageBelief = new Belief(
    likelihood: 0.8,
    causalAgentName: null,
    affectedGoalNames: new[] { "stay alive" },
    goalCongruences: new GoalCongruence[] { 1 });

// Appraise the belief for this specific agent.
gamygdala.Appraise(damageBelief, blacksmith);

// Read the agent's resulting emotional state.
var emotions = blacksmith.GetEmotionalState();
// emotions = [Fear at intensity 0.900]
// (negative-utility goal + positive congruence on damage event = Fear)
```

See [`ReadmeExamplesShould.Quickstart`](src/GamygdalaNet.Test/ReadmeExamplesShould.cs) for the runnable version.

## Agents and Goals

An `Agent` is an NPC that experiences emotions. A `Goal` is an immutable definition of a state the NPC cares about; its `Utility` field, in [-1, 1], says how much. Per-agent likelihood state lives on the `Agent`, not on the `Goal`, so two agents can share a single `Goal` reference without their likelihoods aliasing.

Adding a goal to an agent records the goal definition on that agent. On appraisal, only the appraised agent's stored likelihood changes.

```csharp
var gamygdala = new Gamygdala(
    new ExponentialDecayStrategy(0.8),
    new StaticRelationLikeStrategy());

var alice = gamygdala.CreateAgent("alice");
var bob = gamygdala.CreateAgent("bob");

// Create the goal once. CreateGoalForAgent registers the goal
// with the engine and binds it to alice's goal set.
var treasureGoal = gamygdala.CreateGoalForAgent("alice", "find treasure", goalUtility: 1.0);
// Share the same Goal reference with bob. Both agents now have
// the same goal definition but their own private likelihood state.
bob.AddGoal(treasureGoal);

// "Treasure found" - certain (likelihood 1.0), facilitates the goal.
var treasureFoundBelief = new Belief(
    likelihood: 1.0,
    causalAgentName: "alice",
    affectedGoalNames: new[] { "find treasure" },
    goalCongruences: new GoalCongruence[] { 1 });

// Appraise per-agent so each updates its own likelihood independently.
gamygdala.Appraise(treasureFoundBelief, alice);
gamygdala.Appraise(treasureFoundBelief, bob);

alice.TryGetGoalLikelihood("find treasure", out var aliceLikelihood);
bob.TryGetGoalLikelihood("find treasure", out var bobLikelihood);
// aliceLikelihood = 1.000, bobLikelihood = 1.000
// alice.GetEmotionalState() = [Joy at 1.000]
//   (alice is also the causal agent on her own appraisal,
//    so no social Gratitude fires)
// bob.GetEmotionalState() = [Joy at 1.000, Gratitude at 1.000 toward alice]
//   (alice is OTHER from bob's perspective, so social Gratitude fires)
```

Per Popescu et al. §3.4.2 *Goal Likelihood* the initial likelihood is "Unknown" (no value recorded). The first appraisal records it; subsequent appraisals update it.

See [`ReadmeExamplesShould.AgentsAndGoals_SharedGoalHasIndependentLikelihood`](src/GamygdalaNet.Test/ReadmeExamplesShould.cs) for the runnable version.

## Beliefs and Appraisal

A **Belief** is an annotated game event with:
- **Likelihood**: how certain the event is (0 to 1)
- **Causal Agent**: who caused the event
- **Affected Goals**: which goals the event relates to
- **Goal Congruences**: how good (1) or bad (-1) the event is for each goal

When you appraise a belief, Gamygdala calculates the emotion intensity based on the goal's utility, the belief's congruence with that goal, and the change in the goal's likelihood. Popescu et al. (equation 3) defines intensity as `|utility * delta_likelihood|`.

### Positive Appraisal (Goal Facilitated)

When a belief has positive congruence (event facilitates the goal), the goal's likelihood increases, producing Joy (if the goal is desirable) or Relief (if the goal is undesirable and this reduces its likelihood).

```csharp
var gamygdala = new Gamygdala(
    new ExponentialDecayStrategy(0.8),
    new StaticRelationLikeStrategy());

var healer = gamygdala.CreateAgent("healer");
// Positive utility: healing patients is desirable.
gamygdala.CreateGoalForAgent("healer", "heal patient", goalUtility: 0.9);

// Healer heals self: certain event, positive congruence with goal.
// Self as causal agent skips the social-emotion path.
var patientHealing = new Belief(
    likelihood: 1.0,
    causalAgentName: "healer",
    affectedGoalNames: new[] { "heal patient" },
    goalCongruences: new GoalCongruence[] { 1 });

gamygdala.Appraise(patientHealing, healer);

var emotions = healer.GetEmotionalState();
// emotions = [Joy at intensity 0.900]
// intensity = |utility * delta| = |0.9 * 1.0| (first appraisal,
// no prior likelihood, so delta = newLikelihood = 1.0)
```

The causal agent equals the affected agent (the healer healed themselves), so the social-emotion path (Gratitude toward a benefactor) does not fire.

See [`ReadmeExamplesShould.PositiveAppraisalProducesJoy`](src/GamygdalaNet.Test/ReadmeExamplesShould.cs) for the runnable version.

### Negative Appraisal (Goal Blocked)

When a belief has negative congruence (event blocks the goal), the goal's likelihood decreases, producing Fear or Distress. When a different agent caused the harm, Anger toward the causal agent also fires via the social-emotion path (Gratitude's negative counterpart).

```csharp
var gamygdala = new Gamygdala(
    new ExponentialDecayStrategy(0.8),
    new StaticRelationLikeStrategy());

var defender = gamygdala.CreateAgent("defender");
// Negative utility: defender does NOT want the village to fall.
gamygdala.CreateGoalForAgent("defender", "village survives", goalUtility: -0.8);

// "Invader" caused the attack; positive congruence pushes the
// (negatively-utilized) goal toward failure.
var villageAttacked = new Belief(
    likelihood: 0.9,
    causalAgentName: "invader",
    affectedGoalNames: new[] { "village survives" },
    goalCongruences: new GoalCongruence[] { 1 });

gamygdala.Appraise(villageAttacked, defender);

var emotions = defender.GetEmotionalState();
// emotions = [Fear at 0.760, Anger at 0.760 toward "invader"]
// intensity = |utility * delta| = |-0.8 * 0.95| (first appraisal,
// newLikelihood = (1*0.9+1)/2 = 0.95)
```

See [`ReadmeExamplesShould.NegativeAppraisalProducesDistress`](src/GamygdalaNet.Test/ReadmeExamplesShould.cs) for the runnable version.

## PAD Output

In addition to discrete emotions, Gamygdala computes PAD (Pleasure, Arousal, Dominance) via the Reilly logarithmic aggregator (Popescu et al. equation 5):

```
PAD ← 0.1 × log₂(∑_e 2^(10 × PAD(e) × intensity(e)))
```

Each emotion carries a PAD vector. When multiple emotions are active they combine logarithmically, not additively: the result is "at least as intense as the most intense component" but never sums past the natural range. The paper credits Reilly's logarithmic function with not being strictly additive, using all emotions rather than only the strongest, and staying at least as intense as the most intense contributor.

```csharp
var gamygdala = new Gamygdala(
    new ExponentialDecayStrategy(0.8),
    new StaticRelationLikeStrategy());

// Merchant has two competing goals: positive (sell) and negative (avoid theft).
var merchant = gamygdala.CreateAgent("merchant");
gamygdala.CreateGoalForAgent("merchant", "sell goods", goalUtility: 0.7);
gamygdala.CreateGoalForAgent("merchant", "avoid theft", goalUtility: -0.6);

// Good news: a customer bought.
var goodsSold = new Belief(
    likelihood: 1.0,
    causalAgentName: "customer",
    affectedGoalNames: new[] { "sell goods" },
    goalCongruences: new GoalCongruence[] { 1 });

// Bad news: bandits are around (partial certainty).
var theftThreats = new Belief(
    likelihood: 0.4,
    causalAgentName: "bandit",
    affectedGoalNames: new[] { "avoid theft" },
    goalCongruences: new GoalCongruence[] { 1 });

gamygdala.Appraise(goodsSold, merchant);
gamygdala.Appraise(theftThreats, merchant);

// Both appraisals stack into the merchant's emotional state:
// [Joy=0.700, Gratitude=0.700 toward customer,
//  Fear=0.420, Anger=0.420 toward bandit]

var padState = merchant.GetPadState();
// padState = (Pleasure=+0.597, Arousal=+0.457, Dominance=+0.303)
// The positive emotions dominate but the bandit threat pulls
// arousal up and softens pleasure. Log-sum-exp aggregation
// preserves direction without naive summation overshoot.
```

The PAD values are clamped to [-1, 1] to handle the rare case where multiple high-intensity emotions of the same polarity exceed the natural range.

See [`ReadmeExamplesShould.PadOutputAggregatesEmotionsLogarithmically`](src/GamygdalaNet.Test/ReadmeExamplesShould.cs) for the runnable version.

## Relations and Social Emotions

Beyond internal emotions (Fear, Joy, etc.), Gamygdala models **social emotions**: emotions felt toward other agents. These arise when one agent causes an event affecting another's goals. The intensity depends on the relationship (how much the observer likes or dislikes the agent) and whether the event is desirable or undesirable.

To create a social emotion, establish a relation between agents, then appraise a belief where another agent is the causal agent.

```csharp
var gamygdala = new Gamygdala(
    new ExponentialDecayStrategy(0.8),
    new StaticRelationLikeStrategy());

var alice = gamygdala.CreateAgent("alice");
var bob = gamygdala.CreateAgent("bob");

// Alice's goal + her positive relationship with bob.
gamygdala.CreateGoalForAgent("alice", "stay healthy", goalUtility: 0.9);
gamygdala.CreateRelation("alice", "bob", relation: 1.0);

// Bob does something that facilitates alice's "stay healthy" goal.
// Causal agent = bob (not alice), so the social-emotion path fires.
var bobHealsAlice = new Belief(
    likelihood: 1.0,
    causalAgentName: "bob",
    affectedGoalNames: new[] { "stay healthy" },
    goalCongruences: new GoalCongruence[] { 1 });

gamygdala.Appraise(bobHealsAlice, alice);

var emotions = alice.GetEmotionalState();
// emotions = [Joy at 0.900, Gratitude at 0.900 toward bob]
// Joy comes from goal facilitation; Gratitude comes from AgentActions
// case "self is affected, other is causal, desirability >= 0".

alice.TryGetRelation("bob", out var relationWithBob);
// relationWithBob.EmotionsList = [Gratitude at 0.900]
// The Gratitude is also attached to the specific relation so other
// agents can query "how does alice feel toward bob?"
```

Relations also affect Pity, Resentment, HappyFor, Gloating, Gratification, and Remorse. See the paper's §3.4.4 for full social emotion conditions.

Social emotions are recorded in two places: on the `Relation` between observer and target (`relation.EmotionsList`) and on the observer's overall emotional state (`agent.GetEmotionalState()`). The duplication is intentional: the relation entry answers "how does X feel about Y?" while the overall state feeds PAD aggregation. Listings 1, 3, 4 are pinned against this convention; assertions in `GamygdalaShould` reflect both surfaces.

The worked example is Listing 3 (the Pride scenario). It runs in `GamygdalaShould.CalculateExpectedEmotionalStateOfGivenAgent_WhenGivenPrideScenario`: a blacksmith who likes a village feels Gratitude when the village shelters him, Pity when the village is threatened, then both Gratification and HappyFor when he helps defend it.

See [`ReadmeExamplesShould.RelationsAndSocialEmotions_GratefulTowardsBenefactor`](src/GamygdalaNet.Test/ReadmeExamplesShould.cs) for the runnable version.

## Decay

Emotions fade over time. Call `DecayAll()` with a time delta to apply the decay strategy to every agent.

```csharp
var gamygdala = new Gamygdala(
    new ExponentialDecayStrategy(0.8),
    new StaticRelationLikeStrategy());

var warrior = gamygdala.CreateAgent("warrior");
gamygdala.CreateGoalForAgent("warrior", "defeat enemy", goalUtility: 1.0);

// Maximally certain positive event; no causal agent so no social path.
var enemyDefeated = new Belief(
    likelihood: 1.0,
    causalAgentName: null,
    affectedGoalNames: new[] { "defeat enemy" },
    goalCongruences: new GoalCongruence[] { 1 });

gamygdala.Appraise(enemyDefeated, warrior);
var emotionsInitial = warrior.GetEmotionalState();
var initialJoyIntensity = emotionsInitial[0].Intensity.Value; // 1.000

// Advance simulated time. The decay strategy applies its function
// per millisecond passed; with ExponentialDecayStrategy(0.8) and
// 3000ms the intensity is multiplied by 0.8^3 = 0.512.
gamygdala.DecayAll(millisPassed: 3000);

var emotionsAfterDecay = warrior.GetEmotionalState();
var joyAfterDecay = emotionsAfterDecay[0].Intensity.Value; // 0.512
// joyAfterDecay < initialJoyIntensity
```

The decay function is passed into the engine's constructor as an `IDecayStrategy`. `ExponentialDecayStrategy` applies `intensity * decay_factor^(time_seconds)`. Higher factors (closer to 1) slow the decay; lower factors (closer to 0) speed it up.

See [`ReadmeExamplesShould.DecayReducesEmotionIntensityOverTime`](src/GamygdalaNet.Test/ReadmeExamplesShould.cs) for the runnable version.

## Maintenance vs Achievement Goals

Gamygdala supports two goal types:

- **Achievement Goal** (default): once the goal's stored likelihood reaches 1 (achieved) or 0 (permanently failed), further appraisals do not change the likelihood. The goal is considered settled. (The paper's signed `[-1, +1]` likelihood notation maps to this port's `[0, 1]` domain via equation 2; `-1` in the paper is `0` here.)
- **Maintenance Goal**: the likelihood can cycle indefinitely. Examples: "stay fed" (can be true today, false tomorrow, true again), "avoid pain", "maintain relationship".

When creating a goal, set `isMaintenanceGoal = true` to enable cycling:

```csharp
var gamygdala = new Gamygdala(
    new ExponentialDecayStrategy(0.8),
    new StaticRelationLikeStrategy());

var animal = gamygdala.CreateAgent("animal");
// isMaintenanceGoal: true marks the goal as cyclable. The likelihood
// can move back toward "Unknown" between events; the goal does not
// saturate after a single achievement.
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

gamygdala.Appraise(foodAvailable1, animal);
var firstEmotions = animal.GetEmotionalState(); // [Joy at 0.700]

// Advance time; the first Joy decays.
gamygdala.DecayAll(millisPassed: 5000);

// Same event later in the day; for a maintenance goal this fires
// emotion again rather than being suppressed by saturation.
var foodAvailable2 = new Belief(
    likelihood: 1.0,
    causalAgentName: null,
    affectedGoalNames: new[] { "stay fed" },
    goalCongruences: new GoalCongruence[] { 1 });

gamygdala.Appraise(foodAvailable2, animal);
var secondEmotions = animal.GetEmotionalState(); // [Joy at 0.229]
// secondEmotions still contains Joy (decayed first appraisal +
// near-zero delta from the second since likelihood already at 1).
// The maintenance flag is what allows the goal to keep accepting
// belief updates instead of locking in at saturation.
```

See [`ReadmeExamplesShould.MaintenanceGoalCanFireRepeatedly`](src/GamygdalaNet.Test/ReadmeExamplesShould.cs) and [`ReadmeExamplesShould.AchievementGoalSaturesAchievementGoalStabilizes`](src/GamygdalaNet.Test/ReadmeExamplesShould.cs) for runnable examples.

## Per-Agent Goal State

Per-agent likelihood state lives on the `Agent`, not on the `Goal`. Two agents that share a goal definition do not share its likelihood. `agent.TryGetGoalLikelihood("goal name", out var likelihood)` returns whatever THAT agent has recorded; another agent with the same goal carries its own value.

Define the goal once, add it to every agent that cares about it, let each agent track its own likelihood. Two NPCs that both hold "stay alive" or "be liked" do not alias each other through the `Goal`.

```csharp
var gamygdala = new Gamygdala(
    new ExponentialDecayStrategy(0.8),
    new StaticRelationLikeStrategy());

var alice = gamygdala.CreateAgent("alice");
var bob = gamygdala.CreateAgent("bob");

// Build the goal as an immutable definition and share it. The
// Goal object itself has no per-agent state, so handing the
// same reference to two agents is safe.
var sharedGoal = new Goal("shared goal", utility: 1.0);
gamygdala.RegisterGoal(sharedGoal);

alice.AddGoal(sharedGoal);
bob.AddGoal(sharedGoal);

// Appraise the belief ONLY for alice. Bob holds the same goal but
// has never been appraised against it.
var aliceFavorableBelief = new Belief(
    likelihood: 1.0,
    causalAgentName: "alice",
    affectedGoalNames: new[] { "shared goal" },
    goalCongruences: new GoalCongruence[] { 1 });

gamygdala.Appraise(aliceFavorableBelief, alice);

alice.TryGetGoalLikelihood("shared goal", out var aliceLikelihood); // true, 1.000
bob.TryGetGoalLikelihood("shared goal", out var bobLikelihood);     // false (bob has no recorded state)

var aliceEmotions = alice.GetEmotionalState(); // [Joy at 1.000]
var bobEmotions = bob.GetEmotionalState();     // []  (no appraisal, no emotion)

// Each agent is the sole owner of its per-goal likelihood, so
// two NPCs that both carry "be liked" or "stay alive" do not
// alias each other through the shared Goal definition.
```

See [`ReadmeExamplesShould.TwoAgentsSharingGoalNameHaveIndependentState`](src/GamygdalaNet.Test/ReadmeExamplesShould.cs) for the runnable version.

## Default Emotional State (Personality Baseline)

Per Popescu §3.4.6 each NPC has a default emotional state that the engine decays toward, rather than decaying everything to zero. Set per-emotion defaults via `Agent.SetDefaultEmotionIntensity` before any appraisals run; the agent's emotional state is seeded at those defaults and gravitates back to them under decay.

```csharp
var anxiousOptimist = gamygdala.CreateAgent("anxious-optimist");
anxiousOptimist.SetDefaultEmotionIntensity(EmotionNames.Joy, 0.2);
anxiousOptimist.SetDefaultEmotionIntensity(EmotionNames.Fear, 0.1);

// Reads back the defaults before any appraisal:
// [Joy 0.200, Fear 0.100]
var resting = anxiousOptimist.GetEmotionalState();
```

The substrate accepts the defaults; it does not compute them. Consumers wire personality models (OCEAN via AlmaNet, Schwartz values, custom NPC archetypes, etc.) into this seam. The OCEAN→PAD mapping lives in sibling libraries so each consumer keeps its own tuning; Gamygdala stays personality-model-agnostic.

Emotions without a default decay to zero (the historical behavior). Emotions with a positive default decay toward that default and are never removed.

## Core API Reference

### Gamygdala

- `CreateAgent(name)` - create and register an agent
- `CreateGoalForAgent(agentName, goalName, utility, isMaintenanceGoal)` - create, register, and add a goal to an agent
- `RegisterAgent(agent)` - register an agent
- `RegisterGoal(goal)` - register a goal definition globally
- `CreateRelation(sourceAgentName, targetAgentName, like)` - establish a relation between agents
- `Appraise(belief, affectedAgent = null)` - appraise a belief for all agents or one specific agent
- `DecayAll(millisPassed)` - decay emotions for all agents

### Agent

- `AddGoal(goal)` - add a goal definition to this agent
- `HasGoal(goalName)` - check if agent owns a goal
- `TryGetGoal(goalName, out goal)` - get the agent's copy of the goal definition
- `TryGetGoalLikelihood(goalName, out likelihood)` - get the agent's current likelihood for a goal
- `SetGoalLikelihood(goalName, likelihood)` - update the agent's goal likelihood
- `HasGoalLikelihood(goalName)` - check if the agent has recorded a likelihood for a goal
- `GetEmotionalState(useGain)` - get array of emotions with intensities
- `GetPadState(useGain)` - get PAD (Pleasure, Arousal, Dominance) values
- `SetDefaultEmotionIntensity(emotionName, defaultIntensity)` - set the resting intensity this emotion decays toward (paper §3.4.6)
- `TryGetDefaultEmotionIntensity(emotionName, out defaultIntensity)` - read the resting intensity for a named emotion
- `UpdateRelation(targetAgentName, like, isLikeAdditive)` - set or update a relation
- `TryGetRelation(targetAgentName, out relation)` - get a relation
- `GetRelations()` - get all relations for this agent
- `Decay(gamygdala)` - decay emotions per the decay strategy

### Goal

- `Name` - goal identifier
- `Utility` - how much the NPC values this goal ([-1, 1])
- `IsMaintenanceGoal` - whether the goal can cycle (true) or settles once achieved/failed (false)
- `CustomLikelihoodCalculation` - optional function to compute likelihood instead of from beliefs

### Belief

- `Likelihood` - how certain the belief is [0, 1]
- `CausalAgentName` - agent responsible for the event
- `AffectedGoalNames` - goal names this belief relates to
- `GoalCongruences` - how good (1) or bad (-1) each goal is affected

## Emotion Names

```csharp
EmotionNames.Joy           // Desirable goal achieves
EmotionNames.Distress      // Desirable goal fails
EmotionNames.Hope          // Uncertain positive outcome
EmotionNames.Fear          // Uncertain negative outcome
EmotionNames.Satisfaction  // Expected goal achieves
EmotionNames.FearConfirmed // Expected negative goal achieves
EmotionNames.Disappointment // Expected goal fails
EmotionNames.Relief        // Expected negative goal fails
EmotionNames.HappyFor      // Good happens to liked agent
EmotionNames.Resentment    // Good happens to disliked agent
EmotionNames.Pity          // Bad happens to liked agent
EmotionNames.Gloating      // Bad happens to disliked agent
EmotionNames.Gratitude     // Liked agent causes good
EmotionNames.Anger         // Disliked agent causes bad
EmotionNames.Gratification // You cause good for liked agent
EmotionNames.Remorse       // You cause bad for liked agent
```

## Decay Strategies

Two strategies are provided:

- `ExponentialDecayStrategy(decayFactor)` - applies `intensity * decayFactor^(time_seconds)`. Factor 0.8 decays emotions slower; 0.5 faster.
- `LinearDecayStrategy(decayFactor)` - applies `intensity - decayFactor * time_seconds`. Decays to zero linearly.

Implement `IDecayStrategy` to provide custom decay behavior.

## Relation Strategies

- `StaticRelationLikeStrategy()` - relations are set explicitly and do not change over time.
- `AdditiveRelationLikeStrategy()` - relations update additively as events occur (desirable events increase like, undesirable events decrease it).

## Port-specific behaviors

A few features in this port ship beyond what Popescu's paper specifies. Each is inherited from the upstream JavaScript implementation. Default behavior is paper-faithful; the extensions are opt-in.

- **`Goal.CustomLikelihoodCalculation`.** Replaces equation 2 with a caller-supplied function. The certainty-snap is suppressed for goals with a custom calc, so the custom return value wins. The achievement-goal short-circuit still applies, so once a custom-calc achievement goal reaches 0 or 1 the custom function stops being invoked.
- **`Goal.LikelihoodDecayRate`.** Per-tick fraction by which each agent's stored likelihood for the goal glides back toward the "Unknown" prior of 0.5. Useful for social-needs goals like "be liked" or "be respected": without decay, a single confirmation locks the goal at 1.0 and subsequent positive beliefs produce no meaningful delta. With a small positive rate, the goal becomes responsive to fresh validation again after time passes. Default 0 (no decay, paper-faithful). Applied by `Agent.Decay` alongside the existing emotion + relation decay.
- **`GetEmotionalState(useGain=true)` and `GetPadState(useGain=true)`.** Sigmoid-style gain compression on top of the discrete-emotion intensities and the PAD aggregator. The paper specifies neither; default to `useGain=false` for paper-faithful output.

Personality-as-default-mood (§3.4.6), once a known gap, is now implemented; see [Default Emotional State](#default-emotional-state-personality-baseline) above.
