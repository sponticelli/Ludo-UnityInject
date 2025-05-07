using System;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Marks fields, properties, and methods as injection points for dependency injection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        Inherited = false, AllowMultiple = false)]
    public sealed class InjectAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether this dependency is optional.
        /// If true, injection failure for this point will be skipped silently.
        /// If false (default), failure to resolve the dependency will result in an error log.
        /// For methods, if Optional is true and any parameter cannot be resolved, the method call is skipped silently.
        /// </summary>
        public bool Optional { get; set; } = false; // Default to false (mandatory)
        
    }
}