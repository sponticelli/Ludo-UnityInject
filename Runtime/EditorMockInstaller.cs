using UnityEngine;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Example of a concrete installer for mock services in the editor.
    /// Create assets of this type in Resources/Installers/EditorMock.
    /// </summary>
    [CreateAssetMenu(fileName = "EditorMockInstaller", menuName = "UnityInject/Editor Mock Installer")]
    public class EditorMockInstaller : ScriptableObjectInstaller
    {
        public override void InstallBindings(IContainer container)
        {
            // Example mock bindings for editor-only testing:
            // container.Bind<IApiService>().To<MockApiService>().AsSingleton();
            // container.Bind<IDatabaseService>().To<InMemoryDatabaseService>().AsSingleton();
        }
    }
}