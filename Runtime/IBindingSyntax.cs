using System;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Fluent API interface for specifying the implementation details of a binding.
    /// </summary>
    public interface IBindingSyntax
    {
        /// <summary>
        /// Binds the service to a specific implementation type.
        /// </summary>
        /// <typeparam name="TImplementation">The concrete implementation type.</typeparam>
        /// <returns>A syntax helper to specify the lifetime.</returns>
        ILifetimeSyntax To<TImplementation>() where TImplementation : class;

        /// <summary>
        /// Binds the service to a specific implementation type.
        /// </summary>
        /// <param name="implementationType">The concrete implementation type.</param>
        /// <returns>A syntax helper to specify the lifetime.</returns>
        ILifetimeSyntax To(Type implementationType);

        /// <summary>
        /// Binds the service to a specific pre-existing instance. Implies Singleton lifetime.
        /// </summary>
        /// <param name="instance">The instance to bind.</param>
        /// <returns>A syntax helper (lifetime is fixed as Singleton).</returns>
        ILifetimeSyntax FromInstance(object instance);

        /// <summary>
        /// Binds the service to a factory method that will be called to create the instance.
        /// </summary>
        /// <param name="factoryMethod">The factory method. It receives the container instance.</param>
        /// <returns>A syntax helper to specify the lifetime.</returns>
        ILifetimeSyntax FromMethod(Func<IContainer, object> factoryMethod);
    }
}