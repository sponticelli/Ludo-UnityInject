using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Ludo.UnityInject
{
    /// <summary>
    /// A Dependency Injection container implementation.
    /// </summary>
    public class Container : IContainer
    {
        private readonly IContainer _parent;
        private readonly Dictionary<Type, BindingInfo> _bindings = new Dictionary<Type, BindingInfo>();
        private readonly List<IDisposable> _disposableSingletons = new List<IDisposable>();
        private readonly HashSet<Type> _resolutionStack = new HashSet<Type>(); // For circular dependency detection
        private bool _disposed = false;

        private readonly Dictionary<Type, ConstructorInfo[]> _constructorCache =
            new Dictionary<Type, ConstructorInfo[]>();


        /// <summary>
        /// Creates a new root container.
        /// </summary>
        public Container() : this(null)
        {
        }

        /// <summary>
        /// Creates a new container, optionally linked to a parent container.
        /// </summary>
        /// <param name="parent">The parent container to inherit bindings from.</param>
        protected Container(IContainer parent) // Protected constructor for controlled child creation
        {
            _parent = parent;
            // Automatically register the container itself so it can be injected
            Bind<IContainer>().FromInstance(this);
        }

        /// <inheritdoc />
        public IBindingSyntax Bind<TService>()
        {
            return Bind(typeof(TService));
        }

        /// <inheritdoc />
        public IBindingSyntax Bind(Type serviceType)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Container));
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            var bindingInfo = new BindingInfo(serviceType);
            // Add or overwrite existing binding for this type in this specific container
            _bindings[serviceType] = bindingInfo;
            return new BindingSyntax(this, bindingInfo, serviceType);
        }

        /// <inheritdoc />
        public TService Resolve<TService>()
        {
            return (TService)Resolve(typeof(TService));
        }

        /// <inheritdoc />
        public object Resolve(Type serviceType)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Container));
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            // --- Circular Dependency Check ---
            if (!_resolutionStack.Add(serviceType))
            {
                throw new ResolutionException(
                    $"Circular dependency detected while resolving type '{serviceType.FullName}'. Resolution path: {string.Join(" -> ", _resolutionStack)} -> {serviceType.FullName}");
            }

            try
            {
                // Check local bindings
                if (_bindings.TryGetValue(serviceType, out var bindingInfo))
                {
                    return ResolveInternal(bindingInfo, this); // 'this' is the context container
                }
                
                // Check for Func<T>
                if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(Func<>))
                {
                    Type returnType = serviceType.GetGenericArguments()[0];
                    // Check if the underlying type T can be resolved (by this container or parents)
                    if (this.CanResolve(returnType)) // Use the public CanResolve which checks parents
                    {
                        // Dynamically create and return the Func<T> delegate
                        // The delegate captures the current container context ('this')
                        // Need to use reflection to create the delegate for the specific T
                        return BuildFuncFactory(returnType);
                    }
                    // If Func<T> was explicitly bound earlier, it would have been caught by _bindings lookup.
                    // If T cannot be resolved, fall through to throw ResolutionException later.
                }

                // Check parent container
                if (_parent != null && _parent.CanResolve(serviceType))
                {
                    // Delegate to parent. Parent will handle its own circular dependency check starting from this type.
                    return _parent.Resolve(serviceType);
                }

                // Implicit Concrete Type Resolution (Optional behavior)
                // Allow resolving concrete types that aren't explicitly registered?
                if (!serviceType.IsAbstract && !serviceType.IsInterface && !serviceType.IsGenericTypeDefinition)
                {
                    // Treat as an implicit transient binding: Bind<T>().To<T>().AsTransient()
                    // Ensure it's not explicitly registered locally or in parents already (checked above)
                    try
                    {
                        // Attempt direct instantiation via constructor injection
                        return InstantiateInternal(serviceType, this); // Pass context
                    }
                    catch (ResolutionException ex)
                    {
                        // If instantiation fails, it might be intentional that it wasn't registered.
                        // Rethrow a more specific error indicating it wasn't registered.
                        throw new ResolutionException(
                            $"Could not resolve concrete type '{serviceType.FullName}'. It was not explicitly registered and instantiation failed. See inner exception.",
                            ex);
                    }
                    catch (Exception ex)
                    {
                        // Catch other potential Activator/Reflection exceptions during implicit instantiation
                        throw new ResolutionException(
                            $"An unexpected error occurred trying to implicitly instantiate concrete type '{serviceType.FullName}'. See inner exception.",
                            ex);
                    }
                }

                // Not found
                throw new ResolutionException(
                    $"Could not resolve type '{serviceType.FullName}'. No registration found in this container or its parents, and it's not an instantiable concrete type.");
            }
            finally
            {
                // --- Remove from stack when exiting this level of resolution ---
                _resolutionStack.Remove(serviceType);
            }
        }
        
        internal IReadOnlyDictionary<Type, BindingInfo> GetInternalBindings()
        {
            // Return a read-only wrapper or copy to prevent modification
            return new Dictionary<Type, BindingInfo>(_bindings);
        }
        
        // Helper method to build Func<T> delegate using reflection
        private object BuildFuncFactory(Type returnType)
        {
            // We want to create a delegate equivalent to: () => (TReturn)this.Resolve(returnType)
            // Need to get the Resolve<T>() method specific to the returnType via reflection.

            // MethodInfo resolveMethodGeneric = typeof(Container).GetMethod(nameof(Resolve), Type.EmptyTypes); // Find T Resolve<T>()
            // MethodInfo resolveMethodSpecific = resolveMethodGeneric.MakeGenericMethod(returnType);

            // Simpler approach using non-generic Resolve and casting:
            Func<object> resolveLambda = () => this.Resolve(returnType); // Capture 'this' and 'returnType'

            // We need to convert Func<object> to Func<TReturn>
            // Delegate.CreateDelegate requires a MethodInfo target for static methods or instance+MethodInfo for instance methods.
            // Let's create the Func<> dynamically. Get the Func<> type first.
            Type funcType = typeof(Func<>).MakeGenericType(returnType);

            // Create the lambda dynamically. This is slightly complex.
            // Easier: Define a generic helper method and call *that* via reflection.

            MethodInfo createFuncHelper = typeof(Container)
                .GetMethod(nameof(CreateFuncHelper), BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(returnType);

            return createFuncHelper.Invoke(this, null);
            
        }

        // Generic helper method to create the Func<T> cleanly
        private Func<T> CreateFuncHelper<T>()
        {
            // This lambda captures 'this' (the container instance)
            return () => (T)this.Resolve(typeof(T));
        }

        /// <summary>
        /// Internal resolution logic once a binding is found.
        /// </summary>
        private object ResolveInternal(BindingInfo bindingInfo, IContainer contextContainer)
        {
            if (bindingInfo.Lifetime == Lifetime.Singleton)
            {
                // Use the lock specific to this binding for thread safety
                lock (bindingInfo.SingletonLock)
                {
                    if (bindingInfo.Instance == null) // Double-check lock
                    {
                        // Pass the container where resolution *started* to the factory/instantiator
                        bindingInfo.Instance = CreateInstanceInternal(bindingInfo, contextContainer);
                        TrackDisposable(bindingInfo.Instance);
                    }
                }

                return bindingInfo.Instance;
            }
            else // Transient
            {
                // Pass the container where resolution *started* to the factory/instantiator
                var instance = CreateInstanceInternal(bindingInfo, contextContainer);
                // Transient instances are generally not tracked for disposal by the container
                return instance;
            }
        }

        /// <summary>
        /// Creates an instance based on the BindingInfo.
        /// </summary>
        private object CreateInstanceInternal(BindingInfo bindingInfo, IContainer contextContainer)
        {
            try
            {
                if (bindingInfo.Factory != null)
                {
                    // Use the factory method, passing the context container
                    return bindingInfo.Factory(contextContainer);
                }
                else if (bindingInfo.ImplementationType != null)
                {
                    // Instantiate the specified implementation type via constructor injection
                    return InstantiateInternal(bindingInfo.ImplementationType, contextContainer);
                }
                else if
                    (bindingInfo.Instance !=
                     null) // Should only be hit for singletons after first creation, but check just in case
                {
                    return bindingInfo.Instance; // Should have been caught by singleton logic earlier
                }
                else if (!bindingInfo.RegisteredType.IsAbstract && !bindingInfo.RegisteredType.IsInterface)
                {
                    // Handle case where Bind<Concrete>() was called without To() (implicit self-binding)
                    return InstantiateInternal(bindingInfo.RegisteredType, contextContainer);
                }
                else
                {
                    throw new ResolutionException(
                        $"Binding for '{bindingInfo.RegisteredType.FullName}' is incomplete. No Instance, ImplementationType, or Factory was provided, and the registered type itself is not concrete.");
                }
            }
            catch (ResolutionException)
            {
                throw;
            } // Don't re-wrap ResolutionExceptions
            catch (Exception ex)
            {
                throw new ResolutionException(
                    $"Failed to create instance for registration '{bindingInfo.RegisteredType.FullName}'. See inner exception.",
                    ex);
            }
        }

        /// <inheritdoc />
        public object Instantiate(Type concreteType)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Container));
            if (concreteType == null) throw new ArgumentNullException(nameof(concreteType));
            if (concreteType.IsAbstract || concreteType.IsInterface)
                throw new ArgumentException(
                    $"Cannot instantiate abstract class or interface '{concreteType.FullName}'. Provide a concrete type.",
                    nameof(concreteType));

            // Pass 'this' as the context container for resolving constructor parameters
            return InstantiateInternal(concreteType, this);
        }

        /// <inheritdoc />
        public TConcrete Instantiate<TConcrete>() where TConcrete : class
        {
            return (TConcrete)Instantiate(typeof(TConcrete));
        }


        private ConstructorInfo[] GetCachedConstructors(Type concreteType)
        {
            if (_constructorCache.TryGetValue(concreteType, out var cachedConstructors))
            {
                return cachedConstructors;
            }

            // Find constructors ordered by descending number of parameters
            var constructors = concreteType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();

            // Cache the result
            _constructorCache[concreteType] = constructors;
            return constructors;
        }

        /// <summary>
        /// Internal core instantiation logic using constructor injection.
        /// </summary>
        private object
            InstantiateInternal(Type concreteType, IContainer contextContainer) // contextContainer resolves parameters
        {
            var constructors = GetCachedConstructors(concreteType);

            if (constructors.Length == 0)
            {
                // Allow instantiation of types with no public constructor ONLY if they are value types (structs)
                // or have a parameterless private/internal constructor (less common for DI)
                if (concreteType.IsValueType)
                {
                    try
                    {
                        return Activator.CreateInstance(concreteType);
                    }
                    catch
                    {
                    } // Structs have implicit parameterless constructors
                }

                // No suitable constructor found
                throw new ResolutionException(
                    $"Cannot instantiate type '{concreteType.FullName}'. No public constructors found.");
            }

            ResolutionException lastException = null;

            // Try constructors one by one, starting with the most specific
            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                var parameterInstances = new object[parameters.Length];
                bool allParametersResolved = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    try
                    {
                        // Resolve parameters using the context container (where Resolve/Instantiate was called)
                        parameterInstances[i] = contextContainer.Resolve(parameters[i].ParameterType);
                    }
                    catch (ResolutionException ex)
                    {
                        // Could not resolve a parameter for THIS constructor, try the next one
                        lastException = new ResolutionException(
                            $"Failed to resolve parameter '{parameters[i].Name}' (Type: {parameters[i].ParameterType.FullName}) for constructor of '{concreteType.FullName}'.",
                            ex);
                        allParametersResolved = false;
                        break; // Stop trying to resolve parameters for this constructor
                    }
                    catch (Exception ex) // Catch unexpected errors during parameter resolution
                    {
                        lastException = new ResolutionException(
                            $"Unexpected error resolving parameter '{parameters[i].Name}' for '{concreteType.FullName}'.",
                            ex);
                        allParametersResolved = false;
                        break;
                    }
                }

                if (allParametersResolved)
                {
                    // Successfully resolved all parameters for this constructor, invoke it
                    try
                    {
                        return constructor.Invoke(parameterInstances);
                    }
                    catch (Exception ex) // Catch errors during constructor invocation itself
                    {
                        throw new ResolutionException(
                            $"Constructor invocation failed for type '{concreteType.FullName}'. See inner exception.",
                            ex);
                    }
                }
                // Otherwise, loop continues to try the next constructor (if any)
            }

            // If loop completes without successfully invoking a constructor
            throw new ResolutionException(
                $"Could not instantiate type '{concreteType.FullName}'. No constructor had all parameters resolvable. See last encountered parameter resolution error (inner exception).",
                lastException);
        }

        /// <inheritdoc />
        public bool CanResolve<TService>()
        {
            return CanResolve(typeof(TService));
        }

        /// <inheritdoc />
        public bool CanResolve(Type serviceType)
        {
            if (_disposed) return false;
            if (serviceType == null) return false;

            // Check local explicit bindings
            if (_bindings.ContainsKey(serviceType)) return true;

            // Check parent explicit bindings
            if (_parent != null && _parent.CanResolve(serviceType)) return true;
            
            // Check for Func<T> (Func<TReturn>)
            if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(Func<>))
            {
                // Can resolve Func<T> if T itself can be resolved (and Func<T> isn't explicitly bound)
                if(!_bindings.ContainsKey(serviceType) && (_parent == null || !_parent.CanResolve(serviceType)))
                {
                    Type returnType = serviceType.GetGenericArguments()[0];
                    return this.CanResolve(returnType); // Check if underlying type is resolvable
                }
            }

            // Check if it's a concrete type that *could* be implicitly resolved (basic check)
            // Note: This doesn't guarantee resolution, as constructor params might be missing.
            if (!serviceType.IsAbstract && !serviceType.IsInterface && !serviceType.IsGenericTypeDefinition)
            {
                // We can potentially instantiate concrete types not explicitly registered.
                // A more robust CanResolve might try a dry run of constructor parameter resolution.
                // For now, simply returning true if concrete is a reasonable approximation.
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public IContainer CreateChildContainer()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Container));
            // Pass 'this' container as the parent to the new child
            return new Container(this);
        }

        /// <summary>
        /// Tracks a singleton instance for disposal if it implements IDisposable.
        /// </summary>
        internal void TrackDisposable(object instance)
        {
            // Only track singletons managed by *this* container instance
            // Check if the instance came from a binding in _bindings where lifetime is Singleton
            // (More accurate than just checking if the instance is IDisposable)

            if (instance is IDisposable disposable)
            {
                // Find the binding that produced this instance to confirm it's a singleton managed here
                bool isLocalSingleton = _bindings.Values.Any(b =>
                    b.Lifetime == Lifetime.Singleton && ReferenceEquals(b.Instance, instance));

                if (isLocalSingleton)
                {
                    lock (_disposableSingletons) // Lock list access
                    {
                        if (!_disposableSingletons.Contains(disposable))
                        {
                            _disposableSingletons.Add(disposable);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                // Dispose managed resources: IDisposable singletons created by *this* container
                lock (_disposableSingletons)
                {
                    // Dispose in reverse order of creation (often safer)
                    for (int i = _disposableSingletons.Count - 1; i >= 0; i--)
                    {
                        IDisposable disposableObject = _disposableSingletons[i]; // Added for logging
                        // Debug.Log($"[DI Container] Attempting to dispose singleton ({disposableObject?.GetType().FullName ?? "null"})"); // Added Log

                        try
                        {
                            _disposableSingletons[i]?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            // Log errors during disposal, e.g., using Unity's logger
                            Debug.LogError(
                                $"[DI Container] Error disposing singleton ({_disposableSingletons[i].GetType().FullName}): {ex}");
                        }
                    }

                    _disposableSingletons.Clear();
                }

                _bindings.Clear(); // Clear bindings
                _resolutionStack.Clear(); // Clear resolution stack if dispose happens mid-resolve (unlikely)
            }
            
        }

        // Finalizer as a safety net, especially if dealing with unmanaged resources directly.
        ~Container()
        {
            Dispose(false);
        }
    }
}