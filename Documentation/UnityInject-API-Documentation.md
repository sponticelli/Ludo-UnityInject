# UnityInject API Documentation

## Core Interfaces and Classes

### IContainer

The `IContainer` interface is the core of the UnityInject system, providing methods for registering, resolving, and managing dependencies.

```csharp
/// <summary>
/// Defines the core operations for a Dependency Injection container.
/// This is the primary interface you'll interact with when configuring dependencies.
/// </summary>
public interface IContainer : IDisposable
{
    /// <summary>
    /// Starts the registration process for a given service type T.
    /// </summary>
    /// <typeparam name="TService">The type of service to register (often an interface or abstract class).</typeparam>
    /// <returns>A binding syntax helper to continue the registration.</returns>
    /// <example>
    /// <code>
    /// // Register IPlayerService to be implemented by PlayerService as a singleton
    /// container.Bind<IPlayerService>().To<PlayerService>().AsSingleton();
    /// </code>
    /// </example>
    IBindingSyntax Bind<TService>();

    /// <summary>
    /// Resolves (retrieves or creates) an instance of the requested service type T.
    /// </summary>
    /// <typeparam name="TService">The type of service to resolve.</typeparam>
    /// <returns>An instance of the requested service.</returns>
    /// <exception cref="ResolutionException">Thrown if the type cannot be resolved.</exception>
    /// <example>
    /// <code>
    /// // Get an instance of IPlayerService
    /// var playerService = container.Resolve<IPlayerService>();
    /// </code>
    /// </example>
    TService Resolve<TService>();

    /// <summary>
    /// Creates a new container instance that inherits bindings from this container.
    /// Child containers can override parent bindings and have their own lifetime.
    /// </summary>
    /// <returns>A new child container.</returns>
    /// <example>
    /// <code>
    /// // Create a child container for a specific scope
    /// var childContainer = parentContainer.CreateChildContainer();
    /// 
    /// // Override a binding in the child container
    /// childContainer.Bind<ILogService>().To<DetailedLogService>().AsSingleton();
    /// </code>
    /// </example>
    IContainer CreateChildContainer();
}
```

### SceneContext

The `SceneContext` component manages dependency injection for an entire Unity scene.

```csharp
/// <summary>
/// MonoBehaviour that manages the dependency injection for a scene.
/// Add this component to a GameObject in each scene that requires dependency injection.
/// It creates a child container from the root container, runs scene-specific installers,
/// and injects dependencies into all MonoBehaviours in the scene.
/// </summary>
/// <remarks>
/// Only one SceneContext should exist per scene. It should be placed on an empty GameObject,
/// typically named "_SceneContext" for clarity.
/// </remarks>
/// <example>
/// <code>
/// // In your scene, create an empty GameObject named "_SceneContext"
/// // Add the SceneContext component to it
/// // Assign your scene-specific installer assets to the "Scene Installers" field
/// </code>
/// </example>
public class SceneContext : MonoBehaviour
{
    // Implementation details...
}
```

### GameObjectContext

The `GameObjectContext` component creates an isolated DI scope for a specific GameObject hierarchy.

```csharp
/// <summary>
/// MonoBehaviour that manages dependency injection for a GameObject and its children.
/// This component should be added to the root of a prefab that requires its own dependency injection scope.
/// It creates a child container from the parent container, runs object-specific installers,
/// and injects dependencies into all MonoBehaviours in its hierarchy.
/// </summary>
/// <remarks>
/// GameObjectContext is particularly useful for prefabs that need their own isolated scope,
/// such as enemy prefabs with unique configurations or UI widgets with their own state.
/// 
/// For proper operation, prefabs with GameObjectContext should be instantiated using
/// container.InstantiatePrefab() to ensure the parent container is correctly injected.
/// </remarks>
/// <example>
/// <code>
/// // On your prefab's root GameObject, add the GameObjectContext component
/// // Optionally assign prefab-specific installer assets to the "Object Installers" field
/// // Instantiate the prefab using container.InstantiatePrefab() to ensure proper injection
/// </code>
/// </example>
public class GameObjectContext : MonoBehaviour
{
    // Implementation details...
}
```

### ScriptableObjectInstaller

The `ScriptableObjectInstaller` class is used to configure container bindings via ScriptableObjects.

```csharp
/// <summary>
/// Base class for installers that configure container bindings via ScriptableObjects.
/// Create concrete installer classes that inherit from this to configure specific containers.
/// </summary>
/// <remarks>
/// Installers are the recommended way to configure bindings in UnityInject.
/// They provide a clean, modular approach to dependency configuration and can be
/// easily assigned to different contexts (global, scene, or object).
/// </remarks>
/// <example>
/// <code>
/// // Create a concrete installer
/// [CreateAssetMenu(fileName = "GameplayInstaller", menuName = "Installers/GameplayInstaller")]
/// public class GameplayInstaller : ScriptableObjectInstaller
/// {
///     [SerializeField] private PlayerController playerPrefab;
///     
///     public override void InstallBindings(IContainer container)
///     {
///         // Register services
///         container.Bind<IPlayerService>().To<PlayerService>().AsSingleton();
///         container.Bind<IEnemyFactory>().To<StandardEnemyFactory>().AsSingleton();
///         
///         // Register prefab-based services
///         BindPersistentComponent<IPlayerController, PlayerController>(container, playerPrefab);
///     }
/// }
/// </code>
/// </example>
public abstract class ScriptableObjectInstaller : ScriptableObject
{
    // Implementation details...
}
```

