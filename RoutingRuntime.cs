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
    /// Represents a dispatch process for the <see cref="DurableTaskFactory"/>.
    /// </summary>
    public sealed class RoutingRuntime
    {
        private readonly Dictionary<string, Type> knownTypes = new Dictionary<string, Type>();
        private readonly static MethodInfo dispatchGeneric =
            typeof(RoutingRuntime).GetMethod(nameof(DispatchImplementation), (BindingFlags)int.MaxValue);

        /// <summary>
        /// Register an assembly as a valid target for dispatching.
        /// </summary>
        /// <remarks>
        /// Only public symbols will be registered.
        /// </remarks>
        /// <param name="assembly">An assembly to consider for dispatch operations.</param>
        public void RegisterAssembly(Assembly assembly)
        {
            _ = assembly ?? throw new ArgumentNullException(nameof(assembly));
            foreach (var type in assembly.GetExportedTypes())
            {
                // Ignore generic types, not implemented yet.
                if (type.IsGenericType)
                {
                    continue;
                }

                // Ignore compiler generated symbols
                if (type.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
                {
                    continue;
                }

                this.knownTypes[type.FullName.ToUpperInvariant()] = type;
            }
        }

        /// <summary>
        /// Route the envelope to the correct callback method.
        /// </summary>
        /// <param name="envelope">The parameters and target information.</param>
        /// <exception cref="ArgumentException">The <paramref name="envelope"/> is not valid.</exception>
        /// <exception cref="InvalidOperationException">
        /// Unable to find a valid target, did you call <see cref="RegisterAssembly(Assembly)"/>?
        /// </exception>
        /// <remarks>
        /// Intermediate reflection failures and not bubbled up, and the third party error is directly returned.
        /// </remarks>
        public void Dispatch(RemoteTaskEnvelope envelope)
        {
            // Verify the arguments are sane and has a shot at working...
            if(envelope?.IsValid() != true)
            {
                throw new ArgumentException(nameof(envelope));
            }

            if (this.knownTypes.TryGetValue(envelope.TypeName.ToUpperInvariant(), out var type) == false)
            {
                throw new InvalidOperationException($"Requested type - {envelope.TypeName} is not registered.");
            }

            // Extract the relevant method metadata....
            var method = type.GetMethod(envelope.MethodName) 
                ?? throw new InvalidOperationException(nameof(envelope.MethodName));
            var desiredParameterType = method.GetParameters().FirstOrDefault()?.ParameterType 
                ?? throw new InvalidOperationException("Incorrect signature");

            // Create the instance for this invocation:
            // To avoid the memory allocation the knownTypes could be changed to Lazy<object>
            // ... then contain this information as part of the callback.
            object instance = null;
            try
            {
                if (method.IsStatic == false)
                {
                    instance = Activator.CreateInstance(type);
                }
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }

            // Route the execution to the desired method...
            try
            {
                dispatchGeneric.MakeGenericMethod(desiredParameterType).Invoke(this, new object[] { instance, method, envelope.Value });
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        private void DispatchImplementation<TParameter>(object instance, MethodInfo method, JObject value)
        {
            var desiredValue = value.ToObject<TParameter>();
            object result;
            try
            {
                result = method.Invoke(instance, new object[] { desiredValue });
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }

            // Wait for the task to complete before returning...
            if (result is Task task)
            {
                task.Wait();
            }
        }
    }
}
