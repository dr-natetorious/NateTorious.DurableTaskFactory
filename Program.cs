using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static NateTorious.Durability.ExampleService;

namespace NateTorious.Durability
{
    class Program
    {
        static void Main(string[] args) => MainAsync().Wait();

        static async Task MainAsync()
        {
            // Setup the DurableTaskFactory...
            var client = CreateLocalQueueClient();
            var queueUri = "https://tacos/rule";
            var factory = new DurableTaskFactory(client, queueUri);
            
            // Configure the Routing infrastructure....
            var router = new RoutingRuntime();
            router.RegisterAssembly(Assembly.GetExecutingAssembly());
            router.RegisterAssembly(typeof(ExampleService).Assembly);

            // Request a durable task is created...
            await factory.CreateTaskAsync(ExampleService.StaticTarget, new Person
            {
                Name = "fred",
                Age = 21
            });

            await factory.CreateTaskAsync(new ExampleService().InstanceTarget, new Person
            {
                Name = "barney",
                Age = 22
            });

            // Fetch the message from the queue...
            ReceiveMessageResponse response;
            do
            {
                response = await client.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUri,
                    MaxNumberOfMessages = 1
                });

                // Route the message...
                foreach (var message in response.Messages)
                {
                    try
                    {
                        var envelope = JsonConvert.DeserializeObject<RemoteTaskEnvelope>(message.Body);
                        router.Dispatch(envelope);
                    }
                    catch (Exception error)
                    {
                        Console.WriteLine($"[ERROR]: {error.GetBaseException()}");
                        throw;
                    }
                }
            } while (response.Messages.Count > 0);
        }

        private static IAmazonSQS CreateLocalQueueClient()
        {
            var queue = new Queue<SendMessageRequest>();
            var mock = new Mock<IAmazonSQS>();
            mock
                .Setup(m => m.SendMessageAsync(
                    It.IsAny<SendMessageRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<SendMessageRequest,CancellationToken>((s, c) =>
                {
                    Console.WriteLine($"[{nameof(IAmazonSQS.SendMessageAsync)}] Raised.");
                    lock(queue)
                    {
                        queue.Enqueue(s);
                    }
                })
                .ReturnsAsync(new SendMessageResponse());

            mock
                .Setup(m => m.ReceiveMessageAsync(
                    It.IsAny<ReceiveMessageRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    // Check if theres any messages...
                    SendMessageRequest nextRequest;
                    lock(queue)
                    {
                        queue.TryDequeue(out nextRequest);
                    }

                    // Convert them into expected format...
                    var messages = new List<Message>();
                    if (nextRequest != null)
                    {
                        messages.Add(new Message
                        {
                            Body = nextRequest.MessageBody,
                        });
                    }

                    // Return to the caller...
                    Console.WriteLine($"[{nameof(IAmazonSQS.ReceiveMessageAsync)}] Raised (messages={messages.Count}).");
                    return new ReceiveMessageResponse
                    {
                        Messages = messages
                    };
                });

            return mock.Object;
        }
    }
}
