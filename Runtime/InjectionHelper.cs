using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Internal helper class for reflection-based injection operations.
    /// </summary>
    internal static class InjectionHelper
    {
        /// <summary>
        /// Cache for reflection data to avoid repeated reflection operations.
        /// </summary>
        private static readonly Dictionary<Type, InjectionPoints> InjectionCache =
            new Dictionary<Type, InjectionPoints>();

        /// <summary>
        /// Structure to cache reflection data for a type.
        /// </summary>
        internal class InjectionPoints
        {
            // Store the attribute instance along with the MemberInfo
            public List<Tuple<FieldInfo, InjectAttribute>> Fields; // List of Tuples
            public List<Tuple<PropertyInfo, InjectAttribute>> Properties; // List of Tuples
            public List<Tuple<MethodInfo, InjectAttribute>> Methods; // List of Tuples
        }

        /// <summary>
        /// Injects dependencies into an object using the provided container.
        /// </summary>
        /// <param name="container">The container to resolve dependencies from.</param>
        /// <param name="target">The object to inject into.</param>
        /// <returns>True if any dependencies were injected.</returns>
        public static bool InjectInto(IContainer container, object target)
        {
            if (container == null || target == null) return false;

            Type targetType = target.GetType();
            bool injectedAny = false;

            // Get or create cached injection points, now including the attribute instance
            if (!InjectionCache.TryGetValue(targetType, out var injectionPoints))
            {
                // --- Cache population logic needs to store the attribute ---
                injectionPoints = new InjectionPoints
                {
                    Fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(f => new { Field = f, Attr = CustomAttributeExtensions.GetCustomAttribute<InjectAttribute>((MemberInfo)f) })
                        .Where(x => x.Attr != null)
                        .Select(x => Tuple.Create(x.Field, x.Attr)) // Store FieldInfo and InjectAttribute
                        .ToList(),

                    Properties = targetType
                        .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(p => p.CanWrite) // Ensure property is writable
                        .Select(p => new { Prop = p, Attr = p.GetCustomAttribute<InjectAttribute>() })
                        .Where(x => x.Attr != null)
                        .Select(x => Tuple.Create(x.Prop, x.Attr)) // Store PropertyInfo and InjectAttribute
                        .ToList(),

                    Methods = targetType
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(m => new { Method = m, Attr = m.GetCustomAttribute<InjectAttribute>() })
                        .Where(x => x.Attr != null)
                        .Select(x => Tuple.Create(x.Method, x.Attr)) // Store MethodInfo and InjectAttribute
                        .ToList()
                };
                InjectionCache[targetType] = injectionPoints;
            }

            // Inject fields
            foreach (var fieldTuple in injectionPoints.Fields)
            {
                var field = fieldTuple.Item1;
                var injectAttribute = fieldTuple.Item2; // Get the attribute instance

                try
                {
                    if (container.CanResolve(field.FieldType))
                    {
                        object dependency = container.Resolve(field.FieldType);
                        field.SetValue(target, dependency);
                        injectedAny = true;
                    }
                    // --- Check Optional property ---
                    else if (!injectAttribute.Optional) // Only log error if mandatory
                    {
                        Debug.LogError(
                            $"[UnityInject] Could not resolve mandatory dependency of type {field.FieldType.Name} for field {field.Name} in {targetType.Name}");
                    }
                    // If Optional is true and CanResolve is false, do nothing.
                }
                catch (Exception ex) // Catch potential SetValue errors or further resolution errors
                {
                    Debug.LogError($"[UnityInject] Error injecting field {field.Name} in {targetType.Name}: {ex}");
                }
            }

            // Inject properties
            foreach (var propTuple in injectionPoints.Properties)
            {
                var property = propTuple.Item1;
                var injectAttribute = propTuple.Item2; // Get the attribute instance

                try
                {
                    if (container.CanResolve(property.PropertyType))
                    {
                        object dependency = container.Resolve(property.PropertyType);
                        property.SetValue(target, dependency);
                        injectedAny = true;
                    }
                    // --- Check Optional property ---
                    else if (!injectAttribute.Optional) // Only log error if mandatory
                    {
                        Debug.LogError(
                            $"[UnityInject] Could not resolve mandatory dependency of type {property.PropertyType.Name} for property {property.Name} in {targetType.Name}");
                    }
                    // If Optional is true and CanResolve is false, do nothing.
                }
                catch (Exception ex) // Catch potential SetValue errors or further resolution errors
                {
                    Debug.LogError(
                        $"[UnityInject] Error injecting property {property.Name} in {targetType.Name}: {ex}");
                }
            }

            // Inject methods
            foreach (var methodTuple in injectionPoints.Methods)
            {
                var method = methodTuple.Item1;
                var injectAttribute = methodTuple.Item2; // Get the attribute instance for the method

                try
                {
                    var parameters = method.GetParameters();
                    var arguments = new object[parameters.Length];
                    bool canInvokeMethod = true; // Assume true initially

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (container.CanResolve(parameters[i].ParameterType))
                        {
                            arguments[i] = container.Resolve(parameters[i].ParameterType);
                        }
                        else
                        {
                            // A parameter cannot be resolved.
                            // Check if the *entire method injection* is optional.
                            // We don't support optional parameters individually without extending the attribute further.
                            if (!injectAttribute.Optional)
                            {
                                // Method is mandatory, log error and prevent invocation
                                Debug.LogError(
                                    $"[UnityInject] Could not resolve mandatory parameter '{parameters[i].Name}' of type {parameters[i].ParameterType.Name} for method {method.Name} in {targetType.Name}. Skipping method injection.");
                                canInvokeMethod = false;
                            }
                            else
                            {
                                // Method is optional, just prevent invocation silently
                                canInvokeMethod = false;
                            }

                            // Break check for this method once an unresolvable parameter is found
                            // and we know if we need to log or not.
                            break;
                        }
                    }

                    // Invoke method only if all parameters were resolved, OR if it was optional and resolution failed
                    if (canInvokeMethod)
                    {
                        method.Invoke(target, arguments);
                        injectedAny = true;
                    }
                }
                catch (Exception ex) // Catch potential Invoke errors or further resolution errors
                {
                    Debug.LogError($"[UnityInject] Error injecting method {method.Name} in {targetType.Name}: {ex}");
                }
            }

            return injectedAny;
        }
    }
}