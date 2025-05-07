using System;
using UnityEngine;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Helper class for injecting dependencies into MonoBehaviours without requiring a SceneContext.
    /// </summary>
    public static class MonoBehaviourInjector
    {
        /// <summary>
        /// Injects dependencies into all MonoBehaviours on a GameObject and its children.
        /// </summary>
        /// <param name="container">The container to resolve dependencies from.</param>
        /// <param name="gameObject">The GameObject to inject dependencies into.</param>
        public static void InjectGameObject(IContainer container, GameObject gameObject)
        {
            if (container == null || gameObject == null) return;

            var components = gameObject.GetComponentsInChildren<MonoBehaviour>(true);
            //Debug.Log($"Injecting into {components.Length} components in {gameObject.name}");
            foreach (var component in components)
            {
                //Debug.Log($"Injecting into {component.GetType().Name} on {component.gameObject.name}");
                InjectInto(container, component);
            }
        }

        /// <summary>
        /// Injects dependencies into a specific object.
        /// </summary>
        /// <param name="container">The container to resolve dependencies from.</param>
        /// <param name="target">The object to inject dependencies into.</param>
        public static void InjectInto(IContainer container, object target)
        {
            if (container == null || target == null) return;
            Type targetType = target.GetType();
            InjectionHelper.InjectInto(container, target);
        }
    }
}