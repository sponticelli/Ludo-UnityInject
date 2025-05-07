using UnityEngine;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Example of a concrete installer for global services.
    /// Create assets of this type in Resources/Installers/Global.
    /// </summary>
    [CreateAssetMenu(fileName = "GlobalServiceInstaller", menuName = "UnityInject/Global Service Installer")]
    public class GlobalServiceInstaller : ScriptableObjectInstaller
    {
        public override void InstallBindings(IContainer container)
        {
            // Example bindings:
            // container.Bind<IAudioService>().To<AudioService>().AsSingleton();
            // container.Bind<IInputManager>().To<InputManager>().AsSingleton();
            // container.Bind<ISaveGameService>().To<SaveGameService>().AsSingleton();
        }
    }
}