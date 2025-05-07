# UnityInject: Patterns and Anti-Patterns

This document outlines recommended patterns and anti-patterns when using the UnityInject dependency injection system. Following these guidelines will help you create more maintainable, testable, and robust code.

## Recommended Patterns

### 1. Dependency Inversion Principle

**Pattern**: Code against interfaces, not concrete implementations.

**Example**:
```csharp
// GOOD: Depend on abstractions
public class PlayerController : MonoBehaviour
{
    [Inject] private IInputService _inputService;
    [Inject] private IWeaponService _weaponService;
}

// BAD: Depending on concrete implementations
public class PlayerController : MonoBehaviour
{
    [Inject] private KeyboardInputService _inputService;
    [Inject] private StandardWeaponService _weaponService;
}
```

**Benefits**:
- Easier to swap implementations (e.g., for testing or platform-specific code)
- Clearer contracts between components
- Better separation of concerns

### 2. Constructor Injection for Non-MonoBehaviours

**Pattern**: For regular C# classes (not MonoBehaviours), prefer constructor injection over field injection.

**Example**:
```csharp
// GOOD: Constructor injection for regular classes
public class EnemyAI
{
    private readonly IPathfindingService _pathfinding;
    private readonly IPlayerTracker _playerTracker;
    
    public EnemyAI(IPathfindingService pathfinding, IPlayerTracker playerTracker)
    {
        _pathfinding = pathfinding;
        _playerTracker = playerTracker;
    }
}

// Then bind with a factory method if needed:
container.Bind<IEnemyAI>().FromMethod(c => 
    new EnemyAI(
        c.Resolve<IPathfindingService>(),
        c.Resolve<IPlayerTracker>()
    )
).AsSingleton();
```

**Benefits**:
- Makes dependencies explicit and required
- Objects are always in a valid state after construction
- Works better with unit testing

### 3. Facade Pattern for Subsystems

**Pattern**: Create facade interfaces for complex subsystems to simplify their usage.

**Example**:
```csharp
// GOOD: Using a facade to simplify a complex subsystem
public interface IAudioFacade
{
    void PlayMusic(string trackName);
    void PlaySoundEffect(string sfxName, Vector3 position);
    void StopAllAudio();
}

public class AudioFacade : IAudioFacade
{
    [Inject] private IMusicPlayer _musicPlayer;
    [Inject] private ISoundEffectPlayer _sfxPlayer;
    [Inject] private IAudioMixer _audioMixer;
    
    public void PlayMusic(string trackName)
    {
        _musicPlayer.FadeOutCurrent();
        _musicPlayer.LoadTrack(trackName);
        _musicPlayer.Play();
    }
    
    // Other methods...
}
```

**Benefits**:
- Simplifies client code
- Hides complex interactions between subsystem components
- Provides a clear, high-level API

### 4. Factory Pattern for Complex Object Creation

**Pattern**: Use factories to encapsulate complex object creation logic.

**Example**:
```csharp
public interface IEnemyFactory
{
    Enemy CreateEnemy(EnemyType type, Vector3 position);
}

public class EnemyFactory : IEnemyFactory
{
    [Inject] private IContainer _container;
    [SerializeField] private Dictionary<EnemyType, GameObject> _enemyPrefabs;
    
    public Enemy CreateEnemy(EnemyType type, Vector3 position)
    {
        if (!_enemyPrefabs.TryGetValue(type, out var prefab))
        {
            Debug.LogError($"No prefab found for enemy type: {type}");
            return null;
        }
        
        var instance = _container.InstantiatePrefab(prefab, position, Quaternion.identity);
        return instance.GetComponent<Enemy>();
    }
}
```

**Benefits**:
- Encapsulates creation logic
- Provides a clear API for object creation
- Can include additional setup logic beyond just instantiation

### 5. Composition Root Pattern

**Pattern**: Configure all dependencies in dedicated installer classes.

