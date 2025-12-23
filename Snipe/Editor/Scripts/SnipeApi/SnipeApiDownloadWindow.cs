#if UNITY_EDITOR

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MiniIT.Snipe.Unity.Editor
{
	public class SnipeApiDownloadWindow : EditorWindow
	{
#if SNIPE_8_0_OR_NEWER
		private const string SNIPE_VERSION_SUFFIX = "V8";
#elif SNIPE_7_1_OR_NEWER
		private const string SNIPE_VERSION_SUFFIX = "V71";
#else
		private const string SNIPE_VERSION_SUFFIX = "V7";
#endif

		private const string SERVICE_FILE_NAME = "SnipeApiService.cs";

		private string _directoryPath;

		private TextField _directoryField;
		private Button _browseButton;
		private Label _versionLabel;
		private Button _downloadButton;
		private AuthKeyWidget _authKeyWidget;

		[MenuItem("Snipe/Download SnipeApi...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<SnipeApiDownloadWindow>("SnipeApi");
		}

		private void FindSnipeApiDirectory()
		{
			string[] results = AssetDatabase.FindAssets("SnipeApi");
			if (results != null && results.Length > 0)
			{
				for (int i = 0; i < results.Length; i++)
				{
					string path = AssetDatabase.GUIDToAssetPath(results[i]);

					if (path.EndsWith(SERVICE_FILE_NAME))
					{
						// Application.dataPath ends with "Assets"
						// path starts with "Assets" and ends with "SnipeApiService.cs"
						_directoryPath = Application.dataPath + path.Substring(6, path.Length - 24);
						break;
					}
				}
			}

			if (string.IsNullOrEmpty(_directoryPath))
			{
				_directoryPath = Application.dataPath;
			}
		}

		public void CreateGUI()
		{
			SnipeToolsConfig.Load();

			FindSnipeApiDirectory();

			if (SnipeAutoUpdater.AutoUpdateEnabled)
			{
				SnipeAutoUpdater.CheckUpdateAvailable();
			}

			//-------

			var root = rootVisualElement;
			UIUtility.LoadUI(root, "SnipeApiDownloadWindow", "base");

			_directoryField = root.Q<TextField>("directory");
			_browseButton = root.Q<Button>("btn-browse");
			_versionLabel = root.Q<Label>("version-label");
			_downloadButton = root.Q<Button>("btn-download");
			_authKeyWidget = root.Q<AuthKeyWidget>("auth-key-widget");

			_versionLabel.text = "Snipe API Service Version: " + SNIPE_VERSION_SUFFIX;
#if SNIPE_8_0_OR_NEWER
			_versionLabel.text += " (local source generator)";
#endif

			_directoryField.value = string.IsNullOrEmpty(_directoryPath) ? Application.dataPath : _directoryPath;
			_directoryField.RegisterValueChangedCallback(evt => _directoryPath = evt.newValue);

			_browseButton.clicked += () =>
			{
				string filename = SERVICE_FILE_NAME;
				string path = EditorUtility.SaveFolderPanel($"Choose location of {filename}", _directoryPath, "");
				if (!string.IsNullOrEmpty(path))
				{
					_directoryPath = path;
					_directoryField.value = path;
				}
			};

			SetControlsEnabled(SnipeToolsConfig.IsAuthKeyValid);

			_downloadButton.clicked += OnDownloadButtonPressed;
		}

		private void SetControlsEnabled(bool enabled)
		{
			_directoryField?.SetEnabled(enabled);
			_browseButton?.SetEnabled(enabled);
			_downloadButton?.SetEnabled(enabled);
			_authKeyWidget?.SetEnabled(enabled);
		}

		private async void OnDownloadButtonPressed()
		{
			SetControlsEnabled(false);
			try
			{
#if SNIPE_8_0_OR_NEWER
				await DownloadSpecsAndGenerateSnipeApi();
#else
				await DownloadSnipeApi();
#endif
				await Task.Yield();
				AssetDatabase.Refresh();
			}
			finally
			{
				SetControlsEnabled(true);
			}
		}

		private async Task DownloadSnipeApi()
		{
			Debug.Log("DownloadSnipeApi - start");

			if (string.IsNullOrEmpty(SnipeToolsConfig.AuthKey))
			{
				Debug.LogError("DownloadSnipeApi - FAILED to get token");
				return;
			}

			using (var loader = new HttpClient())
			{
				loader.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeToolsConfig.AuthKey);
				string url = $"https://edit.snipe.dev/api/v1/project/{SnipeToolsConfig.ProjectId}/code/unityBindings{SNIPE_VERSION_SUFFIX}";

				var response = await loader.GetAsync(url);

				if (!response.IsSuccessStatusCode)
				{
					Debug.LogError($"DownloadSnipeApi - FAILED to download SnipeApi; HTTP status: {(int)response.StatusCode} - {response.StatusCode}");
					return;
				}

				string filePath = Path.Combine(_directoryPath, SERVICE_FILE_NAME);

				await using (StreamWriter sw = File.CreateText(filePath))
				{
					await response.Content.CopyToAsync(sw.BaseStream);
				}

				//-------
				// Specs

				// url = $"https://edit.snipe.dev/api/v1/project/{SnipeToolsConfig.ProjectId}/code/meta";
				//
				// response = await loader.GetAsync(url);
				//
				// if (!response.IsSuccessStatusCode)
				// {
				// 	Debug.LogError($"DownloadSnipeApi - FAILED to download Specs; HTTP status: {(int)response.StatusCode} - {response.StatusCode}");
				// 	return;
				// }
				//
				// filePath = Path.Combine(_directoryPath, "SnipeApiSpecs.json");
				//
				// using (StreamWriter sw = File.CreateText(filePath))
				// {
				// 	await response.Content.CopyToAsync(sw.BaseStream);
				// }
			}

			Debug.Log("DownloadSnipeApi - done");
		}

		private async Task DownloadSpecsAndGenerateSnipeApi()
		{
			Debug.Log("SnipeApiGenerateWindow.GenerateSnipeApi - start");

			if (string.IsNullOrEmpty(SnipeToolsConfig.AuthKey))
			{
				Debug.LogError("SnipeApiGenerateWindow.GenerateSnipeApi - FAILED to get token");
				return;
			}

			if (SnipeToolsConfig.ProjectId <= 0)
			{
				Debug.LogError("SnipeApiSpecsDownloader.DownloadSpecsAsync - ProjectId is not configured");
				return;
			}

			// ensure directory exists
			// if (!Directory.Exists(_directoryPath))
			// {
			// 	Directory.CreateDirectory(_directoryPath);
			// }

			// Download specs JSON
			string specsJson = await SnipeApiSpecsDownloader.DownloadSpecsAsync(SnipeToolsConfig.ProjectId, SnipeToolsConfig.AuthKey);
			if (string.IsNullOrEmpty(specsJson))
			{
				Debug.LogError("SnipeApiGenerateWindow.GenerateSnipeApi - failed to download specs");
				return;
			}

			// Save raw specs JSON alongside generated code for debugging
			// try
			// {
			// 	var specsPath = Path.Combine(_directoryPath, "SnipeApiSpecs.json");
			// 	File.WriteAllText(specsPath, specsJson, System.Text.Encoding.UTF8);
			// }
			// catch (System.Exception e)
			// {
			// 	Debug.LogWarning($"SnipeApiGenerateWindow.GenerateSnipeApi - failed to save specs file: {e}");
			// }

			// Generate code from JSON
			string generatedCode = SnipeApiGenerator.Generate(specsJson);
			if (string.IsNullOrEmpty(generatedCode))
			{
				Debug.LogError("SnipeApiGenerateWindow.GenerateSnipeApi - failed to generate code");
				return;
			}

			// Write generated code to file
			string servicePath = Path.Combine(_directoryPath, SERVICE_FILE_NAME);
			await File.WriteAllTextAsync(servicePath, generatedCode, System.Text.Encoding.UTF8);
			Debug.Log($"SnipeApiGenerateWindow.GenerateSnipeApi - generated {SERVICE_FILE_NAME} at path: {servicePath}");

			Debug.Log("SnipeApiGenerateWindow.GenerateSnipeApi - done");
		}
	}
}

#endif // UNITY_EDITOR
