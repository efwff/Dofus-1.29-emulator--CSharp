﻿using Codebreak.Framework.Generic;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace Codebreak.Framework.Network
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TServer"></typeparam>
    /// <typeparam name="TClient"></typeparam>
    public abstract class TcpServerBase<TServer, TClient> : TaskProcessor<TServer>, IServer<TClient>
        where TServer : TcpServerBase<TServer, TClient>, new()
        where TClient : TcpClientBase<TClient>, new()
    {
        /// <summary>
        /// 
        /// </summary>
        private Socket m_socket;
        private ObjectPool<SocketAsyncEventArgs> m_sendPool;
        private ObjectPool<SocketAsyncEventArgs> m_recvPool;
        private BufferManager m_bufferManager;
        private ConcurrentStack<int> m_freeId;
        private ConcurrentDictionary<int, TClient> m_clients;
        public const int MAX_CLIENT = 10000;

        public string Host
        {
            get;
            private set;
        }

        public int Port
        {
            get;
            private set;
        }

        public int BackLog
        {
            get;
            private set;
        }

        public IEnumerable<TClient> Clients
        {
            get
            {
                return m_clients.Values;
            }
        }

        protected TcpServerBase(int maxClient = MAX_CLIENT)
            : base(typeof(TServer).Name)
        {
            m_bufferManager = new BufferManager(1024, 20000);
            m_sendPool = new ObjectPool<SocketAsyncEventArgs>(CreateSendSaea, 10000);
            m_recvPool = new ObjectPool<SocketAsyncEventArgs>(CreateRecvSaea, 10000);
            m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_clients = new ConcurrentDictionary<int, TClient>();
            m_freeId = new ConcurrentStack<int>();
            for (int i = maxClient; i > 0; i--)
                m_freeId.Push(i);
        }

        private SocketAsyncEventArgs CreateSendSaea()
        {
            var saea = new SocketAsyncEventArgs();
            saea.Completed += IOCompleted;
            return saea;
        }

        private PoolableSocketAsyncEventArgs CreateRecvSaea()
        {
            var saea = new PoolableSocketAsyncEventArgs(m_bufferManager);
            saea.Completed += IOCompleted;
            return saea;
        }

        protected void Start(string host, int port, int backLog = 100)
        {
            Host = host;
            Port = port;
            BackLog = backLog;

            m_socket.NoDelay = true;
            m_socket.Bind(new IPEndPoint(IPAddress.Parse(Host), Port));
            m_socket.Listen(BackLog);

            for(int i = 0; i < BackLog; i++)
            {
                StartAccept(null);
            }
        }

        private void AsyncSafe(Func<SocketAsyncEventArgs, bool> func, SocketAsyncEventArgs arg)
        {
            if (!func(arg))
                IOCompleted(this, arg);
        }

        private void IOCompleted(object sender, SocketAsyncEventArgs saea)
        {
            switch (saea.LastOperation)
            {
                case SocketAsyncOperation.Accept: ProcessAccepted(saea); break;
                case SocketAsyncOperation.Receive: ProcessReceived(saea); break;
                case SocketAsyncOperation.Send: ProcessSent(saea); break;
                case SocketAsyncOperation.Disconnect: ProcessDisconnected(saea); break;
                default:
                    break;
            }
        }

        private void ProcessDisconnected(SocketAsyncEventArgs saea)
        {
            Disconnect((TClient)saea.UserToken);
        }

        private void StartAccept(SocketAsyncEventArgs saea)
        {
            if(saea == null)
            {
                saea = new SocketAsyncEventArgs();
                saea.Completed += IOCompleted;
            }
            else
            {
                saea.AcceptSocket = null;
            }

            AsyncSafe(m_socket.AcceptAsync, saea);
        }

        private void StartReceive(SocketAsyncEventArgs saea, TClient client)
        {
            if (saea == null)
            {
                saea = m_recvPool.Pop();
                saea.UserToken = client;
                saea.AcceptSocket = client.Socket;
            }

            AsyncSafe(client.Socket.ReceiveAsync, saea);
        }

        private bool AddClient(TClient client)
        {
            int clientId = -1;
            if (!m_freeId.TryPop(out clientId))
                return false;
            client.Id = clientId;
            return m_clients.AddOrUpdate(clientId, client, (id, cl) => client) == client;
        }

        private void ProcessAccepted(SocketAsyncEventArgs saea)
        {
            // get connected socket
            var socket = saea.AcceptSocket;

            StartAccept(saea);

            // create new client
            var client = new TClient();
            client.Socket = socket;
            client.Ip = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
            client.Server = this;

            // add the client
            if (AddClient(client))
            {
                // start receiving data
                StartReceive(null, client);

                // raise client connection
                OnClientConnected(client);
            }
            else
            {
                // server busy
                Disconnect(client);
            }
        }

        private void ProcessReceived(SocketAsyncEventArgs saea)
        {
            var client = (TClient)saea.UserToken;

            // client disconnected
            if(saea.BytesTransferred == 0)
            {
                saea.Completed -= IOCompleted;
                Disconnect(client);
                return;
            }
            
            // raise event
            OnDataReceived(client, saea.Buffer, saea.Offset, saea.BytesTransferred);
            
            StartReceive(saea, client);
        }

        private void ProcessSent(SocketAsyncEventArgs saea)
        {
            saea.SetBuffer(null, 0, 0);
            m_sendPool.Push(saea);
        }

        public void Disconnect(TClient client)
        {
            if (client == null)
                return;
            
            var socket = client.Socket;
            
            socket.Shutdown(SocketShutdown.Both);

            if (socket.Connected) 
                socket.Disconnect(false);

            if (client.Id != -1)
            {
                m_clients.TryRemove(client.Id, out client);

                if (client != null)
                {
                    m_freeId.Push(client.Id);

                    OnClientDisconnected(client);
                }
            }
        }

        public void Send(TClient client, byte[] data)
        {
            if (client == null)
                return;
            
            var saea = m_sendPool.Pop();
            saea.SetBuffer(data, 0, data.Length);
            AsyncSafe(client.Socket.SendAsync, saea);
        }

        public void SendToAll(byte[] data)
        {
            foreach (var client in m_clients.Values)
                Send(client, data);
        }

        protected abstract void OnClientConnected(TClient client);
        protected abstract void OnClientDisconnected(TClient client);
        protected abstract void OnDataReceived(TClient client, byte[] buffer, int offset, int count);
    }
}
