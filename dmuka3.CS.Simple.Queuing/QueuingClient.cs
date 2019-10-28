using dmuka3.CS.Simple.RSA;
using dmuka3.CS.Simple.TCP;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace dmuka3.CS.Simple.Queuing
{
    /// <summary>
    /// Client for enqueue and dequeue.
    /// </summary>
    public class QueuingClient : IDisposable
    {
        #region Variables
        private object lockObj = new object();

        /// <summary>
        /// Wrong protocol exception.
        /// </summary>
        private static Exception __wrongProtocolException = new Exception("Wrong protocol!");

        /// <summary>
        /// Server's host name.
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// Server's port.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// TCP client.
        /// </summary>
        private TcpClient _client = null;

        /// <summary>
        /// For dmuka protocol.
        /// </summary>
        private TCPClientConnection _conn = null;

        /// <summary>
        /// RSA for client side.
        /// </summary>
        private RSAKey _rsaClient = null;

        /// <summary>
        /// RSA for server side.
        /// </summary>
        private RSAKey _rsaServer = null;
        #endregion

        #region Constructors
        /// <summary>
        /// Create an instance.
        /// </summary>
        /// <param name="hostName">Server's host name.</param>
        /// <param name="port">Server's port.</param>
        public QueuingClient(string hostName, int port)
        {
            this.HostName = hostName;
            this.Port = port;

            this._client = new TcpClient();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Connect the server.
        /// </summary>
        /// <param name="userName">Server authentication user name.</param>
        /// <param name="password">Server authentication password.</param>
        /// <param name="sslDwKeySize">SSL key size as bit.</param>
        public void Start(string userName, string password, int sslDwKeySize)
        {
            if (userName.Contains('<') || userName.Contains('>') || password.Contains('<') || password.Contains('>'))
                throw new Exception("UserName and Password can't containt '<' or '>'!");

            this._client.Connect(this.HostName, this.Port);
            this._conn = new TCPClientConnection(this._client);

            // SERVER : HI
            var serverHi = Encoding.UTF8.GetString(
                                this._conn.Receive()
                                );

            if (serverHi == QueuingMessages.SERVER_HI)
            {
                this._rsaClient = new RSAKey(sslDwKeySize);

                // CLIENT : public_key
                this._conn.Send(
                    Encoding.UTF8.GetBytes(
                        this._rsaClient.PublicKey
                        ));

                // SERVER : public_key
                this._rsaServer = new RSAKey(
                                    Encoding.UTF8.GetString(
                                        this._rsaClient.Decrypt(
                                            this._conn.Receive()
                                        )));

                // CLIENT : HI <user_name> <password>
                this._conn.Send(
                    this._rsaServer.Encrypt(
                        Encoding.UTF8.GetBytes(
                            $"{QueuingMessages.CLIENT_HI} <{userName}> <{password}>"
                            )));

                var serverResAuth = Encoding.UTF8.GetString(
                                        this._rsaClient.Decrypt(
                                            this._conn.Receive()
                                        ));

                if (serverResAuth == QueuingMessages.SERVER_NOT_AUTHORIZED)
                    // - IF AUTH FAIL
                    //      SERVER : NOT_AUTHORIZED
                    throw new Exception($"{QueuingMessages.SERVER_NOT_AUTHORIZED} - Not authorized!");
                else if (serverResAuth != QueuingMessages.SERVER_OK)
                    // - IF AUTH PASS
                    //      SERVER : OK
                    throw __wrongProtocolException;

            }
            else
                throw __wrongProtocolException;
        }

        #region Manage Datas
        /// <summary>
        /// Add a new value to queue.
        /// </summary>
        /// <param name="body">Queue's body.</param>
        /// <returns></returns>
        public void Enqueue(string body)
        {
            lock (lockObj)
            {
                // - IF PROCESS TYPE IS "ENQUEUE"
                //      CLIENT : ENQUEUE
                this._conn.Send(
                    this._rsaServer.Encrypt(
                        Encoding.UTF8.GetBytes(
                            $"{QueuingMessages.CLIENT_ENQUEUE}"
                            )));

                //      CLIENT : data
                this._conn.Send(
                    this._rsaServer.Encrypt(
                        Encoding.UTF8.GetBytes(
                            body
                            )));

                //      SERVER : END
                var serverResEnd = Encoding.UTF8.GetString(
                                        this._rsaClient.Decrypt(
                                            this._conn.Receive()
                                        ));

                if (serverResEnd.StartsWith(QueuingMessages.SERVER_ERROR))
                    throw new Exception(serverResEnd);
                else if (serverResEnd != QueuingMessages.SERVER_END)
                    throw __wrongProtocolException;
            }
        }

        /// <summary>
        /// Get a new value from queue.
        /// </summary>
        /// <returns></returns>
        public (string queueName, string body) Dequeue()
        {
            lock (lockObj)
            {
                // - IF PROCESS TYPE IS "DEQUEUE"
                //      CLIENT : DEQUEUE
                this._conn.Send(
                    this._rsaServer.Encrypt(
                        Encoding.UTF8.GetBytes(
                            $"{QueuingMessages.CLIENT_DEQUEUE}"
                            )));

                //      SERVER : queue_name
                var serverResQueueName = Encoding.UTF8.GetString(
                                        this._rsaClient.Decrypt(
                                            this._conn.Receive()
                                        ));

                if (serverResQueueName.StartsWith(QueuingMessages.SERVER_ERROR))
                    throw new Exception(serverResQueueName);

                //      SERVER : data
                var serverResData = Encoding.UTF8.GetString(
                                        this._rsaClient.Decrypt(
                                            this._conn.Receive()
                                        ));

                if (serverResData.StartsWith(QueuingMessages.SERVER_ERROR))
                    throw new Exception(serverResData);

                //      SERVER : END
                var serverResEnd = Encoding.UTF8.GetString(
                                        this._rsaClient.Decrypt(
                                            this._conn.Receive()
                                        ));

                if (serverResEnd.StartsWith(QueuingMessages.SERVER_ERROR))
                    throw new Exception(serverResEnd);
                else if (serverResEnd != QueuingMessages.SERVER_END)
                    throw __wrongProtocolException;

                return (queueName: serverResQueueName, body: serverResData);
            }
        }

        /// <summary>
        /// Remove a new value on queue.
        /// </summary>
        /// <param name="queueName">Queue's name.</param>
        /// <returns></returns>
        public void DequeueCompleted(string queueName)
        {
            if (queueName.Contains('<') || queueName.Contains('>'))
                throw new Exception("QueueName can't containt '<' or '>'!");

            lock (lockObj)
            {
                // - IF PROCESS TYPE IS "DEQUEUE COMPLETED"
                //      CLIENT : DEQUEUE_COMPLETED <queue_name>
                this._conn.Send(
                    this._rsaServer.Encrypt(
                        Encoding.UTF8.GetBytes(
                            $"{QueuingMessages.CLIENT_DEQUEUE_COMPLETED} <{queueName}>"
                            )));

                //      SERVER : END
                var serverResEnd = Encoding.UTF8.GetString(
                                        this._rsaClient.Decrypt(
                                            this._conn.Receive()
                                        ));

                if (serverResEnd.StartsWith(QueuingMessages.SERVER_ERROR))
                    throw new Exception(serverResEnd);
                else if (serverResEnd != QueuingMessages.SERVER_END)
                    throw __wrongProtocolException;
            }
        }
        #endregion

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            try
            {
                this._conn.Send(
                    this._rsaServer.Encrypt(
                        Encoding.UTF8.GetBytes(
                            $"{QueuingMessages.CLIENT_CLOSE}"
                            )));
            }
            catch
            { }
            this._conn.Dispose();
        }
        #endregion
    }
}
