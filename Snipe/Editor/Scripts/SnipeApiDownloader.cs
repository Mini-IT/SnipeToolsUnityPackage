#if UNITY_EDITOR

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEditor;
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

		protected void OnEnable()
		{
			SnipeToolsConfig.Load();

			FindSnipeApiDirectory();

			if (SnipeAutoUpdater.AutoUpdateEnabled)
			{
				SnipeAutoUpdater.CheckUpdateAvailable();
			}
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

		private void OnGUI()
		{
			EditorGUILayout.Space();

			EditorGUIUtility.labelWidth = 100;

			SnipeToolsGUI.DrawAuthKeyWidget();

			//bool authValid = (!string.IsNullOrEmpty(SnipeToolsConfig.AuthKey) && SnipeToolsConfig.ProjectId > 0);
			// if (!SnipeToolsConfig.Initialized || !authValid)
			// {
			// 	EditorGUILayout.HelpBox("Settings not filled", MessageType.Error);
			// 	if (GUILayout.Button("Go to Settings..."))
			// 	{
			// 		SnipeToolsSettingsWindow.ShowWindow();
			// 		Close();
			// 	}
			//
			// 	EditorGUILayout.Space();
			// }


			EditorGUI.BeginDisabledGroup(!SnipeToolsConfig.IsAuthKeyValid);

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			GUILayout.BeginHorizontal();
			_directoryPath = EditorGUILayout.TextField("Directory", _directoryPath);
			if (GUILayout.Button("...", GUILayout.Width(40)))
			{
				string filename = SERVICE_FILE_NAME;
				string path = EditorUtility.SaveFolderPanel($"Choose location of {filename}", _directoryPath, "");
				if (!string.IsNullOrEmpty(path))
				{
					_directoryPath = path;
				}
			}
			GUILayout.EndHorizontal();

			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();

			EditorGUI.BeginDisabledGroup(true);
			GUILayout.Label("Snipe API Service Version: " + SNIPE_VERSION_SUFFIX);
			EditorGUI.EndDisabledGroup();

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Download"))
			{
				DownloadSnipeApiAndClose();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUI.EndDisabledGroup();
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
