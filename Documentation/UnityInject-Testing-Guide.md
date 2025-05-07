# UnityInject Testing Guide

This guide provides strategies and best practices for testing code that uses the UnityInject dependency injection system.

## Table of Contents

1. [Introduction to Testing with DI](#introduction-to-testing-with-di)
2. [Unit Testing Services](#unit-testing-services)
3. [Testing MonoBehaviours](#testing-monobehaviours)
4. [Integration Testing](#integration-testing)
5. [Mock Implementations](#mock-implementations)
6. [Editor Testing](#editor-testing)
7. [Runtime Testing](#runtime-testing)

## Introduction to Testing with DI

One of the main benefits of dependency injection is improved testability. By depending on abstractions rather than concrete implementations, you can easily substitute real implementations with test doubles (mocks, stubs, fakes) during testing.

### Benefits of DI for Testing

- **Isolation**: Test components in isolation from their dependencies
- **Control**: Precisely control the behavior of dependencies
- **Flexibility**: Easily switch between real and test implementations
- **Predictability**: Create deterministic test environments

## Unit Testing Services

### Testing Non-MonoBehaviour Services

For regular C# classes that use constructor injection:

```csharp
// Service to test
public class ScoreCalculator
{
    private readonly IDifficultyProvider _difficultyProvider;
    
    public ScoreCalculator(IDifficultyProvider difficultyProvider)
    {
        _difficultyProvider = difficultyProvider;
    }
    
    public int CalculateScore(int basePoints)
    {
        float multiplier = _difficultyProvider.GetDifficultyMultiplier();
        return Mathf.RoundToInt(basePoints * multiplier);
    }
}

// Test class
[TestFixture]
public class ScoreCalculatorTests
{
    private class MockDifficultyProvider : IDifficultyProvider
    {
        public float MultiplierToReturn { get; set; } = 1.0f;
        
        public float GetDifficultyMultiplier()
        {
            return MultiplierToReturn;
        }
    }
    
    [Test]
    public void CalculateScore_WithDifferentMultipliers_ReturnsCorrectScore()
    {
        // Arrange
        var mockProvider = new MockDifficultyProvider { MultiplierToReturn = 2.0f };
        var calculator = new ScoreCalculator(mockProvider);
        
        // Act
        int result = calculator.CalculateScore(100);
        
        // Assert
        Assert.AreEqual(200, result);
    }
}
```

### Using a Mocking Framework

For more complex scenarios, consider using a mocking framework like NSubstitute or Moq:

```csharp
// Using NSubstitute
[Test]
public void CalculateScore_WithMockFramework_ReturnsCorrectScore()
{
    // Arrange
    var mockProvider = Substitute.For<IDifficultyProvider>();
    mockProvider.GetDifficultyMultiplier().Returns(2.0f);
    
    var calculator = new ScoreCalculator(mockProvider);
    
    // Act
    int result = calculator.CalculateScore(100);
    
    // Assert
    Assert.AreEqual(200, result);
    
    // Verify the method was called
    mockProvider.Received().GetDifficultyMultiplier();
}
```

## Testing MonoBehaviours

Testing MonoBehaviours that use field injection requires a different approach:

### Manual Injection for Tests

```csharp
// MonoBehaviour to test
public class PlayerHealth : MonoBehaviour
{
    [Inject] private IDamageCalculator _damageCalculator;
    
    public int CurrentHealth { get; private set; } = 100;
    
    public void TakeDamage(int rawDamage)
    {
        int actualDamage = _damageCalculator.CalculateDamage(rawDamage);
        CurrentHealth = Mathf.Max(0, CurrentHealth - actualDamage);
    }
}

// Test class
[TestFixture]
public class PlayerHealthTests
{
    private class MockDamageCalculator : IDamageCalculator
    {
        public int DamageToReturn { get; set; }
        
        public int CalculateDamage(int rawDamage)
        {
            return DamageToReturn;
        }
    }
    
    [Test]
    public void TakeDamage_WithCalculatedDamage_ReducesHealthCorrectly()
    {
        // Arrange
        var gameObject = new GameObject();
        var playerHealth = gameObject.AddComponent<PlayerHealth>();
        
        var mockCalculator = new MockDamageCalculator { DamageToReturn = 25 };
        
        // Manually inject the mock
        var fieldInfo = typeof(PlayerHealth).GetField("_damageCalculator", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        fieldInfo.SetValue(playerHealth, mockCalculator);
        
        // Act
        playerHealth.TakeDamage(50); // Raw damage value (will be converted to 25)
        
        // Assert
        Assert.AreEqual(75, playerHealth.CurrentHealth);
        
        // Cleanup
        Object.DestroyImmediate(gameObject);
    }
}
```

### Using a Test Container

For more complex scenarios, you can create a test container:

```csharp
[Test]
public void TakeDamage_UsingTestContainer_ReducesHealthCorrectly()
{
    // Arrange
    var gameObject = new GameObject();
    var playerHealth = gameObject.AddComponent<PlayerHealth>();
    
    var mockCalculator = new MockDamageCalculator { DamageToReturn = 25 };
    
    // Create a test container
    var container = new Container();
    container.Bind<IDamageCalculator>().FromInstance(mockCalculator);
    
    // Inject dependencies using the container
    InjectionHelper.InjectInto(container, playerHealth);
    
    // Act
    playerHealth.TakeDamage(50);
    
    // Assert
    Assert.AreEqual(75, playerHealth.CurrentHealth);
    
    // Cleanup
    Object.DestroyImmediate(gameObject);
}
```

## Integration Testing

For testing how components work together:

### Testing with SceneContext

```csharp
[UnityTest]
public IEnumerator IntegrationTest_PlayerTakesDamage_TriggersUIUpdate()
{
    // Load a test scene with SceneContext
    SceneManager.LoadScene("TestScene");
    yield return null;
    
    // Find the components
    var player = GameObject.FindObjectOfType<PlayerController>();
    var healthUI = GameObject.FindObjectOfType<HealthUI>();
    
    // Initial state
    Assert.AreEqual(100, player.Health);
    Assert.AreEqual("100", healthUI.HealthText.text);
    
    // Act
    player.TakeDamage(25);
    
    // Wait a frame for UI update
    yield return null;
    
    // Assert
    Assert.AreEqual(75, player.Health);
    Assert.AreEqual("75", healthUI.HealthText.text);
}
```

### Testing with Test Installers

Create special installers for testing:

```csharp
[CreateAssetMenu(fileName = "TestInstaller", menuName = "Installers/TestInstaller")]
public class TestInstaller : ScriptableObjectInstaller
{
    public override void InstallBindings(IContainer container)
    {
        // Bind mock implementations for testing
        container.Bind<IEnemyAI>().To<PredictableEnemyAI>().AsSingleton();
        container.Bind<IRandomService>().To<DeterministicRandomService>().AsSingleton();
        container.Bind<ITimeProvider>().To<MockTimeProvider>().AsSingleton();
    }
}
```

Then use it in your test scene's SceneContext.

## Mock Implementations

### Creating Effective Mocks

Good mock implementations for testing:

```csharp
// Deterministic random service for testing
public class DeterministicRandomService : IRandomService
{
    private Queue<float> _valuesToReturn = new Queue<float>();
    
    public void QueueValues(params float[] values)
    {
        foreach (var value in values)
        {
            _valuesToReturn.Enqueue(value);
        }
    }
    
    public float GetRandomValue()
    {
        if (_valuesToReturn.Count == 0)
        {
            throw new InvalidOperationException("No more random values queued");
        }
        
        return _valuesToReturn.Dequeue();
    }
    
    public int GetRandomInt(int min, int max)
    {
        return Mathf.FloorToInt(GetRandomValue() * (max - min) + min);
    }
}

// Mock time provider for testing time-dependent code
public class MockTimeProvider : ITimeProvider
{
    public float CurrentTime { get; set; } = 0f;
    public float DeltaTime { get; set; } = 0.016f; // ~60fps
    
    public float GetTime()
    {
        return CurrentTime;
    }
    
    public float GetDeltaTime()
    {
        return DeltaTime;
    }
    
    public void AdvanceTime(float seconds)
    {
        CurrentTime += seconds;
    }
}
```

### Using Mock Implementations

```csharp
[Test]
public void EnemyMovement_WithDeterministicRandom_FollowsExpectedPath()
{
    // Arrange
    var gameObject = new GameObject();
    var enemy = gameObject.AddComponent<EnemyMovement>();
    
    var mockRandom = new DeterministicRandomService();
    mockRandom.QueueValues(0.1f, 0.5f, 0.9f); // Predetermined "random" values
    
    var mockTime = new MockTimeProvider();
    
    // Inject mocks
    InjectMocks(enemy, mockRandom, mockTime);
    
    // Initial position
    enemy.transform.position = Vector3.zero;
    
    // Act - simulate several frames
    enemy.Update(); // Uses first random value
    mockTime.AdvanceTime(0.1f);
    
    enemy.Update(); // Uses second random value
    mockTime.AdvanceTime(0.1f);
    
    enemy.Update(); // Uses third random value
    
    // Assert - check the enemy followed the expected path
    // The exact assertions would depend on how your enemy movement uses random values
    
    // Cleanup
    Object.DestroyImmediate(gameObject);
}

private void InjectMocks(EnemyMovement enemy, IRandomService randomService, ITimeProvider timeProvider)
{
    // Manual injection for testing
    typeof(EnemyMovement)
        .GetField("_randomService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        .SetValue(enemy, randomService);
        
    typeof(EnemyMovement)
        .GetField("_timeProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        .SetValue(enemy, timeProvider);
}
```

## Editor Testing

### Using EditorMockInstallers

UnityInject supports special installers that run only in the editor:

```csharp
// Place this in Resources/Installers/EditorMock
[CreateAssetMenu(fileName = "EditorMockInstaller", menuName = "Installers/EditorMockInstaller")]
public class EditorMockInstaller : ScriptableObjectInstaller
{
    public override void InstallBindings(IContainer container)
    {
        // Override production bindings with mocks for editor testing
        container.Bind<INetworkService>().To<MockNetworkService>().AsSingleton();
        container.Bind<IAnalyticsService>().To<NoOpAnalyticsService>().AsSingleton();
        container.Bind<IAdsService>().To<MockAdsService>().AsSingleton();
    }
}
```

This allows you to test your game in the editor without connecting to real services.

### Editor Tools for Testing

Create editor tools to help with testing:

```csharp
#if UNITY_EDITOR
[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        var gameManager = (GameManager)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Testing Tools", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Simulate Level Complete"))
        {
            gameManager.SimulateLevelComplete();
        }
        
        if (GUILayout.Button("Simulate Player Death"))
        {
            gameManager.SimulatePlayerDeath();
        }
    }
}
#endif
```

## Runtime Testing

### In-Game Test Suite

For complex games, consider creating an in-game test suite:

```csharp
public class RuntimeTestSuite : MonoBehaviour
{
    [Inject] private IContainer _container;
    
    [SerializeField] private bool runTestsOnStart = false;
    
    private void Start()
    {
        if (runTestsOnStart)
        {
            RunAllTests();
        }
    }
    
    public void RunAllTests()
    {
        StartCoroutine(RunTestsCoroutine());
    }
    
    private IEnumerator RunTestsCoroutine()
    {
        Debug.Log("Starting runtime tests...");
        
        yield return StartCoroutine(TestEnemySpawning());
        yield return StartCoroutine(TestPlayerMovement());
        yield return StartCoroutine(TestCombatSystem());
        
        Debug.Log("All runtime tests completed.");
    }
    
    private IEnumerator TestEnemySpawning()
    {
        Debug.Log("Testing enemy spawning...");
        
        // Get the service to test
        var spawner = _container.Resolve<IEnemySpawner>();
        
        // Clear existing enemies
        spawner.ClearAllEnemies();
        yield return null;
        
        // Test spawning
        spawner.SpawnEnemy(EnemyType.Basic, Vector3.zero);
        yield return new WaitForSeconds(0.5f);
        
        // Verify
        var enemies = GameObject.FindObjectsOfType<Enemy>();
        if (enemies.Length != 1)
        {
            Debug.LogError($"Expected 1 enemy, found {enemies.Length}");
        }
        else
        {
            Debug.Log("Enemy spawning test passed.");
        }
    }
    
    // Additional test methods...
}
```

### Debugging Aids

Create components to help debug dependency injection issues:

```csharp
public class DIDebugger : MonoBehaviour
{
    [Inject] private IContainer _container;
    
    [SerializeField] private bool logBindingsOnStart = false;
    
    private void Start()
    {
        if (logBindingsOnStart)
        {
            LogAvailableBindings();
        }
    }
    
    public void LogAvailableBindings()
    {
        // This would require extending Container to expose bindings
        Debug.Log("Available bindings in container:");
        
        // Example implementation (would need to be adapted to your Container implementation)
        /*
        var bindingDict = ((Container)_container).GetInternalBindings();
        foreach (var kvp in bindingDict)
        {
            var serviceType = kvp.Key;
            var bindingInfo = kvp.Value;
            
            string implementationInfo;
            if (bindingInfo.Instance != null)
            {
                implementationInfo = $"Instance of {bindingInfo.Instance.GetType().Name}";
            }
            else if (bindingInfo.ImplementationType != null)
            {
                implementationInfo = $"Type {bindingInfo.ImplementationType.Name}";
            }
            else if (bindingInfo.Factory != null)
            {
                implementationInfo = "Factory method";
            }
            else
            {
                implementationInfo = "Unknown binding";
            }
            
            Debug.Log($"  {serviceType.Name} -> {implementationInfo} ({bindingInfo.Lifetime})");
        }
        */
    }
    
    public void TestResolve<T>()
    {
        try
        {
            var instance = _container.Resolve<T>();
            Debug.Log($"Successfully resolved {typeof(T).Name}: {instance}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to resolve {typeof(T).Name}: {ex.Message}");
        }
    }
}
```

## Testing Best Practices Summary

1. **Design for testability** - Use interfaces and dependency injection consistently.
2. **Create specialized mock implementations** - Make deterministic versions of randomized or time-dependent services.
3. **Use manual injection for unit tests** - Directly set dependencies for isolated testing.
4. **Create test installers** - Configure special containers for testing.
5. **Test at multiple levels** - Unit test individual components, integration test how they work together.
6. **Use editor-only mocks** - Override production services with test versions in the editor.
7. **Add debugging tools** - Create utilities to inspect the DI container at runtime.

By following these practices, you can effectively test code that uses the UnityInject dependency injection system, ensuring your game is robust and reliable.
