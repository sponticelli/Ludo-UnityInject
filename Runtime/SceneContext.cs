using System;
using UnityEngine;

namespace Ludo.UnityInject
{
    /// <summary>
    /// MonoBehaviour that manages the dependency injection for a scene.
    /// Add this component to a GameObject in each scene that requires dependency injection.
    /// </summary>
    /// <remarks>
    /// The SceneContext is responsible for:
    /// <list type="bullet">
    /// <item><description>Creating a child container from the root container</description></item>
    /// <item><description>Running scene-specific installers to configure the container</description></item>
    /// <item><description>Scanning the entire scene for MonoBehaviours and injecting dependencies</description></item>
    /// <item><description>Disposing the scene container when the scene is unloaded</description></item>
    /// </list>
    ///
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
    [DefaultExecutionOrder(ExecutionOrder.SceneContext)] // Ensure this runs before other scripts
    public class SceneContext : MonoBehaviour
    {
        [Tooltip("Scene-specific installer assets that configure the scene's container")]
        [SerializeField]
        private ScriptableObjectInstaller[] sceneInstallers;

        /// <summary>
        /// The container for this scene, inheriting from the global root container.
        /// </summary>
        private IContainer _sceneContainer;

        /// <summary>
        /// Gets the container for this scene.
        /// This can be used to manually resolve dependencies or instantiate prefabs.
        /// </summary>
        /// <returns>The scene's dependency injection container.</returns>
        /// <example>
        /// <code>
        /// // Get the scene container
        /// var sceneContext = FindObjectOfType&lt;SceneContext&gt;();
        /// var container = sceneContext.GetSceneContainer();
        ///
        /// // Use the container to instantiate a prefab with dependencies
        /// var instance = container.InstantiatePrefab(prefab, position, rotation);
        /// </code>
        /// </example>
        public IContainer GetSceneContainer() => _sceneContainer;

        private void Awake()
        {
            InitializeContainer();
            InjectSceneDependencies();
        }

        private void InitializeContainer()
        {
            try
            {
                // Get the root container
                if (ProjectInitializer.RootContainer == null)
                {
                    throw new InvalidOperationException(
                        "Root container is null. Make sure ProjectInitializer is working correctly.");
                }

                // Create a child container for this scene
                _sceneContainer = ProjectInitializer.RootContainer.CreateChildContainer();
                // Debug.Log($"[UnityInject] Scene container created for {gameObject.scene.name}");

                // Run scene-specific installers
                RunSceneInstallers();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityInject] Failed to initialize scene container: {ex}");
                _sceneContainer?.Dispose();
                _sceneContainer = null;
                throw;
            }
        }

        private void RunSceneInstallers()
        {
            if (sceneInstallers == null || sceneInstallers.Length == 0)
            {
                return;
            }

            foreach (var installer in sceneInstallers)
            {
                if (installer == null) continue;

                try
                {
                    //Debug.Log($"[UnityInject] Running scene installer: {installer.name}");
                    installer.InstallBindings(_sceneContainer);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnityInject] Error in scene installer {installer.name}: {ex}");
                    // Continue with other installers despite errors
                }
            }
        }

        private void InjectSceneDependencies()
        {
            if (_sceneContainer == null)
            {
                Debug.LogError("[UnityInject] Cannot inject scene dependencies: container is null");
                return;
            }

            try
            {
                // Find all MonoBehaviours in the scene
                var monoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int injectedCount = 0;

                foreach (var monoBehaviour in monoBehaviours)
                {
                    // Skip null objects and this SceneContext itself
                    if (monoBehaviour == null || monoBehaviour == this) continue;

                    // Only process objects in this scene
                    if (monoBehaviour.gameObject.scene != gameObject.scene) continue;

                    try
                    {
                        if (InjectInto(monoBehaviour))
                        {
                            injectedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[UnityInject] Failed to inject into {monoBehaviour.GetType().Name} on {monoBehaviour.gameObject.name}: {ex}");
                    }
                }

                // Debug.Log($"[UnityInject] Injected dependencies into {injectedCount} objects in scene {gameObject.scene.name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityInject] Failed to inject scene dependencies: {ex}");
            }
        }

        /// <summary>
        /// Injects dependencies into the specified object using the scene container.
        /// This is useful for manually injecting dependencies into objects that were created
        /// outside the normal dependency injection flow.
        /// </summary>
        /// <param name="target">The object to inject dependencies into.</param>
        /// <returns>True if any dependencies were injected, false otherwise.</returns>
        /// <remarks>
        /// This method uses reflection to find fields and properties marked with the [Inject] attribute
        /// and resolves their values from the scene container.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Create an object that needs dependencies
        /// var myObject = new MyClass();
        ///
        /// // Get the SceneContext
        /// var sceneContext = FindObjectOfType&lt;SceneContext&gt;();
        ///
        /// // Manually inject dependencies
        /// sceneContext.InjectInto(myObject);
        /// </code>
        /// </example>
        public bool InjectInto(object target)
        {
            if (target == null) return false;
            if (_sceneContainer == null)
            {
                Debug.LogError("[UnityInject] Cannot inject: container is null");
                return false;
            }
            return InjectionHelper.InjectInto(_sceneContainer, target);
        }

        /// <summary>
        /// Injects dependencies into a newly instantiated GameObject and its components.
        /// This is useful for objects instantiated via GameObject.Instantiate() rather than
        /// through the container's InstantiatePrefab method.
        /// </summary>
        /// <param name="gameObject">The GameObject to inject dependencies into.</param>
        /// <remarks>
        /// This method finds all MonoBehaviours in the GameObject hierarchy and injects
        /// dependencies into each one using the scene container.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Instantiate a prefab the standard Unity way
        /// var instance = Instantiate(prefab, position, rotation);
        ///
        /// // Get the SceneContext
        /// var sceneContext = FindObjectOfType&lt;SceneContext&gt;();
        ///
        /// // Manually inject dependencies into the instance
        /// sceneContext.InjectGameObject(instance);
        /// </code>
        /// </example>
        public void InjectGameObject(GameObject gameObject)
        {
            if (gameObject == null || _sceneContainer == null) return;

            var components = gameObject.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var component in components)
            {
                try
                {
                    InjectInto(component);
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[UnityInject] Failed to inject into {component.GetType().Name} on {component.gameObject.name}: {ex}");
                }
            }
        }

        private void OnDestroy()
        {
            if (_sceneContainer != null)
            {
                // Debug.Log($"[UnityInject] Disposing scene container for {gameObject.scene.name}");
                _sceneContainer.Dispose();
                _sceneContainer = null;
            }
        }
    }
}