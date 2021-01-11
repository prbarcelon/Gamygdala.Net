using GamygdalaNet.Agents;
using GamygdalaNet.Types;

namespace GamygdalaNet.RelationLikeStrategies
{
    /// <summary>
    ///     Like value is initialized to 0.0 if no existing relation exists. Like value of relation must be set manually to get
    ///     a non-zero like value.
    /// </summary>
    public class StaticRelationLikeStrategy : IRelationLikeStrategy
    {
        public void UpdateRelation(Agent self, string targetAgentName, DoubleNegativeOneToPositiveOneInclusive like)
        {
            if (!self.HasRelationWith(targetAgentName))
                self.UpdateRelation(targetAgentName, 0);
        }
    }
}