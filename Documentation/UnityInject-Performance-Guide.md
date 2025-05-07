# UnityInject Performance Guide

This guide provides recommendations for optimizing performance when using the UnityInject dependency injection system in your Unity projects.

## Table of Contents

1. [Understanding Performance Considerations](#understanding-performance-considerations)
2. [Optimizing Container Configuration](#optimizing-container-configuration)
3. [Efficient Injection Strategies](#efficient-injection-strategies)
4. [Scene Loading Optimization](#scene-loading-optimization)
5. [Prefab Instantiation Best Practices](#prefab-instantiation-best-practices)
6. [Memory Management](#memory-management)
7. [Profiling and Debugging](#profiling-and-debugging)

## Understanding Performance Considerations

UnityInject uses reflection to discover and inject dependencies, which can have performance implications if not used carefully. The main performance considerations are:

1. **Initialization Time**: The time it takes to set up containers and perform initial injections.
2. **Memory Usage**: The memory overhead of maintaining container hierarchies and cached reflection data.
3. **Runtime Performance**: The impact of dependency resolution during gameplay.

## Optimizing Container Configuration

### Minimize Global Installers

Global installers run at application startup and can impact initial loading time.

```csharp
// GOOD: Focused global installer with only essential services
public class GlobalServiceInstaller : ScriptableObjectInstaller
{
    public override void InstallBindings(IContainer container)
    {
        // Only include truly global services
        container.Bind<IAudioService>().To<AudioService>().AsSingleton();
        container.Bind<IInputService>().To<InputService>().AsSingleton();
        container.Bind<ISaveSystem>().To<SaveSystem>().AsSingleton();
    }
}
```

### Use Appropriate Binding Lifetimes

Choose the right lifetime for your services to avoid unnecessary object creation or memory usage.

```csharp
// Singleton: Created once and reused (higher memory, faster resolution)
container.Bind<IScoreManager>().To<ScoreManager>().AsSingleton();

// Transient: Created each time (lower memory, slower resolution)
container.Bind<IBulletFactory>().To<BulletFactory>().AsTransient();
```

### Pre-warm Frequently Used Services

For critical services that need to be available immediately:

```csharp
public class ServicePrewarmer : MonoBehaviour
{
    [Inject] private IContainer _container;
    
    private void Start()
    {
        // Force creation of important services at startup
        _container.Resolve<IPlayerService>();
        _container.Resolve<IEnemyManager>();
        _container.Resolve<IWeaponSystem>();
    }
}
```

## Efficient Injection Strategies

### Limit Injection Points

Only inject what you actually need:

```csharp
// BAD: Injecting services that aren't used
public class PlayerController : MonoBehaviour
{
    [Inject] private IInputService _inputService; // Used
    [Inject] private IAudioService _audioService; // Used
    [Inject] private IAnalyticsService _analyticsService; // Rarely used
    [Inject] private ILeaderboardService _leaderboardService; // Never used
}

// GOOD: Only inject what you need
public class PlayerController : MonoBehaviour
{
    [Inject] private IInputService _inputService;
    [Inject] private IAudioService _audioService;
}
```

### Use Optional Injection Wisely

Optional injection requires additional checks:

```csharp
// Only use Optional for truly optional dependencies
[Inject(Optional = true)] private IAnalyticsService _analyticsService;

private void LogEvent(string eventName)
{
    // This null check has a small performance cost
    if (_analyticsService != null)
    {
        _analyticsService.LogEvent(eventName);
    }
}
```

### Batch Injections

When manually injecting, batch operations where possible:

```csharp
// GOOD: Batch injection for a group of objects
public void InitializeEnemies(List<Enemy> enemies)
{
    foreach (var enemy in enemies)
    {
        _container.InjectInto(enemy);
    }
}
```

## Scene Loading Optimization

### Optimize SceneContext Scanning

The `SceneContext` scans the entire scene for MonoBehaviours, which can be expensive in large scenes:

```csharp
// Consider organizing your scene with empty parent GameObjects
// to group objects that need injection vs. those that don't

// For very large scenes, consider manual injection for specific hierarchies
public class ManualInjectionHelper : MonoBehaviour
{
    [Inject] private IContainer _container;
    [SerializeField] private GameObject _complexHierarchy;
    
    private void Start()
    {
        // Manually inject into a specific hierarchy
        var components = _complexHierarchy.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var component in components)
        {
            _container.InjectInto(component);
        }
    }
}
```

### Lazy-Load Scene Dependencies

For scenes with many dependencies, consider lazy loading:

```csharp
public class LazyDependencyLoader : MonoBehaviour
{
    [Inject] private IContainer _container;
    [SerializeField] private GameObject _heavySystemPrefab;
    
    private GameObject _heavySystemInstance;
    
    public void LoadHeavySystemWhenNeeded()
    {
        if (_heavySystemInstance == null)
        {
            _heavySystemInstance = _container.InstantiatePrefab(_heavySystemPrefab, transform.position, Quaternion.identity);
        }
    }
}
```

## Prefab Instantiation Best Practices

### Optimize GameObjectContext Usage

`GameObjectContext` creates a new container for each prefab instance, which has overhead:

```csharp
// Only use GameObjectContext when you need instance-specific dependencies
// For prefabs that share the same dependencies, consider using the scene container

// GOOD: For enemies that need unique stats
// Add GameObjectContext to the prefab

// GOOD: For bullets that are identical
// Don't use GameObjectContext, just use scene container injection
```

### Pool Objects with Injection

Combine object pooling with injection for frequently created/destroyed objects:

```csharp
public class BulletPool : MonoBehaviour
{
    [Inject] private IContainer _container;
    [SerializeField] private GameObject _bulletPrefab;
    
    private Queue<GameObject> _pool = new Queue<GameObject>();
    private int _initialSize = 20;
    
    private void Start()
    {
        // Pre-populate pool
        for (int i = 0; i < _initialSize; i++)
        {
            var bullet = _container.InstantiatePrefab(_bulletPrefab, Vector3.zero, Quaternion.identity);
            bullet.SetActive(false);
            _pool.Enqueue(bullet);
        }
    }
    
    public GameObject GetBullet()
    {
        if (_pool.Count > 0)
        {
            var bullet = _pool.Dequeue();
            bullet.SetActive(true);
            return bullet;
        }
        
        // Create new if pool is empty
        return _container.InstantiatePrefab(_bulletPrefab, Vector3.zero, Quaternion.identity);
    }
    
    public void ReturnBullet(GameObject bullet)
    {
        bullet.SetActive(false);
        _pool.Enqueue(bullet);
    }
}
```

## Memory Management

### Dispose Containers Properly

Ensure containers are properly disposed to release resources:

```csharp
// SceneContext and GameObjectContext automatically dispose their containers
// when destroyed, but if you create containers manually, ensure proper disposal:

public class CustomContainerExample : MonoBehaviour
{
    private IContainer _customContainer;
    
    private void Start()
    {
        _customContainer = ProjectInitializer.RootContainer.CreateChildContainer();
        // Configure container...
    }
    
    private void OnDestroy()
    {
        _customContainer?.Dispose();
    }
}
```

### Clean Up Event Subscriptions

If your injected services use events, ensure proper cleanup:

```csharp
public class PlayerController : MonoBehaviour
{
    [Inject] private IInputService _inputService;
    
    private void OnEnable()
    {
        _inputService.OnFirePressed += OnFirePressed;
    }
    
    private void OnDisable()
    {
        // Clean up to prevent memory leaks
        _inputService.OnFirePressed -= OnFirePressed;
    }
    
    private void OnFirePressed() { /* ... */ }
}
```

## Profiling and Debugging

### Monitor Initialization Time

Profile the initialization time of your containers:

```csharp
public class PerformanceMonitor : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(MeasureInitializationTime());
    }
    
    private IEnumerator MeasureInitializationTime()
    {
        // Wait for end of frame to ensure all initialization is complete
        yield return new WaitForEndOfFrame();
        
        // Log time since startup
        Debug.Log($"Time to fully initialize: {Time.realtimeSinceStartup} seconds");
    }
}
```

### Debug Container Hierarchy

Visualize your container hierarchy for debugging:

```csharp
public static class ContainerDebugExtensions
{
    public static void LogContainerHierarchy(this IContainer container, string prefix = "")
    {
        Debug.Log($"{prefix}Container: {container.GetType().Name}");
        
        // Log bindings (requires access to internal bindings)
        // This is a simplified example - you'd need to extend Container to expose bindings
        /*
        foreach (var binding in container.GetBindings())
        {
            Debug.Log($"{prefix}  Binding: {binding.ServiceType.Name} -> {binding.ImplementationType?.Name ?? "Instance"}");
        }
        */
        
        // Log child containers if you track them
        // This would require extending the Container class to track children
    }
}
```

## Performance Optimization Summary

1. **Be selective with global services** - Only make truly global services available at the application level.
2. **Choose appropriate lifetimes** - Use singletons for shared services and transient for disposable objects.
3. **Minimize injection points** - Only inject what you actually need.
4. **Optimize scene scanning** - Structure your scenes to minimize the cost of dependency scanning.
5. **Use object pooling** - Combine with injection for frequently created/destroyed objects.
6. **Properly dispose resources** - Ensure containers and services are properly cleaned up.
7. **Profile and monitor** - Keep an eye on initialization times and memory usage.

By following these guidelines, you can use UnityInject effectively while maintaining good performance in your Unity projects.
