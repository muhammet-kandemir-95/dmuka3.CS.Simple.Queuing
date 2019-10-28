# dmuka3.CS.Simple.Queuing

 This library provides you to consume queue by order. It is working on disk. This library is like **RabbitMQ**. You can see the queue on file explorer.
 
 ## Nuget
 **Link** : https://www.nuget.org/packages/dmuka3.CS.Simple.Queuing
 ```nuget
 Install-Package dmuka3.CS.Simple.Queuing
 ```
 
 ## Example 1

  We should create a server to connect and to enqueue. After that, we will connect to this server to dequeue.
  
  ```csharp
// Creating the server.
QueuingServer server = new QueuingServer("muhammed", "123123", 2048, 4, 9090, timeOutAuth: 1);
// Starting it as async.
new Thread(() =>
{
    server.Start();
}).Start();

// Wait the server.
Thread.Sleep(1000);

// Client to enqueue.
QueuingClient client1 = new QueuingClient("127.0.0.1", 9090);
// Connect to the server.
client1.Start("muhammed", "123123", 2048);

// Client to dequeue.
QueuingClient client2 = new QueuingClient("127.0.0.1", 9090);
// Connect to the server.
client2.Start("muhammed", "123123", 2048);

// Queue's data.
string sendedBody = Guid.NewGuid().ToString();
// This is only for checking.
string gotBody = "";

new Thread(() =>
{
    // Dequeue a data from server.
    var queue = client2.Dequeue();
    // What was the body?
    gotBody = queue.body;
    // Dequeue completed.
    client2.DequeueCompleted(queue.queueName);
}).Start();

new Thread(() =>
{
    // Enqueue a data.
    client1.Enqueue(sendedBody);
}).Start();

Thread.Sleep(1000);

client1.Dispose();
client2.Dispose();
server.Dispose();

Assert.AreEqual(sendedBody, gotBody);
  ```
  
  You may wonder what is the "2048". It is key size of RSA. We always use RSA during communication on TCP. This is security to save your secret datas.
  
