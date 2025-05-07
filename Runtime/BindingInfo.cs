using System;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Internal class holding the details of a type binding registration.
    /// </summary>
    public class BindingInfo
    {
        public Type RegisteredType { get; }
        public Lifetime Lifetime { get; set; } = Lifetime.Transient; // Default to Transient

        // Source of the instance
        public Type ImplementationType { get; set; }
        public object Instance { get; set; }
        public Func<IContainer, object> Factory { get; set; }

        // Lock for thread-safe singleton instantiation
        public readonly object SingletonLock = new object();

        public BindingInfo(Type registeredType)
        {
            RegisteredType = registeredType ?? throw new ArgumentNullException(nameof(registeredType));
        }
    }
}