﻿using System;
using System.Collections.Generic;
using System.Linq;
using Codebreak.Framework.Generic;
using Codebreak.RPC.Protocol;
using Codebreak.Service.World.Game;
using Codebreak.Service.World.RPC;
using Codebreak.Service.World.Network;

namespace Codebreak.Service.World.Manager
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class AccountTicket
    {
        public long Id;
        public string Name;
        public string Pseudo;
        public int Power;
        public DateTime RemainingSubscription;
        public DateTime LastConnectionTime;
        public string LastConnectionIP;
        public string Ticket;
        public long Time;
    }

    /// <summary>
    /// 
    /// </summary>
    public sealed class AccountManager : Singleton<AccountManager>
    {

        private Dictionary<long, WorldClient> m_clientByAccount;
        private Dictionary<string, AccountTicket> m_accountByTicket;

        /// <summary>
        /// 
        /// </summary>
        public AccountManager()
        {
            m_accountByTicket = new Dictionary<string, AccountTicket>();
            m_clientByAccount = new Dictionary<long, WorldClient>();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Initialize()
        {
            WorldService.Instance.AddTimer(1000, Update);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="accountId"></param>
        /// <param name="name"></param>
        /// <param name="power"></param>
        /// <param name="remainingSub"></param>
        /// <param name="lastConnection"></param>
        /// <param name="lastIp"></param>
        /// <param name="ticket"></param>
        public void AddTicket(long accountId, string name, string pseudo, int power, long remainingSub, long lastConnection, string lastIp, string ticket)
        {
            Logger.Info("GameTicket : account=" + name + " ticket=" + ticket);
            WorldService.Instance.AddMessage(() => m_accountByTicket.Add(ticket, new AccountTicket()
            { 
                Id = accountId,
                Pseudo = pseudo,
                Name = name,
                RemainingSubscription = new DateTime(remainingSub),
                Power = power,
                LastConnectionTime = new DateTime(lastConnection),
                LastConnectionIP = lastIp,
                Ticket = ticket, 
                Time = WorldService.Instance.LastUpdate
            }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ticket"></param>
        /// <returns></returns>
        public AccountTicket GetAccountTicket(string ticket)
        {
            AccountTicket account = null;
            if(m_accountByTicket.ContainsKey(ticket))
            {
                account = m_accountByTicket[ticket];
                m_accountByTicket.Remove(ticket);
            }
            return account;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public void ClientAuthentified(WorldClient client)
        {
            m_clientByAccount.Add(client.Account.Id, client);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public void ClientDisconnected(WorldClient client)
        {
            if(client.Account != null)
            {
                m_clientByAccount.Remove(client.Account.Id);

                RPCManager.Instance.AccountDisconnected(client.Account.Id);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void Update()
        {
            if (m_accountByTicket.Count > 0)
            {
                for (int i = m_accountByTicket.Count - 1; i > -1; i--)
                {
                    var value = m_accountByTicket.ElementAt(i).Value;
                    var elapsedTime = WorldService.Instance.LastUpdate - value.Time;

                    if (elapsedTime >= WorldConfig.RPC_ACCOUNT_TICKET_TIMEOUT)
                    {
                        Logger.Debug("Ticket timed out : " + value.Ticket);
                        m_accountByTicket.Remove(value.Ticket);
                        RPCManager.Instance.AccountDisconnected(value.Id);
                    }
                }
            }
        }
    }
}
