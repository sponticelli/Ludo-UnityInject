using UnityEngine;
using System;

namespace Ludo.UnityInject
{
    /// <summary>
    /// MonoBehaviour that manages the dependency injection for a GameObject and its children.
    /// This component should be added to the root of a prefab that requires its own dependency injection scope.
    /// It initializes a child container from the parent container (usually the SceneContext container),
    /// runs any specified installers, and injects dependencies into all MonoBehaviours in the GameObject's hierarchy.
    /// </summary>
    /// <remarks>
    /// The GameObjectContext is responsible for:
    /// <list type="bullet">
    /// <item><description>Creating a child container from the parent container (usually the scene container)</description></item>
    /// <item><description>Running object-specific installers to configure the container</description></item>
    /// <item><description>Allowing components to register bindings via IObjectContextBinder</description></item>
    /// <item><description>Injecting dependencies into all MonoBehaviours in its hierarchy</description></item>
    /// <item><description>Disposing the object container when the GameObject is destroyed</description></item>
    /// </list>
    ///
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
    ///
    /// // Example of instantiating a prefab with GameObjectContext:
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
    [DefaultExecutionOrder(ExecutionOrder.GameObjectContext)] // Run early, but after SceneContext
    public class GameObjectContext : MonoBehaviour
    {
        [Tooltip("Optional installers specific to this prefab instance's scope")]
        [SerializeField] private ScriptableObjectInstaller[] objectInstallers;

        [Inject] // Inject the parent container (usually SceneContainer)
        private IContainer _parentContainer;

        private IContainer _objectContainer; // This instance's specific container

        private void Start()
        {
            InitializeObjectContainer();
            // Injection for children MUST happen after container is ready
            // but still within Awake if possible, before children's Start methods run.
            InjectGameObjectDependencies();
        }

        private void InitializeObjectContainer()
        {
            // Check if parent was injected (it should be if instantiated via DI)
            if (_parentContainer == null)
            {
                Debug.LogError(
                    $"[GameObjectContext] Parent container was not injected into {gameObject.name}! Falling back to RootContainer. Ensure this prefab is instantiated via container.InstantiatePrefab() or similar.",
                    this);
                // Fallback: Use RootContainer as parent if injection failed
                _parentContainer = ProjectInitializer.RootContainer;
                if (_parentContainer == null)
                {
                    Debug.LogError(
                        $"[GameObjectContext] Critical Error: RootContainer is also null! Cannot create object scope for {gameObject.name}.",
                        this);
                    this.enabled = false; // Disable context if no container possible
                    return;
                }
            }

            try
            {
                // Create the child container for this GameObject scope
                _objectContainer = _parentContainer.CreateChildContainer();
                Debug.Log($"[GameObjectContext] Object container created for {gameObject.name}", this);

                // --- Self-Registration & Child Component Registration ---
                // Allow root components (e.g., EnemyController) to register things
                var binders = GetComponents<IObjectContextBinder>(); // Get components on this SAME GameObject
                foreach (var binder in binders)
                {
                    try
                    {
                        binder.RegisterBindings(_objectContainer);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"Error during self-registration for {binder.GetType().Name} on {gameObject.name}: {ex}",
                            this);
                    }
                }

                // --- Run Installers ---
                RunObjectInstallers();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameObjectContext] Failed to initialize object container for {gameObject.name}: {ex}",
                    this);
                _objectContainer?.Dispose();
                _objectContainer = null;
                this.enabled = false; // Disable if setup failed
            }
        }

        private void RunObjectInstallers()
        {
            if (_objectContainer == null || objectInstallers == null || objectInstallers.Length == 0)
            {
                return;
            }

            foreach (var installer in objectInstallers)
            {
                if (installer == null) continue;
                try
                {
                    // Debug.Log($"[GameObjectContext] Running object installer: {installer.name} on {gameObject.name}", this);
                    installer.InstallBindings(_objectContainer);
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[GameObjectContext] Error in object installer {installer.name} on {gameObject.name}: {ex}",
                        this);
                }
            }
        }

        private void InjectGameObjectDependencies()
        {
            if (_objectContainer == null) return; // Don't inject if container failed

            // Inject into all MonoBehaviours on this GameObject and its children
            // Note: This uses the shared helper, which uses caching.
            var components = GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var component in components)
            {
                // Avoid re-injecting self with wrong container? Or let InjectionHelper handle it?
                // Let helper handle injection based on [Inject] attributes.
                if (component == this) continue; // Don't try to inject into self again here

                try
                {
                    InjectionHelper.InjectInto(_objectContainer, component);
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[GameObjectContext] Failed to inject into {component.GetType().Name} on {component.gameObject.name}: {ex}",
                        component);
                }
            }

            Debug.Log($"[GameObjectContext] Injection complete for {gameObject.name} hierarchy.", this);
        }

        /// <summary>
        /// Gets the container for this GameObject hierarchy.
        /// This can be used to manually resolve dependencies or instantiate prefabs within this scope.
        /// </summary>
        /// <returns>The object-specific dependency injection container.</returns>
        /// <remarks>
        /// Use this method with caution. It's generally better to inject dependencies directly
        /// rather than accessing the container to resolve them manually.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example of a component that needs to access the object container
        /// public class NestedContextManager : MonoBehaviour
        /// {
        ///     private GameObjectContext _parentContext;
        ///
        ///     private void Awake()
        ///     {
        ///         _parentContext = GetComponentInParent&lt;GameObjectContext&gt;();
        ///         var container = _parentContext.GetObjectContainer();
        ///
        ///         // Use the container...
        ///     }
        /// }
        /// </code>
        /// </example>
        public IContainer GetObjectContainer() => _objectContainer;

        private void OnDestroy()
        {
            // Dispose the object-specific container and its singletons
            _objectContainer?.Dispose();
            // Debug.Log($"[GameObjectContext] Disposed object container for {gameObject.name}", this);
        }
    }
}