## Binding Syntax

UnityInject uses a fluent syntax for configuring bindings:

```csharp
/// <summary>
/// Interface for configuring what a service type is bound to.
/// </summary>
public interface IBindingSyntax
{
    /// <summary>
    /// Binds the service type to a specific implementation type.
    /// </summary>
    /// <typeparam name="TImplementation">The concrete implementation type.</typeparam>
    /// <returns>A syntax helper to configure the binding's lifetime.</returns>
    /// <example>
    /// <code>
    /// container.Bind<IPlayerService>().To<PlayerService>().AsSingleton();
    /// </code>
    /// </example>
    ILifetimeSyntax To<TImplementation>() where TImplementation : class;
    
    /// <summary>
    /// Binds the service type to an existing instance.
    /// The binding will automatically be a singleton.
    /// </summary>
    /// <param name="instance">The instance to bind to.</param>
    /// <returns>A syntax helper to configure the binding's lifetime (limited to singleton).</returns>
    /// <example>
    /// <code>
    /// var existingManager = new AudioManager();
    /// container.Bind<IAudioManager>().FromInstance(existingManager);
    /// </code>
    /// </example>
    ILifetimeSyntax FromInstance(object instance);
    
    /// <summary>
    /// Binds the service type to a factory method that will be called to create instances.
    /// </summary>
    /// <param name="factoryMethod">The factory method to use for creating instances.</param>
    /// <returns>A syntax helper to configure the binding's lifetime.</returns>
    /// <example>
    /// <code>
    /// container.Bind<IRandomService>().FromMethod(c => new RandomService(UnityEngine.Random.Range(1, 100))).AsSingleton();
    /// </code>
    /// </example>
    ILifetimeSyntax FromMethod(Func<IContainer, object> factoryMethod);
}

/// <summary>
/// Interface for configuring a binding's lifetime.
/// </summary>
public interface ILifetimeSyntax
{
    /// <summary>
    /// Configures the binding to provide a single shared instance for all resolutions.
    /// </summary>
    /// <returns>The binding configuration.</returns>
    /// <example>
    /// <code>
    /// container.Bind<IScoreManager>().To<ScoreManager>().AsSingleton();
    /// </code>
    /// </example>
    void AsSingleton();
    
    /// <summary>
    /// Configures the binding to provide a new instance for each resolution.
    /// </summary>
    /// <returns>The binding configuration.</returns>
    /// <example>
    /// <code>
    /// container.Bind<IEnemyFactory>().To<StandardEnemyFactory>().AsTransient();
    /// </code>
    /// </example>
    void AsTransient();
}
```

## Injection Attributes

UnityInject uses attributes to mark fields and properties for injection:

```csharp
/// <summary>
/// Marks a field or property for dependency injection.
/// The container will automatically inject an instance of the appropriate type.
/// </summary>
/// <example>
/// <code>
/// public class PlayerController : MonoBehaviour
/// {
///     [Inject] private IInputService _inputService;
///     [Inject] private IWeaponManager _weaponManager;
///     
///     [Inject(Optional = true)] private IAnalyticsService _analytics;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter)]
public class InjectAttribute : Attribute
{
    /// <summary>
    /// If true, the dependency is optional and no error will be thrown if it cannot be resolved.
    /// </summary>
    public bool Optional { get; set; } = false;
}
```

## Extension Methods

UnityInject provides several useful extension methods:

```csharp
/// <summary>
/// Extension methods for the IContainer interface.
/// </summary>
public static class ContainerExtensions
{
    /// <summary>
    /// Instantiates a prefab and injects dependencies into all its components.
    /// </summary>
    /// <param name="container">The container to resolve dependencies from.</param>
    /// <param name="prefab">The prefab to instantiate.</param>
    /// <param name="position">The position for the instantiated object.</param>
    /// <param name="rotation">The rotation for the instantiated object.</param>
    /// <returns>The instantiated GameObject with dependencies injected.</returns>
    /// <example>
    /// <code>
    /// public class EnemySpawner : MonoBehaviour
    /// {
    ///     [Inject] private IContainer _container;
    ///     [SerializeField] private GameObject _enemyPrefab;
    ///     
    ///     public void SpawnEnemy()
    ///     {
    ///         GameObject enemy = _container.InstantiatePrefab(_enemyPrefab, transform.position, Quaternion.identity);
    ///     }
    /// }
    /// </code>
    /// </example>
    public static GameObject InstantiatePrefab(this IContainer container, GameObject prefab, Vector3 position, Quaternion rotation)
    {
        // Implementation details...
    }
}
```
