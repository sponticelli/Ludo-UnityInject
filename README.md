# UnityInject

A lightweight, flexible dependency injection framework for Unity projects.

## Installation

### Install as a Unity Package from GitHub

1. Open your Unity project
2. Go to Window > Package Manager
3. Click the "+" button in the top-left corner
4. Select "Add package from git URL..."
5. Enter the following URL:
   ```
   https://github.com/sponticelli/Ludo-UnityInject.git
   ```
6. Click "Add"

### Dependencies

UnityInject requires the following Unity package:

- **Unity Editor Coroutines** (`com.unity.editorcoroutines`): Used for the editor tools

To install this dependency:
1. Open the Package Manager (Window > Package Manager)
2. Select "Packages: Unity Registry" from the dropdown
3. Find "Editor Coroutines" in the list
4. Click "Install"

## Introduction

UnityInject is a dependency injection framework designed specifically for Unity projects. It provides a clean, type-safe way to manage dependencies in your game architecture, making your code more modular, testable, and maintainable.

Key features:
- **Simple API**: Easy-to-use fluent API for registering and resolving dependencies
- **Hierarchical Containers**: Support for parent-child container relationships
- **Lifetime Management**: Control instance lifetime with transient or singleton scopes
- **Unity Integration**: Seamless integration with Unity's component system
- **Scene Context**: Automatic dependency injection for MonoBehaviours in your scenes
- **GameObject Context**: Isolated dependency scope for prefabs and their hierarchies
- **Editor Tools**: Visual debugging of container bindings

## Getting Started

Check out the [Documentation](Documentation/) for detailed guides on:

- Basic Setup and Configuration
- Registering Dependencies
- Injecting Dependencies
- Working with Scenes and Prefabs
- Advanced Usage Patterns

## Example Usage

```csharp
// Register dependencies in an installer
public class GameInstaller : ScriptableObjectInstaller
{
    public override void InstallBindings(IContainer container)
    {
        // Register a singleton service
        container.Bind<IGameStateManager>().To<GameStateManager>().AsSingleton();
        
        // Register a transient service
        container.Bind<IEnemyFactory>().To<EnemyFactory>().AsTransient();
    }
}

// Inject dependencies into your MonoBehaviours
public class PlayerController : MonoBehaviour
{
    [Inject] private IGameStateManager _gameStateManager;
    [Inject] private IInputService _inputService;
    
    // Use injected dependencies...
}
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.