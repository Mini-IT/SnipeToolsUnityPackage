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
	public class SnipeApiDownloader : EditorWindow
	{
#if SNIPE_8_0_OR_NEWER
		private const string SNIPE_VERSION_SUFFIX = "V8";
#elif SNIPE_7_1_OR_NEWER
		private const string SNIPE_VERSION_SUFFIX = "V71";
#elif SNIPE_7_0_OR_NEWER
		private const string SNIPE_VERSION_SUFFIX = "V7";
#else
		private const string SNIPE_VERSION_SUFFIX = "V61";
#endif

		private const string SERVICE_FILE_NAME = "SnipeApiService.cs";

		private string _directoryPath;

		[MenuItem("Snipe/Download SnipeApi...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<SnipeApiDownloader>("SnipeApi");
		}

		// protected void OnEnable()
		// {
		// 	SnipeToolsConfig.Load();
		//
		// 	FindSnipeApiDirectory();
		//
		// 	if (SnipeAutoUpdater.AutoUpdateEnabled)
		// 	{
		// 		SnipeAutoUpdater.CheckUpdateAvailable();
		// 	}
		// }

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
			var baseStyle = LoadStyleSheet("base");
			if (baseStyle != null)
			{
				root.styleSheets.Add(baseStyle);
			}

			var tree = LoadUxml("SnipeApiDownloader");
			if (tree != null)
			{
				tree.CloneTree(root);
			}

			var directoryField = root.Q<TextField>("directory");
			var browseButton = root.Q<Button>("browse");
			var versionLabel = root.Q<Label>("version-label");
			var downloadButton = root.Q<Button>("download");

			versionLabel.text = "Snipe API Service Version: " + SNIPE_VERSION_SUFFIX;

			directoryField.value = string.IsNullOrEmpty(_directoryPath) ? Application.dataPath : _directoryPath;
			directoryField.RegisterValueChangedCallback(evt => _directoryPath = evt.newValue);

			browseButton.clicked += () =>
			{
				string filename = SERVICE_FILE_NAME;
				string path = EditorUtility.SaveFolderPanel($"Choose location of {filename}", _directoryPath, "");
				if (!string.IsNullOrEmpty(path))
				{
					_directoryPath = path;
					directoryField.value = path;
				}
			};

			UpdateInteractable(root);

			downloadButton.clicked += () => DownloadSnipeApiAndClose();
		}

		private void UpdateInteractable(VisualElement root)
		{
			bool enabled = SnipeToolsConfig.IsAuthKeyValid;
			var directoryField = root.Q<TextField>("directory");
			var browseButton = root.Q<Button>("browse");
			var downloadButton = root.Q<Button>("download");

			if (directoryField != null) directoryField.SetEnabled(enabled);
			if (browseButton != null) browseButton.SetEnabled(enabled);
			if (downloadButton != null) downloadButton.SetEnabled(enabled);
		}

		private static VisualTreeAsset LoadUxml(string fileStem)
		{
			string filter = fileStem + " t:VisualTreeAsset";
			var guids = AssetDatabase.FindAssets(filter);
			if (guids != null && guids.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
				return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
			}
			return null;
		}

		private static StyleSheet LoadStyleSheet(string fileStem)
		{
			string filter = fileStem + " t:StyleSheet";
			var guids = AssetDatabase.FindAssets(filter);
			if (guids != null && guids.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
				return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
			}
			return null;
		}

		private async void DownloadSnipeApiAndClose()
		{
			await DownloadSnipeApi();
			await Task.Yield();
			AssetDatabase.Refresh();
			this.Close();
		}

		public async Task DownloadSnipeApi()
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
					Debug.LogError($"DownloadSnipeApi - FAILED to get token; HTTP status: {(int)response.StatusCode} - {response.StatusCode}");
					return;
				}

				string filePath = Path.Combine(_directoryPath, SERVICE_FILE_NAME);

				using (StreamWriter sw = File.CreateText(filePath))
				{
					await response.Content.CopyToAsync(sw.BaseStream);
				}
			}

			Debug.Log("DownloadSnipeApi - done");
		}
	}
}

#endif // UNITY_EDITOR
