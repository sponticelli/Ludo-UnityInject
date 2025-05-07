using System;
using UnityEngine;

namespace Ludo.UnityInject
{
    /// <summary>
    /// Static initializer for the global DI container.
    /// Ensures the root container is available before any scene loads.
    /// </summary>
    public static class ProjectInitializer
    {
        private static readonly string GlobalInstallersPath = "Installers/Global";
        private static readonly string EditorMockInstallersPath = "Installers/EditorMock";
        
        /// <summary>
        /// The global root container instance.
        /// </summary>
        public static IContainer RootContainer { get; private set; }

        /// <summary>
        /// Initializes the global container before any scene loads.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (RootContainer != null)
            {
                Debug.LogWarning("[UnityInject] Root container already initialized. Skipping initialization.");
                return;
            }

            try
            {
                // Create the root container
                RootContainer = new Container();
                // Debug.Log("[UnityInject] Root container created.");

                // Register the container itself as a singleton
                RootContainer.Bind<IContainer>().FromInstance(RootContainer);

                // Load and run installers
                LoadAndRunInstallers();

                // Register for application quit to dispose the container
                Application.quitting += OnApplicationQuit;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityInject] Failed to initialize root container: {ex}");
                RootContainer?.Dispose();
                RootContainer = null;
                throw;
            }
        }

        private static void LoadAndRunInstallers()
        {
            try
            {
                var globalInstallers = Resources.LoadAll<ScriptableObjectInstaller>(GlobalInstallersPath);
                // Debug.Log($"[UnityInject] Found {globalInstallers.Length} global installers.");
                foreach (var installer in globalInstallers)
                {
                    // Debug.Log($"[UnityInject] Running global installer: {installer.name}");
                    try
                    {
                        installer.InstallBindings(RootContainer);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UnityInject] Error in global installer {installer.name}: {ex}");
                    }
                }

                // load editor mock installers if in editor (to override globals)
                if (Application.isEditor)
                {
                    var editorInstallers = Resources.LoadAll<ScriptableObjectInstaller>(EditorMockInstallersPath);
                    foreach (var installer in editorInstallers)
                    {
                        try
                        {
                            // Debug.Log($"[UnityInject] Running editor mock installer: {installer.name}");
                            installer.InstallBindings(RootContainer);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[UnityInject] Error in editor mock installer {installer.name}: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityInject] Failed to load or run installers: {ex}");
            }
        }

        private static void OnApplicationQuit()
        {
            // Debug.Log("[UnityInject] Application quitting, disposing root container.");
            RootContainer?.Dispose();
            RootContainer = null;
        }
    }
}