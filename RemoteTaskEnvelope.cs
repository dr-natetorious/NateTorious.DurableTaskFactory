using Newtonsoft.Json.Linq;
using System;

namespace NateTorious.Durability
{
    /// <summary>
    /// Represents a serializable envelope.
    /// </summary>
    [Serializable]
    public class RemoteTaskEnvelope
    {
        /// <summary>
        /// Gets or sets the name of the target type.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets the name of the callback method.
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// Gets or sets the parameter to pass.
        /// </summary>
        public JObject Value { get; set; }

        /// <summary>
        /// Checks if the current instance is complete.
        /// </summary>
        /// <returns><value>true</value> for yes.</returns>
        public bool IsValid()
        {
            return string.IsNullOrWhiteSpace(this.TypeName) == false
                && string.IsNullOrWhiteSpace(this.MethodName) == false
                && this.Value != null;
        }
    }
}
