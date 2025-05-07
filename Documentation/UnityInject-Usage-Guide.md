# UnityInject Usage Guide

This guide provides comprehensive examples and best practices for using the UnityInject dependency injection system in your Unity projects.

## Table of Contents

1. [Getting Started](#getting-started)
2. [Container Hierarchy](#container-hierarchy)
3. [Binding Services](#binding-services)
4. [Injecting Dependencies](#injecting-dependencies)
5. [Working with Prefabs](#working-with-prefabs)
6. [Common Patterns](#common-patterns)
7. [Anti-Patterns to Avoid](#anti-patterns-to-avoid)
8. [Troubleshooting](#troubleshooting)

## Getting Started

UnityInject is a dependency injection framework designed specifically for Unity. It helps manage dependencies between different parts of your application, making your code more modular, testable, and maintainable.

### Automatic Initialization

The framework automatically initializes itself. A root DI container (`IContainer`) is created before any scene loads, thanks to the `ProjectInitializer`. You don't need to manually create the first container.

### Basic Setup

1. **Create a Global Installer**:
   ```csharp
   [CreateAssetMenu(fileName = "GlobalServiceInstaller", menuName = "Installers/GlobalServiceInstaller")]
   public class GlobalServiceInstaller : ScriptableObjectInstaller
   {
       public override void InstallBindings(IContainer container)
       {
           // Register global services
           container.Bind<IAudioService>().To<AudioService>().AsSingleton();
           container.Bind<IInputService>().To<InputService>().AsSingleton();
           container.Bind<ISaveManager>().To<PlayerPrefsSaveManager>().AsSingleton();
       }
   }
   ```

2. **Place the installer in the correct location**:
   - Create the asset in `Resources/Installers/Global` to make it a global installer.

3. **Set up Scene Injection**:
   - Create an empty GameObject in your scene named `_SceneContext`.
   - Add the `SceneContext` component to it.
   - Create and assign scene-specific installers to the `Scene Installers` field.

## Container Hierarchy

UnityInject uses a hierarchical container system with three main levels:

### 1. Global Container (Root)

- **Scope**: Application-wide, exists for the entire lifetime of the application.
- **Purpose**: Manages global services and systems that should be available everywhere.
- **Examples**: `IAudioService`, `IInputService`, `ISaveManager`, `IAnalyticsService`.
- **Implementation**: Created automatically by `ProjectInitializer` before any scene loads.
- **Configuration**: Place installer assets in `Resources/Installers/Global`.

### 2. Scene Container

- **Scope**: Specific to a single loaded scene. Created when the scene loads, destroyed when it unloads.
- **Purpose**: Manages systems and data relevant only to the current scene.
- **Examples**: `IGameplayManager`, `IObjectiveTracker`, `ISceneUIManager`, `IEnemySpawner`.
- **Implementation**: Requires a `SceneContext` component on a GameObject in the scene.
- **Configuration**: Assign installer assets to the `Scene Installers` field on the `SceneContext` component.

### 3. GameObject Container

- **Scope**: Specific to an instance of a prefab or a complex GameObject hierarchy.
- **Purpose**: Manages dependencies specific to one instance of an object.
- **Examples**: An individual enemy's AI controller, unique stats, specific weapon controller.
- **Implementation**: Requires a `GameObjectContext` component on the root of the prefab/GameObject.
- **Configuration**: Assign installer assets to the `Object Installers` field on the `GameObjectContext` component.

## Binding Services

Binding is the process of telling the container how to create or find instances of your services.

### Basic Binding Types

```csharp
// Bind an interface to a concrete implementation as a singleton
container.Bind<IPlayerService>().To<PlayerService>().AsSingleton();

// Bind to a new instance each time (transient)
container.Bind<IEnemyFactory>().To<StandardEnemyFactory>().AsTransient();

// Bind to an existing instance
var existingManager = new AudioManager();
container.Bind<IAudioManager>().FromInstance(existingManager);

// Bind using a factory method
container.Bind<IRandomService>().FromMethod(c => {
    return new RandomService(UnityEngine.Random.Range(1, 100));
}).AsSingleton();
```

### Binding MonoBehaviours

For MonoBehaviours, you typically bind them using prefabs or existing instances:

```csharp
// In an installer:
[SerializeField] private PlayerController playerPrefab;

public override void InstallBindings(IContainer container)
{
    // Create a persistent singleton from a prefab
    BindPersistentComponent<IPlayerController, PlayerController>(container, playerPrefab);
    
    // Or bind an existing instance in the scene
    var uiManager = FindObjectOfType<UIManager>();
    container.Bind<IUIManager>().FromInstance(uiManager);
}
```

## Injecting Dependencies

### Field Injection

The most common way to inject dependencies is through fields:

```csharp
public class PlayerController : MonoBehaviour
{
    [Inject] private IInputService _inputService;
    [Inject] private IWeaponManager _weaponManager;
    
    // Optional dependency - no error if not found
    [Inject(Optional = true)] private IAnalyticsService _analytics;
    
    private void Start()
    {
        // Use injected dependencies
        _inputService.OnFirePressed += _weaponManager.FirePrimaryWeapon;
        
        if (_analytics != null) {
            _analytics.LogEvent("PlayerSpawned");
        }
    }
}
```

### Property Injection

You can also inject through properties:

```csharp
public class EnemyAI : MonoBehaviour
{
    [Inject]
    public IPathfindingService PathfindingService { get; private set; }
    
    [Inject]
    public IPlayerTracker PlayerTracker { get; private set; }
}
```

### Injecting the Container

Sometimes you need access to the container itself:

```csharp
public class PrefabSpawner : MonoBehaviour
{
    [Inject] private IContainer _container;
    [SerializeField] private GameObject _prefabToSpawn;
    
    public void SpawnPrefab()
    {
        // Use the container to instantiate and inject
        var instance = _container.InstantiatePrefab(_prefabToSpawn, transform.position, Quaternion.identity);
    }
}
```

## Working with Prefabs

### Prefabs with GameObjectContext

For prefabs that need their own dependency scope:

1. Add `GameObjectContext` to the root GameObject of your prefab.
2. Create prefab-specific installers if needed and assign them to the `Object Installers` field.
3. Instantiate the prefab using `container.InstantiatePrefab()` to ensure proper injection.

```csharp
// Example of a self-binding component for a prefab
public class EnemySelfBinder : MonoBehaviour, IObjectContextBinder
{
    [SerializeField] private EnemyStats enemyStats;
    
    public void RegisterBindings(IContainer container)
    {
        // Bind this specific enemy's stats
        container.Bind<EnemyStats>().FromInstance(enemyStats);
    }
}
```

### Simple Prefabs

For simpler prefabs without their own context:

```csharp
public class EnemySpawner : MonoBehaviour
{
    [Inject] private IContainer _container;
    [SerializeField] private GameObject _simpleEnemyPrefab;
    
    public void SpawnSimpleEnemy()
    {
        // Instantiate and inject using the scene container
        var instance = _container.InstantiatePrefab(_simpleEnemyPrefab, transform.position, Quaternion.identity);
    }
}
```

## Common Patterns

### Service Locator Pattern (via Injection)

Instead of using a global service locator (anti-pattern), inject the services you need:

```csharp
// GOOD: Explicit dependencies
public class PlayerController : MonoBehaviour
{
    [Inject] private IInputService _inputService;
    [Inject] private IAudioService _audioService;
    
    private void PlayJumpSound()
    {
        _audioService.PlaySound("Jump");
    }
}
```

### Factory Pattern

Use factories to create complex objects:

```csharp
public interface IEnemyFactory
{
    Enemy CreateEnemy(EnemyType type, Vector3 position);
}

public class StandardEnemyFactory : IEnemyFactory
{
    [Inject] private IContainer _container;
    [SerializeField] private GameObject[] _enemyPrefabs;
    
    public Enemy CreateEnemy(EnemyType type, Vector3 position)
    {
        var prefab = _enemyPrefabs[(int)type];
        var instance = _container.InstantiatePrefab(prefab, position, Quaternion.identity);
        return instance.GetComponent<Enemy>();
    }
}
```

### Facade Pattern

Use facades to simplify complex subsystems:

```csharp
public interface IGameplayFacade
{
    void StartLevel(int levelId);
    void PauseGame();
    void ResumeGame();
    void EndLevel(bool success);
}

public class GameplayFacade : IGameplayFacade
{
    [Inject] private ILevelLoader _levelLoader;
    [Inject] private ITimeManager _timeManager;
    [Inject] private IScoreManager _scoreManager;
    [Inject] private IUIManager _uiManager;
    
    public void StartLevel(int levelId)
    {
        _levelLoader.LoadLevel(levelId);
        _timeManager.ResetTime();
        _scoreManager.ResetScore();
        _uiManager.ShowGameplayUI();
    }
    
    // Other methods...
}
```

### Command Pattern

Use commands for encapsulating operations:

```csharp
public interface ICommand
{
    void Execute();
}

public class SpawnEnemyWaveCommand : ICommand
{
    [Inject] private IEnemySpawner _spawner;
    
    private readonly EnemyWaveConfig _waveConfig;
    
    public SpawnEnemyWaveCommand(EnemyWaveConfig waveConfig)
    {
        _waveConfig = waveConfig;
    }
    
    public void Execute()
    {
        _spawner.SpawnWave(_waveConfig);
    }
}
```

## Anti-Patterns to Avoid

### 1. Service Locator

**Bad**:
```csharp
// Avoid this pattern
public class PlayerController : MonoBehaviour
{
    private void Start()
    {
        ServiceLocator.Get<IAudioService>().PlaySound("Spawn");
    }
}
```

**Good**:
```csharp
public class PlayerController : MonoBehaviour
{
    [Inject] private IAudioService _audioService;
    
    private void Start()
    {
        _audioService.PlaySound("Spawn");
    }
}
```

### 2. Circular Dependencies

**Bad**:
```csharp
// Class A depends on B, and B depends on A
public class PlayerManager
{
    [Inject] private WeaponManager _weaponManager;
}

public class WeaponManager
{
    [Inject] private PlayerManager _playerManager; // Circular dependency!
}
```

**Good**:
```csharp
// Refactor to avoid circular dependency
public class PlayerManager
{
    [Inject] private WeaponManager _weaponManager;
}

public class WeaponManager
{
    // Use events or interfaces to communicate back to PlayerManager
    public event Action<Weapon> WeaponChanged;
}
```

### 3. Container as Service Locator

**Bad**:
```csharp
public class BadManager : MonoBehaviour
{
    [Inject] private IContainer _container;
    
    private void DoSomething()
    {
        // Using container as service locator
        var service = _container.Resolve<ISomeService>();
        service.DoWork();
    }
}
```

**Good**:
```csharp
public class GoodManager : MonoBehaviour
{
    [Inject] private ISomeService _someService;
    
    private void DoSomething()
    {
        _someService.DoWork();
    }
}
```

### 4. God Objects

**Bad**:
```csharp
// Too many responsibilities in one class
public class GameManager : MonoBehaviour
{
    [Inject] private IInputService _inputService;
    [Inject] private IAudioService _audioService;
    [Inject] private IUIManager _uiManager;
    [Inject] private IPlayerManager _playerManager;
    [Inject] private IEnemyManager _enemyManager;
    [Inject] private ILevelManager _levelManager;
    // ... many more dependencies
    
    // Hundreds of methods managing everything
}
```

**Good**:
```csharp
// Split into focused managers with clear responsibilities
public class GameplayController : MonoBehaviour
{
    [Inject] private IGameStateMachine _stateMachine;
    [Inject] private ILevelFacade _levelFacade;
    [Inject] private IPlayerFacade _playerFacade;
    
    // Focused on high-level game flow
}
```

## Troubleshooting

### Common Issues and Solutions

#### 1. Missing Dependencies

**Symptom**: `Could not resolve mandatory dependency of type X for field Y in Z`

**Solutions**:
- Check that the dependency is properly bound in an installer
- Verify the installer is assigned to the correct context
- Make sure the dependency is marked as `[Inject(Optional = true)]` if it's optional

#### 2. Circular Dependencies

**Symptom**: `Circular dependency detected while resolving type 'X'. Resolution path: X -> Y -> X`

**Solutions**:
- Refactor one of the classes to use events or callbacks instead of direct references
- Extract the circular part into a separate service that both can depend on
- Use a factory or provider to break the cycle

#### 3. GameObjectContext Not Working

**Symptom**: Dependencies not injected into prefab instances

**Solutions**:
- Make sure you're instantiating the prefab using `container.InstantiatePrefab()`
- Check that the `GameObjectContext` component is on the root GameObject of the prefab
- Verify that any required installers are assigned to the `Object Installers` field

#### 4. Scene Dependencies Not Injected

**Symptom**: Dependencies not injected into scene objects

**Solutions**:
- Ensure there's a `SceneContext` component in your scene
- Check that scene-specific installers are assigned to the `Scene Installers` field
- Verify the execution order - `SceneContext` should initialize before other scripts
