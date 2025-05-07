using System;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Defines the core operations for a Dependency Injection container.
    /// This is the primary interface you'll interact with when configuring dependencies
    /// and resolving services in your application.
    /// </summary>
    /// <remarks>
    /// The container is responsible for:
    /// <list type="bullet">
    /// <item><description>Registering (binding) service types to implementations</description></item>
    /// <item><description>Resolving instances when requested</description></item>
    /// <item><description>Managing object lifetimes (singleton vs transient)</description></item>
    /// <item><description>Creating hierarchical container structures</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Basic usage:
    /// <code>
    /// // Register a service
    /// container.Bind&lt;IPlayerService&gt;().To&lt;PlayerService&gt;().AsSingleton();
    ///
    /// // Resolve a service
    /// var playerService = container.Resolve&lt;IPlayerService&gt;();
    /// </code>
    /// </example>
    public interface IContainer : IDisposable
    {
        /// <summary>
        /// Starts the registration process for a given service type T.
        /// This is the entry point for configuring how a type should be resolved.
        /// </summary>
        /// <typeparam name="TService">The type of service to register (often an interface or abstract class).</typeparam>
        /// <returns>A syntax helper to continue the registration.</returns>
        /// <example>
        /// <code>
        /// // Register IPlayerService to be implemented by PlayerService as a singleton
        /// container.Bind&lt;IPlayerService&gt;().To&lt;PlayerService&gt;().AsSingleton();
        ///
        /// // Register IEnemyFactory to be created new each time
        /// container.Bind&lt;IEnemyFactory&gt;().To&lt;StandardEnemyFactory&gt;().AsTransient();
        ///
        /// // Register an existing instance
        /// container.Bind&lt;IUIManager&gt;().FromInstance(existingManager);
        /// </code>
        /// </example>
        IBindingSyntax Bind<TService>();

        /// <summary>
        /// Starts the registration process for a given service type.
        /// This overload accepts a Type parameter for scenarios where the type isn't known at compile time.
        /// </summary>
        /// <param name="serviceType">The type of service to register.</param>
        /// <returns>A syntax helper to continue the registration.</returns>
        /// <example>
        /// <code>
        /// // Register a type dynamically
        /// Type serviceType = GetServiceType(); // Some method that returns a Type
        /// container.Bind(serviceType).To(typeof(ConcreteImplementation)).AsSingleton();
        /// </code>
        /// </example>
        IBindingSyntax Bind(Type serviceType);

        /// <summary>
        /// Resolves (retrieves or creates) an instance of the requested service type T.
        /// This will return an existing instance for singleton bindings or create a new instance
        /// for transient bindings.
        /// </summary>
        /// <typeparam name="TService">The type of service to resolve.</typeparam>
        /// <returns>An instance of the requested service.</returns>
        /// <exception cref="ResolutionException">Thrown if the type cannot be resolved or if there's a circular dependency.</exception>
        /// <remarks>
        /// The resolution process follows these steps:
        /// <list type="number">
        /// <item><description>Check if the type is registered in this container</description></item>
        /// <item><description>If not found, check parent containers</description></item>
        /// <item><description>If still not found, attempt to create the type directly if it's a concrete type</description></item>
        /// <item><description>If none of the above succeed, throw a ResolutionException</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get an instance of IPlayerService
        /// var playerService = container.Resolve&lt;IPlayerService&gt;();
        ///
        /// // Use the resolved service
        /// playerService.InitializePlayer();
        /// </code>
        /// </example>
        TService Resolve<TService>();

        /// <summary>
        /// Resolves (retrieves or creates) an instance of the requested service type.
        /// This overload accepts a Type parameter for scenarios where the type isn't known at compile time.
        /// </summary>
        /// <param name="serviceType">The type of service to resolve.</param>
        /// <returns>An instance of the requested service.</returns>
        /// <exception cref="ResolutionException">Thrown if the type cannot be resolved or if there's a circular dependency.</exception>
        /// <example>
        /// <code>
        /// // Resolve a type dynamically
        /// Type serviceType = GetServiceType(); // Some method that returns a Type
        /// var service = container.Resolve(serviceType);
        /// </code>
        /// </example>
        object Resolve(Type serviceType);

        /// <summary>
        /// Checks if the specified service type can be resolved by this container or its parents.
        /// This is useful for checking if a service is available before attempting to resolve it.
        /// </summary>
        /// <typeparam name="TService">The type of service to check.</typeparam>
        /// <returns>True if the type can be resolved, false otherwise.</returns>
        /// <remarks>
        /// This method performs a lightweight check and doesn't actually create any instances.
        /// For concrete types that aren't explicitly registered, it returns true if the type could
        /// potentially be instantiated, but doesn't guarantee that constructor parameters can be resolved.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Check if a service is available
        /// if (container.CanResolve&lt;IAnalyticsService&gt;())
        /// {
        ///     var analytics = container.Resolve&lt;IAnalyticsService&gt;();
        ///     analytics.LogEvent("GameStarted");
        /// }
        /// </code>
        /// </example>
        bool CanResolve<TService>();

        /// <summary>
        /// Checks if the specified service type can be resolved by this container or its parents.
        /// This overload accepts a Type parameter for scenarios where the type isn't known at compile time.
        /// </summary>
        /// <param name="serviceType">The type of service to check.</param>
        /// <returns>True if the type can be resolved, false otherwise.</returns>
        /// <example>
        /// <code>
        /// // Check if a type can be resolved dynamically
        /// Type serviceType = GetServiceType(); // Some method that returns a Type
        /// if (container.CanResolve(serviceType))
        /// {
        ///     var service = container.Resolve(serviceType);
        /// }
        /// </code>
        /// </example>
        bool CanResolve(Type serviceType);

        /// <summary>
        /// Creates a new container instance that inherits bindings from this container.
        /// Child containers can override parent bindings and have their own lifetime.
        /// </summary>
        /// <returns>A new child container.</returns>
        /// <remarks>
        /// Child containers:
        /// <list type="bullet">
        /// <item><description>Inherit all bindings from their parent</description></item>
        /// <item><description>Can override parent bindings with their own</description></item>
        /// <item><description>Have their own lifetime (disposing a child doesn't affect the parent)</description></item>
        /// <item><description>Are used to implement hierarchical DI (global → scene → object)</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Create a child container for a specific scope
        /// var childContainer = parentContainer.CreateChildContainer();
        ///
        /// // Override a binding in the child container
        /// childContainer.Bind&lt;ILogService&gt;().To&lt;DetailedLogService&gt;().AsSingleton();
        ///
        /// // Child-specific binding
        /// childContainer.Bind&lt;ILevelData&gt;().FromInstance(levelData);
        /// </code>
        /// </example>
        IContainer CreateChildContainer();

        /// <summary>
        /// Instantiates a concrete type, resolving its constructor dependencies using the container.
        /// This is useful for creating instances of types that aren't registered in the container
        /// but have dependencies that are.
        /// </summary>
        /// <param name="concreteType">The concrete type to instantiate.</param>
        /// <returns>A new instance of the concrete type with dependencies injected via constructor.</returns>
        /// <exception cref="ResolutionException">Thrown if instantiation fails (e.g., cannot resolve constructor parameters).</exception>
        /// <exception cref="ArgumentException">Thrown if the type is abstract, an interface, or otherwise not instantiable.</exception>
        /// <remarks>
        /// This method:
        /// <list type="bullet">
        /// <item><description>Finds the most suitable constructor (preferring ones with the most parameters that can be resolved)</description></item>
        /// <item><description>Resolves all constructor parameters from the container</description></item>
        /// <item><description>Creates a new instance using the constructor and resolved parameters</description></item>
        /// <item><description>Does NOT perform field or property injection (use InjectionHelper.InjectInto for that)</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Create a custom service not registered in the container
        /// var customService = container.Instantiate(typeof(CustomService));
        /// </code>
        /// </example>
        object Instantiate(Type concreteType);

        /// <summary>
        /// Instantiates a concrete type T, resolving its constructor dependencies using the container.
        /// This is a generic version of the Instantiate method for stronger typing.
        /// </summary>
        /// <typeparam name="TConcrete">The concrete type to instantiate.</typeparam>
        /// <returns>A new instance of TConcrete with dependencies injected via constructor.</returns>
        /// <exception cref="ResolutionException">Thrown if instantiation fails (e.g., cannot resolve constructor parameters).</exception>
        /// <example>
        /// <code>
        /// // Create a custom service with strong typing
        /// var customService = container.Instantiate&lt;CustomService&gt;();
        /// </code>
        /// </example>
        TConcrete Instantiate<TConcrete>() where TConcrete : class;
    }
}