**Example**:
```csharp
// GOOD: Dedicated installer for a subsystem
[CreateAssetMenu(fileName = "AudioSystemInstaller", menuName = "Installers/AudioSystemInstaller")]
public class AudioSystemInstaller : ScriptableObjectInstaller
{
    [SerializeField] private AudioMixer audioMixerPrefab;
    [SerializeField] private MusicPlayer musicPlayerPrefab;
    
    public override void InstallBindings(IContainer container)
    {
        // Bind concrete implementations
        container.Bind<IAudioMixer>().To<AudioMixer>().AsSingleton();
        container.Bind<IMusicPlayer>().To<MusicPlayer>().AsSingleton();
        container.Bind<ISoundEffectPlayer>().To<SoundEffectPlayer>().AsSingleton();
        
        // Bind the facade
        container.Bind<IAudioFacade>().To<AudioFacade>().AsSingleton();
        
        // Bind prefab-based components
        BindPersistentComponent<IAudioMixer, AudioMixer>(container, audioMixerPrefab);
        BindPersistentComponent<IMusicPlayer, MusicPlayer>(container, musicPlayerPrefab);
    }
}
```

**Benefits**:
- Centralizes configuration
- Makes dependencies explicit
- Easier to understand the system's structure

### 6. Scoped Containers for Isolated Components

**Pattern**: Use `GameObjectContext` for prefabs that need their own isolated scope.

**Example**:
```csharp
// On a prefab:
// - Add GameObjectContext to the root
// - Add a component implementing IObjectContextBinder

public class EnemyContextBinder : MonoBehaviour, IObjectContextBinder
{
    [SerializeField] private EnemyStats stats;
    
    public void RegisterBindings(IContainer container)
    {
        // Bind instance-specific data
        container.Bind<EnemyStats>().FromInstance(stats);
        
        // Bind instance-specific services
        container.Bind<IEnemyAI>().To<StandardEnemyAI>().AsSingleton();
    }
}
```

**Benefits**:
- Isolates instance-specific dependencies
- Allows multiple instances with different configurations
- Prevents cross-contamination between instances

## Anti-Patterns to Avoid

### 1. Service Locator Anti-Pattern

**Anti-Pattern**: Using a global service locator to access services.

**Example**:
```csharp
// BAD: Using a service locator
public class PlayerController : MonoBehaviour
{
    private void Start()
    {
        var inputService = ServiceLocator.Get<IInputService>();
        var audioService = ServiceLocator.Get<IAudioService>();
    }
}
```

**Problems**:
- Hides dependencies, making them implicit rather than explicit
- Makes testing more difficult
- Can lead to runtime errors if services aren't registered

**Solution**: Use dependency injection instead:
```csharp
// GOOD: Using dependency injection
public class PlayerController : MonoBehaviour
{
    [Inject] private IInputService _inputService;
    [Inject] private IAudioService _audioService;
}
```

### 2. Container as Service Locator Anti-Pattern

**Anti-Pattern**: Injecting the container itself and using it to resolve services on demand.

**Example**:
```csharp
// BAD: Using the container as a service locator
public class GameManager : MonoBehaviour
{
    [Inject] private IContainer _container;
    
    private void Start()
    {
        var inputService = _container.Resolve<IInputService>();
        var audioService = _container.Resolve<IAudioService>();
    }
}
```

**Problems**:
- Same issues as the service locator pattern
- Bypasses the benefits of dependency injection
- Makes dependencies implicit

**Solution**: Inject the specific services you need:
```csharp
// GOOD: Injecting specific services
public class GameManager : MonoBehaviour
{
    [Inject] private IInputService _inputService;
    [Inject] private IAudioService _audioService;
}
```

### 3. Circular Dependencies

**Anti-Pattern**: Creating circular dependencies between services.

**Example**:
```csharp
// BAD: Circular dependency
public class PlayerManager
{
    [Inject] private WeaponManager _weaponManager;
}

public class WeaponManager
{
    [Inject] private PlayerManager _playerManager; // Circular dependency!
}
```

**Problems**:
- Can cause resolution errors
- Makes the system harder to understand
- Indicates a design problem

**Solution**: Refactor to break the cycle:
```csharp
// GOOD: Using events to break the cycle
public class PlayerManager
{
    [Inject] private WeaponManager _weaponManager;
    
    public void Initialize()
    {
        _weaponManager.WeaponChanged += OnWeaponChanged;
    }
    
    private void OnWeaponChanged(Weapon weapon) { /* ... */ }
}

public class WeaponManager
{
    public event Action<Weapon> WeaponChanged;
    
    public void SwitchWeapon(Weapon weapon)
    {
        // Logic...
        WeaponChanged?.Invoke(weapon);
    }
}
```

