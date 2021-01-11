using GamygdalaNet.Agents;
using GamygdalaNet.Types;

namespace GamygdalaNet.RelationLikeStrategies
{
    public interface IRelationLikeStrategy
    {
        /// <summary>
        ///     Sets the relation this agent has with the target agent. If the relation does not exist, it will be created,
        ///     otherwise it will be updated.
        /// </summary>
        /// <param name="self">Agent owning the relation towards <see cref="targetAgentName" />.</param>
        /// <param name="targetAgentName">The agent who is the target of the relation.</param>
        /// <param name="like">The relation, or how much the target agent is liked, from -1 (disliked) to 1 (liked).</param>
        void UpdateRelation(Agent self, string targetAgentName, DoubleNegativeOneToPositiveOneInclusive like);
    }
}