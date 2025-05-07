namespace Ludo.UnityInject
{
    /// <summary>
    /// Fluent API interface for specifying the lifetime of a binding.
    /// </summary>
    public interface ILifetimeSyntax
    {
        /// <summary>
        /// Specifies that only a single instance should be created per container (Singleton).
        /// </summary>
        void AsSingleton();

        /// <summary>
        /// Specifies that a new instance should be created every time the service is resolved (Transient).
        /// This is often the default lifetime.
        /// </summary>
        void AsTransient();

        // Future: void InSceneScope();
        // Future: void InScope(object scopeId);
    }
}