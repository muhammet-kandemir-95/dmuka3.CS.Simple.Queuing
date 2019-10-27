using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace dmuka3.CS.Simple.Queuing.NUnit
{
    public class QueuingTests
    {
        [Test]
        public void ClassicTest()
        {
            if (Directory.Exists(QueuingServer.QueuesDirectory))
                Directory.Delete(QueuingServer.QueuesDirectory, true);

            QueuingServer server = new QueuingServer("muhammed", "123123", 2048, 4, 9090, timeOutAuth: 1);
            new Thread(() =>
            {
                server.Start();
            }).Start();

            Thread.Sleep(1000);

            QueuingClient client1 = new QueuingClient("127.0.0.1", 9090);
            client1.Start("muhammed", "123123", 2048);

            QueuingClient client2 = new QueuingClient("127.0.0.1", 9090);
            client2.Start("muhammed", "123123", 2048);

            string sendedBody = Guid.NewGuid().ToString();
            string gotBody = "";

            new Thread(() =>
            {
                var queue = client2.Dequeue();
                gotBody = queue.body;
                client2.DequeueCompleted(queue.queueName);
            }).Start();

            new Thread(() =>
            {
                client1.Enqueue(sendedBody);
            }).Start();

            Thread.Sleep(1000);

            client1.Dispose();
            client2.Dispose();
            server.Dispose();

            Assert.AreEqual(sendedBody, gotBody);
        }

        [Test]
        public void MultipleQueueTest()
        {
            if (Directory.Exists(QueuingServer.QueuesDirectory))
                Directory.Delete(QueuingServer.QueuesDirectory, true);

            QueuingServer server = new QueuingServer("muhammed", "123123", 2048, 4, 9090, timeOutAuth: 1);
            new Thread(() =>
            {
                server.Start();
            }).Start();

            Thread.Sleep(1000);

            QueuingClient client1 = new QueuingClient("127.0.0.1", 9090);
            client1.Start("muhammed", "123123", 2048);

            QueuingClient client2 = new QueuingClient("127.0.0.1", 9090);
            client2.Start("muhammed", "123123", 2048);

            List<string> sendedMessages = new List<string>();
            List<bool> messagesCheck = new List<bool>();

            new Thread(() =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var queue = client2.Dequeue();
                    messagesCheck.Add(sendedMessages[0] == queue.body);
                    sendedMessages.RemoveAt(0);
                    client2.DequeueCompleted(queue.queueName);
                }
            }).Start();

            new Thread(() =>
            {
                sendedMessages.Add(Guid.NewGuid().ToString());
                client1.Enqueue(sendedMessages[sendedMessages.Count - 1]);

                sendedMessages.Add(Guid.NewGuid().ToString());
                client1.Enqueue(sendedMessages[sendedMessages.Count - 1]);

                sendedMessages.Add(Guid.NewGuid().ToString());
                client1.Enqueue(sendedMessages[sendedMessages.Count - 1]);
            }).Start();

            Thread.Sleep(1000);

            client1.Dispose();
            client2.Dispose();
            server.Dispose();

            Assert.AreEqual(messagesCheck.Where(o => o).Count(), 3);
        }

        [Test]
        public void WrongUserNameTest()
        {
            QueuingServer server = new QueuingServer("muhammed", "123123", 2048, 4, 9090, timeOutAuth: 1);
            new Thread(() =>
            {
                server.Start();
            }).Start();

            Thread.Sleep(1000);

            QueuingClient client = new QueuingClient("127.0.0.1", 9090);
            bool err = false;
            try
            {
                client.Start("muhammed2", "123123", 2048);
            }
            catch (Exception ex)
            {
                err = ex.Message.StartsWith("NOT_AUTHORIZED");
            }


            client.Dispose();
            server.Dispose();

            Assert.IsTrue(err);
        }

        [Test]
        public void WrongPasswordTest()
        {
            QueuingServer server = new QueuingServer("muhammed", "123123", 2048, 4, 9090, timeOutAuth: 1);
            new Thread(() =>
            {
                server.Start();
            }).Start();

            Thread.Sleep(1000);

            QueuingClient client = new QueuingClient("127.0.0.1", 9090);
            bool err = false;
            try
            {
                client.Start("muhammed", "123124", 2048);
            }
            catch (Exception ex)
            {
                err = ex.Message.StartsWith("NOT_AUTHORIZED");
            }


            client.Dispose();
            server.Dispose();

            Assert.IsTrue(err);
        }

    }
}