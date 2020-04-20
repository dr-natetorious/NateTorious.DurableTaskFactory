using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NateTorious.Durability
{
    /// <summary>
    /// Represents a factory for executing an async task with durability and fan-out.
    /// </summary>
    /// <example>
    /// await factory.CreateTaskAsync(ExampleService.StaticTarget, new Person
    /// {
    ///     Name = "fred",
    ///     Age = 21
    /// });
    /// 
    /// await factory.CreateTaskAsync(new ExampleService().InstanceTarget, new Person
    /// {
    ///     Name = "barney",
    ///     Age = 22
    /// });
    /// </example>
    /// <remarks>
    /// This class is thread-safe.
    /// </remarks>
    public sealed class DurableTaskFactory
    {
        private readonly IAmazonSQS queueClient;
        private readonly string queueUri;

        /// <summary>
        /// Create a new instance of the factory class.
        /// </summary>
        /// <param name="queueClient">The queue client for maintaining state.</param>
        /// <param name="queueUri">The address of the queue.</param>
        /// <exception cref="ArgumentNullException"/>
        public DurableTaskFactory(IAmazonSQS queueClient, string queueUri)
        {
            this.queueClient = queueClient ?? throw new ArgumentNullException(nameof(queueClient));
            this.queueUri = queueUri ?? throw new ArgumentNullException(nameof(queueUri));
        }

        /// <summary>
        /// Invoke the given <paramref name="callbackTarget"/> with specified <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="T">A serializable parameter into the <paramref name="callbackTarget"/>.</typeparam>
        /// <param name="callbackTarget">A static or instance method pointer.</param>
        /// <param name="value">The parameter value to <paramref name="callbackTarget"/>.</param>
        /// <returns>A <see cref="Task"/> to monitor for completion.</returns>
        /// <exception cref="NotSupportedException">
        /// The target signature does not meet the requirements:
        /// <list type="bullet">
        /// <item>The declaring type cannot be generic.</item>
        /// <item>The method name cannot use overloads</item>
        /// <item>The parameter count must be exactly one.</item>
        /// <item>The target cannot be anonymous method.</item>
        /// </list>
        /// </exception>
        public Task CreateTaskAsync<T>(Func<T, Task> callbackTarget, T value)
            where T : class, new()
        {
            return this.CreateTaskAsync(callbackTarget.Method, value);
        }

        /// <summary>
        /// Invoke the given <paramref name="callbackTarget"/> with specified <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="T">A serializable parameter into the <paramref name="callbackTarget"/>.</typeparam>
        /// <param name="callbackTarget">A static or instance method pointer.</param>
        /// <param name="value">The parameter value to <paramref name="callbackTarget"/>.</param>
        /// <returns>A <see cref="Task"/> to monitor for completion.</returns>
        /// <exception cref="NotSupportedException">
        /// The target signature does not meet the requirements:
        /// <list type="bullet">
        /// <item>The declaring type cannot be generic.</item>
        /// <item>The method name cannot use overloads</item>
        /// <item>The parameter count must be exactly one.</item>
        /// <item>The target cannot be anonymous method.</item>
        /// </list>
        /// </exception>
        public Task CreateTaskAsync<T>(Action<T> callbackTarget, T value)
            where T : class, new()
        {
            return this.CreateTaskAsync(callbackTarget.Method, value);
        }

        /// <summary>
        /// Invoke the given <paramref name="callbackTarget"/> with specified <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="T">A serializable parameter into the <paramref name="callbackTarget"/>.</typeparam>
        /// <param name="callbackTarget">A static or instance method pointer.</param>
        /// <param name="value">The parameter value to <paramref name="callbackTarget"/>.</param>
        /// <returns>A <see cref="Task"/> to monitor for completion.</returns>
        /// <exception cref="NotSupportedException">
        /// The target signature does not meet the requirements:
        /// <list type="bullet">
        /// <item>The declaring type cannot be generic.</item>
        /// <item>The method name cannot use overloads</item>
        /// <item>The parameter count must be exactly one.</item>
        /// <item>The target cannot be anonymous method.</item>
        /// </list>
        /// </exception>
        public async Task CreateTaskAsync<T>(MethodInfo callbackTarget, T value)
            where T : class, new()
        {
            var declaringType = callbackTarget.DeclaringType;

            if (declaringType.IsGenericType)
            {
                throw new NotImplementedException();
            }

            if (declaringType.GetMethods().Where(m=>m.Name == callbackTarget.Name).Count() != 1)
            {
                throw new NotSupportedException();
            }

            if (callbackTarget.Name.Contains("<>") || callbackTarget.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
            {
                throw new NotSupportedException();
            }

            if (callbackTarget.GetParameters().Length != 1)
            {
                throw new NotSupportedException();
            }

            await this.queueClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = this.queueUri,
                MessageBody = JsonConvert.SerializeObject(new RemoteTaskEnvelope
                {
                    TypeName = declaringType.FullName,
                    MethodName = callbackTarget.Name,
                    Value = JObject.FromObject(value)
                }),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    { "DeclaringType", new MessageAttributeValue{ StringValue = declaringType.FullName } },
                    { "MethodName", new MessageAttributeValue { StringValue = callbackTarget.Name } }
                }
            });
        }
    }
}
