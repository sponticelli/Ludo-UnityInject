using System;

namespace Ludo.UnityInject
{
    /// <summary>
    /// A lifetime syntax implementation used for FromInstance where the lifetime is fixed.
    /// </summary>
    internal class FixedLifetimeSyntax : ILifetimeSyntax
    {
        private readonly BindingInfo _bindingInfo;

        internal FixedLifetimeSyntax(BindingInfo bindingInfo)
        {
            _bindingInfo = bindingInfo;
        }

        public void AsSingleton()
        {
            /* No-op, FromInstance implies Singleton */
        }

        public void AsTransient()
        {
            throw new InvalidOperationException(
                "Cannot change lifetime for 'FromInstance' bindings. They are always Singleton.");
        }
    }
}