using System;
using UnityEngine;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Extension methods for GameObject instantiation with dependency injection.
    /// </summary>
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Instantiates a prefab and injects dependencies into its components.
        /// </summary>
        /// <param name="container">The container to resolve dependencies from.</param>
        /// <param name="prefab">The prefab to instantiate.</param>
        /// <param name="position">The position in world space.</param>
        /// <param name="rotation">The rotation in world space.</param>
        /// <returns>The instantiated GameObject with dependencies injected.</returns>
        public static GameObject InstantiatePrefab(this IContainer container, GameObject prefab,
            Vector3 position = default, Quaternion rotation = default)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));

            // Instantiate the prefab
            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            // Use the provided container directly
            MonoBehaviourInjector.InjectGameObject(container, instance);

            return instance;
        }
    }
}