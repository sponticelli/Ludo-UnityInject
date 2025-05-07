using UnityEngine;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Base class for installers that configure container bindings via ScriptableObjects.
    /// </summary>
    public abstract class ScriptableObjectInstaller : ScriptableObject
    {
        [SerializeField] private bool debugLog = false;
        
        
        /// <summary>
        /// Override this method to configure bindings for the container.
        /// </summary>
        /// <param name="container">The container to configure.</param>
        public abstract void InstallBindings(IContainer container);
        
        /// <summary>
        /// Helper method to instantiate a MonoBehaviour prefab, make it persistent,
        /// and bind it as a singleton instance for the specified service type.
        /// Checks if the service is already registered before proceeding.
        /// </summary>
        /// <typeparam name="TService">The interface or service type to bind.</typeparam>
        /// <typeparam name="TComponent">The concrete MonoBehaviour type of the prefab component, which must implement TService.</typeparam>
        /// <param name="container">The container to bind into.</param>
        /// <param name="prefabComponent">The prefab containing the TComponent.</param>
        /// <returns>True if the binding was successful, false if prefab was null, component was missing, or binding failed.</returns>
        protected virtual bool BindPersistentComponent<TService, TComponent>(IContainer container, TComponent prefabComponent)
            where TService : class // Ensures TService is a reference type (interface or class)
            where TComponent : MonoBehaviour, TService // Ensures TComponent is a MonoBehaviour AND implements TService
        {
            // Check if prefab is assigned
            if (prefabComponent == null)
            {
                Debug.LogError($"[{GetType().Name}] Prefab for {typeof(TComponent).Name} is null. Cannot bind {typeof(TService).Name}.");
                return false;
            }

            // Check if already registered
            if (container.CanResolve<TService>())
            {
                Debug.LogWarning($"[{GetType().Name}] Service {typeof(TService).Name} is already registered. Skipping instantiation and binding for prefab {prefabComponent.name}.");
                return true; // Indicate skipped but not an error state
            }

            // Instantiate
            if (debugLog) Debug.Log($"[{GetType().Name}] Instantiating persistent singleton from prefab {prefabComponent.name} for service {typeof(TService).Name}.");
            TComponent instance = Instantiate(prefabComponent);
            instance.gameObject.name = $"{typeof(TService).Name} (Singleton)";

            if (instance == null)
            {
                 Debug.LogError($"[{GetType().Name}] Failed to instantiate prefab {prefabComponent.name} for service {typeof(TService).Name}.");
                 return false;
            }
            
            MonoBehaviourInjector.InjectInto(container, instance);

            // Make persistent
            DontDestroyOnLoad(instance.gameObject);
            if (debugLog) Debug.Log($"[{GetType().Name}] Applied DontDestroyOnLoad to {instance.gameObject.name}.");
            
            // Bind the instance
            container.Bind<TService>().FromInstance(instance); // instance is implicitly convertible to TService
            if (debugLog) Debug.Log($"[{GetType().Name}] Bound instance {instance.name} to service {typeof(TService).Name}.");

            return true;
        }
    }
}