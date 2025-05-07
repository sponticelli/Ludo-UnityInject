using System;
using System.Collections;
using System.Collections.Generic;
using System.IO; // Required for Path operations
using System.Linq;
using Ludo.UnityInject; // Runtime namespace
using Unity.EditorCoroutines.Editor; // For Editor Coroutines
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ludo.UnityInject.Editor
{
    public class DiBindingsViewer : EditorWindow
    {
        [MenuItem("Window/Ludo/UnityInject/Bindings Viewer")]
        public static void ShowWindow()
        {
            DiBindingsViewer wnd = GetWindow<DiBindingsViewer>();
            wnd.titleContent = new GUIContent("DI Bindings Viewer");
            wnd.minSize = new Vector2(350, 200); // Set a minimum size
        }

        // UI Element References
        private VisualElement _rootElement;
        private Button _refreshButton;
        private Label _modeLabel;
        private Label _statusLabel;
        private ScrollView _scrollView;
        private VisualElement _bindingsContainer;

        // State
        private EditorCoroutine _refreshCoroutine = null;
        private static readonly Color _errorColor = new Color(1f, 0.6f, 0.6f);
        private static readonly Color _defaultBgColor = new Color(0.22f, 0.22f, 0.22f); // Adjust based on editor theme

        public void CreateGUI()
        {
            _rootElement = rootVisualElement;

            // --- Dynamically find UXML path relative to this script ---
            string uxmlPath = FindRelativePath("DiBindingsViewer.uxml");
            if (string.IsNullOrEmpty(uxmlPath))
            {
                _rootElement.Add(new Label("Error: Could not determine path for DiBindingsViewer.uxml relative to DiBindingsViewer.cs"));
                return;
            }
            // --- End dynamic path finding ---


            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (visualTree == null)
            {
                _rootElement.Add(new Label($"Error: Could not load UXML at resolved path: {uxmlPath}. Make sure it's in the same directory as the script."));
                return;
            }
            visualTree.CloneTree(_rootElement);

            // Query elements
            _refreshButton = _rootElement.Q<Button>("refresh-button");
            _modeLabel = _rootElement.Q<Label>("mode-label");
            _statusLabel = _rootElement.Q<Label>("status-label");
            _scrollView = _rootElement.Q<ScrollView>("bindings-scrollview");
            _bindingsContainer = _rootElement.Q<VisualElement>("bindings-container");

            // Validate elements found
            if (_refreshButton == null || _modeLabel == null || _statusLabel == null || _scrollView == null || _bindingsContainer == null)
            {
                _rootElement.Clear(); // Clear potentially broken UI
                _rootElement.Add(new Label("Error: Could not find all required elements in DiBindingsViewer.uxml. Check element names ('refresh-button', 'mode-label', etc.)."));
                return;
            }

            // Register callbacks
            _refreshButton.clicked += RefreshView;

            // --- MOVED ---
            // Initial population - Call RefreshView AFTER UI elements are queried
            RefreshView();
            // --- END MOVED ---
        }

        /// <summary>
        /// Helper to find an asset path relative to this script file.
        /// </summary>
        /// <param name="fileName">The filename of the asset (e.g., "MyWindow.uxml")</param>
        /// <returns>The asset path or null if not found.</returns>
        private string FindRelativePath(string fileName)
        {
            // Find the path of the current script asset
            var script = MonoScript.FromScriptableObject(this);
            if (script == null) return null; // Should not happen for EditorWindow

            string scriptPath = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(scriptPath)) return null;

            // Get the directory containing the script
            string scriptDirectory = Path.GetDirectoryName(scriptPath);

            // Combine directory and filename
            // Normalize path separators for Unity
            string combinedPath = Path.Combine(scriptDirectory ?? "", fileName).Replace('\\', '/');

            return combinedPath;
        }


        private void OnEnable()
        {
            // Subscribe to play mode state changes
            EditorApplication.playModeStateChanged += HandlePlayModeStateChange;

            // --- MOVED ---
            // Initial refresh is now called at the end of CreateGUI
            // RefreshView();
            // --- END MOVED ---

            // It might still be useful to refresh if the window was enabled after a domain reload
            // where CreateGUI might not be immediately called again. Let's add a check.
            // However, the safest place for the *very first* refresh is CreateGUI.
            // A refresh here handles re-enabling the window without full reload.
             if (_bindingsContainer != null) // Check if GUI has been created at least once
             {
                 RefreshView();
             }
        }

        private void OnDisable()
        {
            // Unsubscribe
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChange;
            // Stop any running coroutine when the window is disabled/closed
            StopRefreshCoroutine();
        }

        // --- HandlePlayModeStateChange, StopRefreshCoroutine, RefreshView ---
        // --- RefreshPlayModeView, DisplayContainerBindings, DetermineBoundToDescription ---
        // --- RefreshEditModeView, ScanAndSimulateInstallers, DisplaySimulatedBindings ---
        // --- DetermineSimulatedBoundToDescription, AddStatusMessage ---
        // (These methods remain the same as the previous version)
        // ... (rest of the methods as defined previously) ...

        private void HandlePlayModeStateChange(PlayModeStateChange state)
        {
            // Refresh the view whenever the play mode changes
            // Check if UI elements are still valid (can become null during domain reload)
            if (_refreshButton != null)
            {
                 RefreshView();
            } else {
                // UI likely needs recreation after domain reload/assembly recompilation
                // Force recreation on next OnGUI update might be needed, or handle here.
                // For simplicity, we assume CreateGUI handles querying again if needed.
                Debug.LogWarning("[BindingsViewer] UI Elements lost during play mode change, CreateGUI should re-acquire them.");
            }
        }

        private void StopRefreshCoroutine()
        {
            if (_refreshCoroutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        private void RefreshView()
        {
            // Ensure UI elements are valid before proceeding
            if (_bindingsContainer == null || _statusLabel == null || _modeLabel == null)
            {
                 // Don't log error here anymore, as OnEnable might call this before CreateGUI
                 // Just return silently if UI isn't ready. CreateGUI will call it when ready.
                 // Debug.LogError("[BindingsViewer] Cannot refresh, UI elements not initialized. Was CreateGUI called?");
                 return;
            }

            StopRefreshCoroutine(); // Stop previous refresh if running
            _bindingsContainer.Clear(); // Clear previous results
            _statusLabel.text = "Refreshing...";
            _statusLabel.style.backgroundColor = _defaultBgColor; // Reset status color

            // Check current editor state
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // If entering play mode, wait until actually playing
                if (EditorApplication.isPlaying)
                {
                    RefreshPlayModeView();
                }
                else
                {
                     _modeLabel.text = "Play Mode (Waiting to Enter)";
                     _statusLabel.text = "Waiting for Play Mode...";
                }
            }
            else
            {
                RefreshEditModeView();
            }
        }

        // --- Play Mode Logic ---

        private void RefreshPlayModeView()
        {
            _modeLabel.text = "Play Mode (Live)";
            _statusLabel.text = "Accessing live containers...";

            IContainer rootContainer = null;
            try {
                 rootContainer = ProjectInitializer.RootContainer;
            } catch (Exception ex) {
                 // Catch potential issues if ProjectInitializer itself failed
                 _statusLabel.text = $"Error accessing RootContainer: {ex.Message}";
                 _statusLabel.style.backgroundColor = _errorColor;
                 Debug.LogError($"[BindingsViewer] Error accessing RootContainer: {ex}");
                 return;
            }


            if (rootContainer == null)
            {
                _statusLabel.text = "Error: RootContainer not found or not initialized in Play Mode.";
                _statusLabel.style.backgroundColor = _errorColor;
                return;
            }

            // Display Root Container Bindings
            DisplayContainerBindings("Root Container", rootContainer);

            // Display Scene Container Bindings (find active SceneContext)
            SceneContext sceneContext = null;
            try {
                // Use FindObjectsOfType carefully, might be slow in complex scenes
                // Consider optimization if this becomes a bottleneck
                sceneContext = FindObjectOfType<SceneContext>();
            } catch(Exception ex) {
                 AddStatusMessage($"Error finding SceneContext: {ex.Message}", true);
            }

            if (sceneContext != null)
            {
                 IContainer sceneContainer = null;
                 try {
                     sceneContainer = sceneContext.GetSceneContainer(); // Assumes public getter exists
                 } catch (Exception ex) {
                     AddStatusMessage($"Error getting scene container from {sceneContext.gameObject.name}: {ex.Message}", true);
                 }

                 if (sceneContainer != null)
                 {
                     DisplayContainerBindings($"Scene Container ({sceneContext.gameObject.scene.name})", sceneContainer);
                 }
                 else {
                      AddStatusMessage($"Scene Context found in '{sceneContext.gameObject.scene.name}' but its container is null.", true);
                 }
            }
            else
            {
                 AddStatusMessage("No active SceneContext found in the current scene(s).", false);
            }

            _statusLabel.text = $"Refreshed (Play Mode) - {DateTime.Now:H:mm:ss}";
        }

        private void DisplayContainerBindings(string title, IContainer container)
        {
            var foldout = new Foldout { text = title };
            _bindingsContainer.Add(foldout);

            // Access internal bindings - requires InternalsVisibleTo or public accessor
            IReadOnlyDictionary<Type, BindingInfo> bindings = null;
            if (container is Container concreteContainer) // Check if it's our implementation
            {
                 try {
                     // Ensure GetInternalBindings is accessible (internal + InternalsVisibleTo)
                     bindings = concreteContainer.GetInternalBindings();
                 } catch (Exception ex) {
                     AddStatusMessage($"Error accessing bindings for {title}: {ex.Message}", true);
                     foldout.Add(new Label($"<Error accessing bindings: {ex.Message}>"));
                     return;
                 }
            } else {
                 foldout.Add(new Label($"<Cannot inspect bindings: Container is not expected type 'Container' ({container?.GetType().Name})>"));
                 return;
            }

            if (bindings == null || bindings.Count == 0)
            {
                foldout.Add(new Label("  (No bindings registered in this container)"));
                return;
            }

            // Sort bindings by registered type name for consistent display
            var sortedBindings = bindings.OrderBy(kvp => kvp.Key.Name);

            foreach (var kvp in sortedBindings)
            {
                Type registeredType = kvp.Key;
                BindingInfo info = kvp.Value;

                string boundTo = DetermineBoundToDescription(info);
                // Use Foldout for complex types? Or just label? Keep label for now.
                var label = new Label($"  • {registeredType.Name} -> {boundTo} [{info.Lifetime}]");
                label.tooltip = $"Registered: {registeredType.FullName}\nBound To: {boundTo}\nLifetime: {info.Lifetime}";
                foldout.Add(label);
            }
        }

        private string DetermineBoundToDescription(BindingInfo info)
        {
             if (info.Instance != null)
             {
                 // Avoid potential long ToString() or errors
                 return $"Instance<{info.Instance.GetType().Name}> (ID: {info.Instance.GetHashCode()})";
             }
             if (info.Factory != null)
             {
                 // Getting exact factory method name is hard, just indicate it's a factory
                 return "Factory Method";
             }
             if (info.ImplementationType != null)
             {
                 return $"Type<{info.ImplementationType.Name}>";
             }
             // Handle case where concrete type was bound directly without To()
             if (!info.RegisteredType.IsAbstract && !info.RegisteredType.IsInterface)
             {
                 return $"Type<{info.RegisteredType.Name}> (Implicit)";
             }
             return "<Unknown Binding Type>";
        }


        // --- Edit Mode Logic ---

        private void RefreshEditModeView()
        {
            _modeLabel.text = "Edit Mode (Simulated)";
            _statusLabel.text = "Scanning project for installers...";
            // Start coroutine for asset scanning and simulation
            _refreshCoroutine = EditorCoroutineUtility.StartCoroutine(ScanAndSimulateInstallers(), this);
        }

        private IEnumerator ScanAndSimulateInstallers()
        {
            var collector = new BindingCollector();
            var allInstallers = new List<ScriptableObjectInstaller>();
            int yieldCounter = 0;

            // Find all ScriptableObjectInstaller assets
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(ScriptableObjectInstaller).Name}");
            if (guids.Length == 0)
            {
                 _statusLabel.text = "No ScriptableObjectInstallers found in project.";
                 _refreshCoroutine = null;
                 yield break; // Exit coroutine
            }

             _statusLabel.text = $"Found {guids.Length} potential installers. Loading...";
             yield return null;

            // Load assets (yield periodically if many assets)
            for(int i=0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                // Check if path is valid before loading
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) // Basic check
                {
                    var installer = AssetDatabase.LoadAssetAtPath<ScriptableObjectInstaller>(path);
                    if (installer != null)
                    {
                        allInstallers.Add(installer);
                    }
                } else {
                     Debug.LogWarning($"[BindingsViewer] Found GUID {guids[i]} but path '{path}' is invalid or file doesn't exist.");
                }

                 yieldCounter++;
                 if (yieldCounter > 30) { // Yield every ~30 assets loaded
                     yield return null;
                     yieldCounter = 0;
                 }
            }

             _statusLabel.text = $"Simulating bindings from {allInstallers.Count} installers...";
             yield return null; // Allow UI update
             yieldCounter = 0;


            // Simulate bindings by calling InstallBindings on the collector
            foreach (var installer in allInstallers.OrderBy(i => i.name)) // Sort for consistency
            {
                 collector.SetCurrentInstaller(installer); // Track source
                 try
                 {
                     installer.InstallBindings(collector);
                 }
                 catch (NotImplementedException)
                 {
                     // Ignore errors from collector's unimplemented methods - This shouldn't happen if collector is correct
                      Debug.LogWarning($"[BindingsViewer] NotImplementedException simulating '{installer.name}'. BindingCollector might be incomplete.", installer);
                 }
                 catch (Exception ex)
                 {
                     Debug.LogError($"[BindingsViewer] Error simulating installer '{installer.name}': {ex}", installer);
                      AddStatusMessage($"Error simulating '{installer.name}'. See console.", true);
                 }
                 yieldCounter++;
                  if (yieldCounter > 10) { // Yield every ~10 installers simulated
                     yield return null;
                     yieldCounter = 0;
                 }
            }

            // Display the recorded bindings
            DisplaySimulatedBindings(collector.RecordedBindings);

            _statusLabel.text = $"Refreshed (Edit Mode - Simulated {collector.RecordedBindings.Count} bindings) - {DateTime.Now:H:mm:ss}";
            _refreshCoroutine = null; // Mark coroutine as finished
        }

        private void DisplaySimulatedBindings(List<RecordedBinding> bindings)
        {
             // Ensure UI elements are still valid
            if (_bindingsContainer == null) return;

            if (bindings == null || bindings.Count == 0)
            {
                _bindingsContainer.Add(new Label("(No bindings recorded from installers)"));
                return;
            }

            // Group bindings by the installer that registered them
            var groupedBindings = bindings
                .GroupBy(b => b.InstallerSource)
                .OrderBy(g => g.Key != null ? g.Key.name : "Unknown Source");

            foreach (var group in groupedBindings)
            {
                string title = group.Key != null ? $"Installer: {group.Key.name}" : "Bindings from Unknown Source";
                var foldout = new Foldout { text = title };
                _bindingsContainer.Add(foldout);

                if (!group.Any())
                {
                     foldout.Add(new Label("  (No bindings recorded by this installer)"));
                     continue;
                }

                // Sort bindings within the group
                var sortedBindings = group.OrderBy(b => b.RegisteredType.Name);

                foreach (var binding in sortedBindings)
                {
                    string boundTo = DetermineSimulatedBoundToDescription(binding);
                    var label = new Label($"  • {binding.RegisteredType.Name} -> {boundTo} [{binding.Lifetime}]");
                    label.tooltip = $"Registered: {binding.RegisteredType.FullName}\nBound To: {boundTo}\nLifetime: {binding.Lifetime}\nSource: {(group.Key != null ? AssetDatabase.GetAssetPath(group.Key) : "N/A")}";

                    // Add click handler to ping the installer asset
                    if (group.Key != null)
                    {
                         // Capture loop variable correctly for the lambda
                         var installerAsset = group.Key;
                         label.RegisterCallback<MouseDownEvent>(evt => {
                             // Ping object on single click
                             if (evt.clickCount == 1 && installerAsset != null)
                             {
                                 EditorGUIUtility.PingObject(installerAsset);
                                 evt.StopPropagation(); // Prevent potential text selection issues
                             }
                         });
                         // Consider adding a visual cue like text color or style class
                         label.AddToClassList("clickable-link"); // Example: Use USS for styling
                    }
                    foldout.Add(label);
                }
            }
        }

         private string DetermineSimulatedBoundToDescription(RecordedBinding binding)
         {
             switch(binding.SourceType)
             {
                 case BindingSourceType.Instance: return "Instance<Bound In Code>"; // Can't know instance type easily
                 case BindingSourceType.Factory: return "Factory Method";
                 case BindingSourceType.Type:
                     return $"Type<{binding.ImplementationType?.Name ?? "Unknown Type"}>";
                 default:
                     // Handle case where only Bind<T>() was called without To/From
                     if (!binding.RegisteredType.IsAbstract && !binding.RegisteredType.IsInterface)
                     {
                         return $"Type<{binding.RegisteredType.Name}> (Implicit)";
                     }
                     return "<Incomplete Binding>";
             }
         }

         // Helper to add status messages without overwriting the main status
         private void AddStatusMessage(string message, bool isError)
         {
             // Log to console for persistence
             if(isError) Debug.LogError($"[BindingsViewer Status] {message}");
             else Debug.LogWarning($"[BindingsViewer Status] {message}");

             // Update status label (shows last message)
             if (_statusLabel != null)
             {
                 _statusLabel.text = $"Status: {message}";
                 if (isError) _statusLabel.style.backgroundColor = _errorColor;
                 // Optionally reset color after a delay?
             }
         }
    }
}