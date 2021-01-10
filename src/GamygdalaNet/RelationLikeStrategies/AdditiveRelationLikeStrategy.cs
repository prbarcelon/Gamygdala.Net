using GamygdalaNet.Agents;
using GamygdalaNet.Types;

namespace GamygdalaNet.RelationLikeStrategies
{
    /// <summary>
    ///     Like value is calculated from desirability of the belief (event) and is added to the existing relation's like
    ///     value.
    /// </summary>
    public class AdditiveRelationLikeStrategy : IRelationLikeStrategy
    {
        public void UpdateRelation(Agent self, string causalAgentName, DoubleNegativeOneToPositiveOneInclusive like)
        {
            self.UpdateRelation(causalAgentName, like, true);
        }
    }
}