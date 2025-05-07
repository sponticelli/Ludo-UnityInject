using System;
using System.Collections.Generic;
using UnityEngine; // For Debug

namespace Ludo.UnityInject.Editor
{
    /// <summary>
    /// Represents the source of a binding registration.
    /// </summary>
    internal enum BindingSourceType
    {
        Type,
        Instance,
        Factory
    }

    /// <summary>
    /// Stores information about a binding registered during simulation.
    /// </summary>
    internal class RecordedBinding
    {
        public Type RegisteredType { get; set; }
        public BindingSourceType SourceType { get; set; }
        public Type ImplementationType { get; set; } // Relevant for SourceType.Type
        public bool IsInstanceBound { get; set; } // Relevant for SourceType.Instance
        public bool IsFactoryBound { get; set; } // Relevant for SourceType.Factory
        public Lifetime Lifetime { get; set; } = Lifetime.Transient; // Default
        public ScriptableObjectInstaller InstallerSource { get; set; } // Track which installer registered it
    }

    /// <summary>
    /// Dummy implementation of binding syntax for recording purposes.
    /// </summary>
    internal class RecordedBindingSyntax : IBindingSyntax, ILifetimeSyntax
    {
        private readonly BindingCollector _collector;
        private readonly RecordedBinding _currentBinding;

        internal RecordedBindingSyntax(BindingCollector collector, RecordedBinding binding)
        {
            _collector = collector;
            _currentBinding = binding;
        }

        // IBindingSyntax Implementation (Records details)
        public ILifetimeSyntax To<TImplementation>() where TImplementation : class => To(typeof(TImplementation));

        public ILifetimeSyntax To(Type implementationType)
        {
            _currentBinding.SourceType = BindingSourceType.Type;
            _currentBinding.ImplementationType = implementationType;
            return this; // Return self to chain lifetime
        }

        public ILifetimeSyntax FromInstance(object instance)
        {
            _currentBinding.SourceType = BindingSourceType.Instance;
            _currentBinding.IsInstanceBound = true;
            _currentBinding.Lifetime = Lifetime.Singleton; // FromInstance implies Singleton
            return this; // Return self, but AsSingleton/AsTransient will be restricted
        }

        public ILifetimeSyntax FromMethod(Func<IContainer, object> factoryMethod)
        {
            _currentBinding.SourceType = BindingSourceType.Factory;
            _currentBinding.IsFactoryBound = true;
            return this; // Return self to chain lifetime
        }

        // ILifetimeSyntax Implementation (Records lifetime)
        public void AsSingleton()
        {
            if (_currentBinding.SourceType == BindingSourceType.Instance)
            {
                // Already singleton, do nothing or warn
                return;
            }

            _currentBinding.Lifetime = Lifetime.Singleton;
        }

        public void AsTransient()
        {
            if (_currentBinding.SourceType == BindingSourceType.Instance)
            {
                // Cannot make instance binding transient
                Debug.LogWarning(
                    $"[BindingCollector] Attempted to set Transient lifetime for an Instance binding ({_currentBinding.RegisteredType.Name}). Ignored.");
                return;
            }

            _currentBinding.Lifetime = Lifetime.Transient;
        }
    }

    /// <summary>
    /// A dummy IContainer implementation used solely to collect binding
    /// registrations from ScriptableObjectInstallers in Edit Mode.
    /// </summary>
    internal class BindingCollector : IContainer // Implement relevant parts
    {
        public List<RecordedBinding> RecordedBindings { get; } = new List<RecordedBinding>();
        private ScriptableObjectInstaller _currentInstaller = null;

        public void SetCurrentInstaller(ScriptableObjectInstaller installer)
        {
            _currentInstaller = installer;
        }

        public void Clear()
        {
            RecordedBindings.Clear();
            _currentInstaller = null;
        }

        // --- IContainer Implementation (Only Bind is functional) ---

        public IBindingSyntax Bind<TService>() => Bind(typeof(TService));

        public IBindingSyntax Bind(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            // Record the binding attempt
            var recordedBinding = new RecordedBinding
            {
                RegisteredType = serviceType,
                InstallerSource = _currentInstaller // Associate with the current installer
            };
            RecordedBindings.Add(recordedBinding);

            // Return the dummy syntax helper
            return new RecordedBindingSyntax(this, recordedBinding);
        }

        // --- Other IContainer methods (Not implemented or throw) ---
        // These are not needed for simulation, only Bind matters.

        public TService Resolve<TService>() => throw new NotImplementedException("BindingCollector cannot resolve.");

        public object Resolve(Type serviceType) =>
            throw new NotImplementedException("BindingCollector cannot resolve.");

        public bool CanResolve<TService>() =>
            throw new NotImplementedException("BindingCollector cannot check resolution.");

        public bool CanResolve(Type serviceType) =>
            throw new NotImplementedException("BindingCollector cannot check resolution.");

        public IContainer CreateChildContainer() =>
            throw new NotImplementedException("BindingCollector cannot create children.");

        public object Instantiate(Type concreteType) =>
            throw new NotImplementedException("BindingCollector cannot instantiate.");

        public TConcrete Instantiate<TConcrete>() where TConcrete : class =>
            throw new NotImplementedException("BindingCollector cannot instantiate.");

        public void Dispose()
        {
            /* No-op for collector */
        }
    }
}