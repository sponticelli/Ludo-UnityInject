using System;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Internal implementation of the binding syntax helper.
    /// </summary>
    internal class BindingSyntax : IBindingSyntax
    {
        private readonly Container _container;
        private readonly BindingInfo _bindingInfo;
        private readonly Type _serviceType; // Keep track for validation

        internal BindingSyntax(Container container, BindingInfo bindingInfo, Type serviceType)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _bindingInfo = bindingInfo ?? throw new ArgumentNullException(nameof(bindingInfo));
            _serviceType = serviceType; // The type passed to Bind<T>()
        }

        public ILifetimeSyntax To<TImplementation>() where TImplementation : class
        {
            return To(typeof(TImplementation));
        }

        public ILifetimeSyntax To(Type implementationType)
        {
            if (implementationType == null) throw new ArgumentNullException(nameof(implementationType));

            // Validate assignability
            if (!_serviceType.IsAssignableFrom(implementationType))
            {
                throw new ArgumentException(
                    $"Implementation type '{implementationType.FullName}' is not assignable to service type '{_serviceType.FullName}'.");
            }

            if (implementationType.IsAbstract || implementationType.IsInterface)
            {
                throw new ArgumentException(
                    $"Implementation type '{implementationType.FullName}' cannot be an interface or abstract class.");
            }

            if (implementationType.IsGenericTypeDefinition)
            {
                throw new ArgumentException(
                    $"Implementation type '{implementationType.FullName}' cannot be an open generic type definition. Provide a closed generic type.");
            }

            _bindingInfo.ImplementationType = implementationType;
            _bindingInfo.Factory = null; // Clear other sources
            _bindingInfo.Instance = null;
            return new LifetimeSyntax(_bindingInfo);
        }

        public ILifetimeSyntax FromInstance(object instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            // Validate assignability
            if (!_serviceType.IsInstanceOfType(instance))
            {
                throw new ArgumentException(
                    $"Provided instance of type '{instance.GetType().FullName}' is not assignable to service type '{_serviceType.FullName}'.");
            }

            _bindingInfo.Instance = instance;
            _bindingInfo.Lifetime = Lifetime.Singleton; // FromInstance forces Singleton
            _bindingInfo.ImplementationType = null; // Clear other sources
            _bindingInfo.Factory = null;

            // Track for disposal immediately if IDisposable and managed by this container
            _container.TrackDisposable(_bindingInfo.Instance);

            // Return a lifetime syntax that enforces Singleton
            return new FixedLifetimeSyntax(_bindingInfo);
        }

        public ILifetimeSyntax FromMethod(Func<IContainer, object> factoryMethod)
        {
            if (factoryMethod == null) throw new ArgumentNullException(nameof(factoryMethod));

            _bindingInfo.Factory = factoryMethod;
            _bindingInfo.ImplementationType = null; // Clear other sources
            _bindingInfo.Instance = null;
            return new LifetimeSyntax(_bindingInfo);
        }
    }
}