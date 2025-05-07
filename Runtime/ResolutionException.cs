using System;

namespace Ludo.UnityInject
{
    #region Interfaces

    #endregion

    #region Supporting Types

    /// <summary>
    /// Exception thrown when a dependency resolution fails within the container.
    /// </summary>
    public class ResolutionException : Exception
    {
        public ResolutionException(string message) : base(message)
        {
        }

        public ResolutionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    #endregion

    #region Container Implementation

    #endregion

    #region Fluent Syntax Implementations

    #endregion
}