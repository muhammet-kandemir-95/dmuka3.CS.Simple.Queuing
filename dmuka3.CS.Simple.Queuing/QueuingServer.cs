using dmuka3.CS.Simple.RSA;
using dmuka3.CS.Simple.Semaphore;
using dmuka3.CS.Simple.TCP;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace dmuka3.CS.Simple.Queuing
{
    /// <summary>
    /// Server to manage queues.
    /// </summary>
    public class QueuingServer : IDisposable
    {
        #region Variables
        /// <summary>
        /// Server port.
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// Time out auth as second.
        /// </summary>
        public int TimeOutAuth { get; private set; }

        /// <summary>
        /// Server authentication user name.
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>
        /// Server authentication password.
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// Maximum connection count at the same time processing.
        /// </summary>
        public int CoreCount { get; private set; }

        /// <summary>
        /// SSL key size as bit.
        /// </summary>
        public int SSLDwKeySize { get; private set; }

        /// <summary>
        /// Server.
        /// </summary>
        private TcpListener _listener = null;

        /// <summary>
        /// Manage the semaphore for connections.
        /// </summary>
        private ActionQueue _actionQueueConnections = null;

        /// <summary>
        /// Queues files' directory path.
        /// </summary>
        public const string QueuesDirectory = "Queues";

        /// <summary>
        /// Avoid to cross same time queue.
        /// </summary>
        private ushort _fileCounter = 0;
        #endregion

        #region Constructors
        /// <summary>
        /// Instance server.
        /// </summary>
        /// <param name="userName">Server authentication user name.</param>
        /// <param name="password">Server authentication password.</param>
        /// <param name="sslDwKeySize">SSL key size as bit.</param>
        /// <param name="coreCount">Maximum connection count at the same time processing.</param>
        /// <param name="port">Server port.</param>
        /// <param name="timeOutAuth">Time out auth as second.</param>
        public QueuingServer(string userName, string password, int sslDwKeySize, int coreCount, int port, int timeOutAuth = 1)
        {
            this.UserName = userName;
            this.Password = password;
            this.TimeOutAuth = timeOutAuth;
            this.SSLDwKeySize = sslDwKeySize;
            this._listener = new TcpListener(IPAddress.Any, port);
            this._actionQueueConnections = new ActionQueue(coreCount);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Start the server as sync.
        /// </summary>
        public void Start()
        {
            this._actionQueueConnections.Start();
            this._listener.Start();
            var rsaServer = new RSAKey(this.SSLDwKeySize);

            while (true)
            {
                TcpClient client = null;

                try
                {
                    client = this._listener.AcceptTcpClient();
                }
                catch
                {
                    break;
                }

                this._actionQueueConnections.AddAction(() =>
                {
                    try
                    {
                        var conn = new TCPClientConnection(client);
                        // SERVER : HI
                        conn.Send(
                            Encoding.UTF8.GetBytes(
                                QueuingMessages.SERVER_HI
                                ));

                        // CLIENT : public_key
                        var clientPublicKey = Encoding.UTF8.GetString(
                                                conn.Receive(maxPackageSize: 10240, timeOutSecond: this.TimeOutAuth)
                                                );
                        var rsaClient = new RSAKey(clientPublicKey);

                        // SERVER : public_key
                        conn.Send(
                            rsaClient.Encrypt(
                                Encoding.UTF8.GetBytes(
                                    rsaServer.PublicKey
                                    )));

                        // CLIENT : HI <user_name> <password>
                        var clientHi = Encoding.UTF8.GetString(
                                            rsaServer.Decrypt(conn.Receive(timeOutSecond: this.TimeOutAuth))
                                            );
                        var splitClientHi = clientHi.Split('<');
                        var clientHiUserName = splitClientHi[1].Split('>')[0];
                        var clientHiPassword = splitClientHi[2].Split('>')[0];
                        if (this.UserName != clientHiUserName || this.Password != clientHiPassword)
                        {
                            // - IF AUTH FAIL
                            //      SERVER : NOT_AUTHORIZED
                            conn.Send(
                                rsaClient.Encrypt(
                                    Encoding.UTF8.GetBytes(
                                        QueuingMessages.SERVER_NOT_AUTHORIZED
                                        )));
                            conn.Dispose();
                        }
                        else
                        {
                            // - IF AUTH PASS
                            //      SERVER : OK
                            conn.Send(
                                rsaClient.Encrypt(
                                    Encoding.UTF8.GetBytes(
                                        QueuingMessages.SERVER_OK
                                        )));

                            while (true)
                            {
                                var clientProcess = Encoding.UTF8.GetString(
                                                    rsaServer.Decrypt(conn.Receive())
                                                    );

                                #region ENQUEUE
                                if (clientProcess.StartsWith(QueuingMessages.CLIENT_ENQUEUE))
                                {
                                    // - IF PROCESS TYPE IS "ENQUEUE"
                                    //      CLIENT : ENQUEUE
                                    //      CLIENT : data
                                    var data = Encoding.UTF8.GetString(
                                                        rsaServer.Decrypt(conn.Receive())
                                                        );

                                    try
                                    {
                                        if (!Directory.Exists(QueuesDirectory))
                                            Directory.CreateDirectory(QueuesDirectory);

                                        this._fileCounter++;
                                        var fileName = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + this._fileCounter.ToString("00000") + ".txt";
                                        var filePath = Path.Combine(QueuesDirectory, fileName);
                                        File.WriteAllText(filePath, data, Encoding.UTF8);
                                    }
                                    catch (Exception ex)
                                    {
                                        conn.Send(
                                            rsaClient.Encrypt(
                                                Encoding.UTF8.GetBytes(
                                                    $"{QueuingMessages.SERVER_ERROR} <{nameof(QueuingMessages.CLIENT_ENQUEUE)}.Executing> \"{ex.ToString().Replace("\"", "\\\"")}\""
                                                    )));
                                        continue;
                                    }

                                    //      SERVER : END
                                    conn.Send(
                                        rsaClient.Encrypt(
                                            Encoding.UTF8.GetBytes(
                                                QueuingMessages.SERVER_END
                                                )));
                                }
                                #endregion
                                #region DEQUEUE_COMPLETED
                                else if (clientProcess.StartsWith(QueuingMessages.CLIENT_DEQUEUE_COMPLETED))
                                {
                                    // - IF PROCESS TYPE IS "DEQUEUE COMPLETED"
                                    //      CLIENT : DEQUEUE_COMPLETED <queue_name>
                                    var queueName = clientProcess.Split('<')[1].Split('>')[0];

                                    try
                                    {
                                        var filePath = Path.Combine(QueuesDirectory, queueName);
                                        if (File.Exists(filePath))
                                            File.Delete(filePath);
                                    }
                                    catch (Exception ex)
                                    {
                                        conn.Send(
                                            rsaClient.Encrypt(
                                                Encoding.UTF8.GetBytes(
                                                    $"{QueuingMessages.SERVER_ERROR} <{nameof(QueuingMessages.CLIENT_DEQUEUE)}.Executing> \"{ex.ToString().Replace("\"", "\\\"")}\""
                                                    )));
                                        continue;
                                    }

                                    //      SERVER : END
                                    conn.Send(
                                        rsaClient.Encrypt(
                                            Encoding.UTF8.GetBytes(
                                                QueuingMessages.SERVER_END
                                                )));
                                }
                                #endregion
                                #region DEQUEUE
                                else if (clientProcess.StartsWith(QueuingMessages.CLIENT_DEQUEUE))
                                {
                                    // - IF PROCESS TYPE IS "DEQUEUE"
                                    //      CLIENT : DEQUEUE

                                    var queueFilePath = "";
                                    var queueFileContext = "";
                                    try
                                    {
                                        if (!Directory.Exists(QueuesDirectory))
                                            Directory.CreateDirectory(QueuesDirectory);

                                        while (true)
                                        {
                                            var files = Directory.GetFiles(QueuesDirectory);
                                            if (files.Length > 0)
                                            {
                                                queueFilePath = files.OrderBy(o => Path.GetFileNameWithoutExtension(o)).Take(1).FirstOrDefault();
                                                queueFileContext = File.ReadAllText(queueFilePath, Encoding.UTF8);
                                                break;
                                            }
                                            Thread.Sleep(1);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        conn.Send(
                                            rsaClient.Encrypt(
                                                Encoding.UTF8.GetBytes(
                                                    $"{QueuingMessages.SERVER_ERROR} <{nameof(QueuingMessages.CLIENT_DEQUEUE)}.Executing> \"{ex.ToString().Replace("\"", "\\\"")}\""
                                                    )));
                                        continue;
                                    }

                                    //      SERVER : queue_name
                                    conn.Send(
                                        rsaClient.Encrypt(
                                            Encoding.UTF8.GetBytes(
                                                Path.GetFileName(queueFilePath)
                                                )));

                                    //      SERVER : data
                                    conn.Send(
                                        rsaClient.Encrypt(
                                            Encoding.UTF8.GetBytes(
                                                queueFileContext
                                                )));

                                    //      SERVER : END
                                    conn.Send(
                                        rsaClient.Encrypt(
                                            Encoding.UTF8.GetBytes(
                                                QueuingMessages.SERVER_END
                                                )));
                                }
                                #endregion
                                #region CLOSE
                                else if (clientProcess.StartsWith(QueuingMessages.CLIENT_CLOSE))
                                {
                                    // - IF PROCESS TYPE IS "CLOSE"
                                    //      CLIENT : CLOSE
                                    //      SERVER : END
                                    conn.Dispose();
                                    break;
                                }
                                #endregion
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            client.Dispose();
                        }
                        catch
                        { }

                        Console.WriteLine("A connetion get an error = " + ex.ToString());
                    }
                });
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            this._actionQueueConnections.Dispose();
            this._listener.Stop();
        }
        #endregion
    }
}