### 4. God Object Anti-Pattern

**Anti-Pattern**: Creating classes with too many dependencies and responsibilities.

**Example**:
```csharp
// BAD: Too many dependencies and responsibilities
public class GameManager : MonoBehaviour
{
    [Inject] private IInputService _inputService;
    [Inject] private IAudioService _audioService;
    [Inject] private IUIManager _uiManager;
    [Inject] private IPlayerManager _playerManager;
    [Inject] private IEnemyManager _enemyManager;
    [Inject] private ILevelManager _levelManager;
    [Inject] private ISaveSystem _saveSystem;
    [Inject] private IAnalyticsService _analyticsService;
    // ... many more dependencies
    
    // Hundreds of methods managing everything
}
```

**Problems**:
- Violates Single Responsibility Principle
- Hard to test and maintain
- Becomes a bottleneck for changes

**Solution**: Split into smaller, focused classes:
```csharp
// GOOD: Focused classes with clear responsibilities
public class GameStateController : MonoBehaviour
{
    [Inject] private IGameStateMachine _stateMachine;
    
    // Methods for high-level game flow control
}

public class LevelController : MonoBehaviour
{
    [Inject] private ILevelManager _levelManager;
    [Inject] private IEnemyManager _enemyManager;
    
    // Methods for level-specific logic
}

public class UIController : MonoBehaviour
{
    [Inject] private IUIManager _uiManager;
    
    // Methods for UI control
}
```

### 5. Overusing Singletons

**Anti-Pattern**: Making everything a singleton by default.

**Example**:
```csharp
// BAD: Making everything a singleton
public override void InstallBindings(IContainer container)
{
    container.Bind<IPlayerService>().To<PlayerService>().AsSingleton();
    container.Bind<IEnemyService>().To<EnemyService>().AsSingleton();
    container.Bind<IWeaponService>().To<WeaponService>().AsSingleton();
    container.Bind<IBulletService>().To<BulletService>().AsSingleton();
    // Everything is a singleton!
}
```

**Problems**:
- Can lead to unexpected state sharing
- Makes testing more difficult
- Can cause memory leaks if not properly managed

**Solution**: Use the appropriate lifetime for each service:
```csharp
// GOOD: Using appropriate lifetimes
public override void InstallBindings(IContainer container)
{
    // Global services as singletons
    container.Bind<IAudioService>().To<AudioService>().AsSingleton();
    container.Bind<ISaveSystem>().To<SaveSystem>().AsSingleton();
    
    // Services that should be created per-use as transient
    container.Bind<IEnemyFactory>().To<EnemyFactory>().AsTransient();
    container.Bind<IBulletFactory>().To<BulletFactory>().AsTransient();
}
```

### 6. Ignoring Disposal

**Anti-Pattern**: Not properly disposing of services that implement IDisposable.

**Example**:
```csharp
// BAD: Not handling disposal
public class NetworkService : INetworkService, IDisposable
{
    private HttpClient _httpClient = new HttpClient();
    
    // Methods...
    
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

// And then binding without considering disposal:
container.Bind<INetworkService>().To<NetworkService>().AsSingleton();
```

**Problems**:
- Can cause resource leaks
- May leave connections open
- Can lead to unexpected behavior

**Solution**: The UnityInject container automatically tracks and disposes of singleton services that implement IDisposable when the container is disposed. Make sure your services properly implement IDisposable if they manage disposable resources.

## Best Practices Summary

1. **Depend on abstractions** (interfaces or abstract classes), not concrete implementations.
2. **Keep services focused** with a single responsibility.
3. **Make dependencies explicit** through injection, not service location.
4. **Use appropriate container scopes** (global, scene, or object) based on the lifetime needs of your services.
5. **Prefer constructor injection** for non-MonoBehaviour classes.
6. **Use field/property injection** for MonoBehaviours.
7. **Avoid circular dependencies** by using events, callbacks, or restructuring your code.
8. **Create dedicated installers** for different subsystems.
9. **Use factories** for complex object creation.
10. **Consider disposal** for services that manage resources.

By following these patterns and avoiding the anti-patterns, you'll create a more maintainable, testable, and robust application with UnityInject.
