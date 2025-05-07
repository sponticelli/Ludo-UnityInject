namespace Ludo.UnityInject
{
    /// <summary>
    /// Defines the lifetime management options for a registered service.
    /// </summary>
    public enum Lifetime
    {
        /// <summary>
        /// A new instance is created for each resolution request.
        /// </summary>
        Transient,

        /// <summary>
        /// A single instance is created and reused within the container's scope.
        /// </summary>
        Singleton
        // Future: Scoped
    }
}