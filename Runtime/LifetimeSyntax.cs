using System;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Internal implementation of the lifetime syntax helper.
    /// </summary>
    internal class LifetimeSyntax : ILifetimeSyntax
    {
        protected readonly BindingInfo _bindingInfo;

        internal LifetimeSyntax(BindingInfo bindingInfo)
        {
            _bindingInfo = bindingInfo;
            // Apply default lifetime if not already set (e.g., by FromInstance)
            if (_bindingInfo.Lifetime == default && _bindingInfo.Instance == null) // Check Instance == null too
            {
                _bindingInfo.Lifetime = Lifetime.Transient; // Default to Transient
            }
        }

        public void AsSingleton()
        {
            if (_bindingInfo.Instance != null)
                throw new InvalidOperationException(
                    "Cannot change lifetime to Singleton after using 'FromInstance'. It's already Singleton.");

            _bindingInfo.Lifetime = Lifetime.Singleton;
        }

        public void AsTransient()
        {
            if (_bindingInfo.Instance != null)
                throw new InvalidOperationException(
                    "Cannot change lifetime to Transient after using 'FromInstance'. It's fixed as Singleton.");

            _bindingInfo.Lifetime = Lifetime.Transient;
        }
    }
}