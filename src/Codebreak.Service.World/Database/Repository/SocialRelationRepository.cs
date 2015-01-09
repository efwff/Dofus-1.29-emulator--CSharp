﻿using Codebreak.Framework.Database;
using Codebreak.Service.World.Database.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codebreak.Service.World.Database.Repository
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class SocialRelationRepository : Repository<SocialRelationRepository, SocialRelationDAO>
    {
        /// <summary>
        /// 
        /// </summary>
        private Dictionary<long, List<SocialRelationDAO>> m_relationByAccountId;

        /// <summary>
        /// 
        /// </summary>
        public SocialRelationRepository()
        {
            m_relationByAccountId = new Dictionary<long, List<SocialRelationDAO>>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relation"></param>
        public override void OnObjectAdded(SocialRelationDAO relation)
        {
            if (!m_relationByAccountId.ContainsKey(relation.AccountId))
                m_relationByAccountId.Add(relation.AccountId, new List<SocialRelationDAO>());
            m_relationByAccountId[relation.AccountId].Add(relation);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relation"></param>
        public override void OnObjectRemoved(SocialRelationDAO relation)
        {
            m_relationByAccountId[relation.AccountId].Remove(relation);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="accountId"></param>
        /// <returns></returns>
        public List<SocialRelationDAO> GetByAccountId(long accountId)
        {
            if (!m_relationByAccountId.ContainsKey(accountId))
                m_relationByAccountId.Add(accountId, new List<SocialRelationDAO>());
            return m_relationByAccountId[accountId];
        }
    }
}
