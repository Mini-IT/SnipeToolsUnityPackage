#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

using Debug = UnityEngine.Debug;

namespace MiniIT.Snipe.Editor
{
	public class SnipeApiDownloader : EditorWindow
	{
		//private static readonly string[] SNIPE_VERSIONS = new string[] { "V5", "V6" };
		
		enum SnipeApiVariation
		{
			StaticClass,
			Service,
		}

		private string _directoryPath;
		private string _snipeVersionSuffix = "V6"; // SNIPE_VERSIONS[1]; //"V6";
		private SnipeApiVariation _variation = SnipeApiVariation.StaticClass;

		[MenuItem("Snipe/Download SnipeApi...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<SnipeApiDownloader>("SnipeApi");
		}

		protected void OnEnable()
		{
			SnipeAuthKey.Load();

			FindSnipeApiDirectory();
			
			if (SnipeAutoUpdater.AutoUpdateEnabled)
				SnipeAutoUpdater.CheckUpdateAvailable();
		}
		
		private void FindSnipeApiDirectory()
		{
			string[] results = AssetDatabase.FindAssets("SnipeApi");
			if (results != null && results.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(results[0]);
				if (path.EndsWith("SnipeApi.cs"))
				{
					// Application.dataPath ends with "Assets"
					// path starts with "Assets" and ends with "SnipeApi.cs"
					_directoryPath = Application.dataPath + path.Substring(6, path.Length - 17);
				}
				else if (path.EndsWith("SnipeApiService.cs"))
				{
					// Application.dataPath ends with "Assets"
					// path starts with "Assets" and ends with "SnipeApiService.cs"
					_directoryPath = Application.dataPath + path.Substring(6, path.Length - 24);
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
			
			string auth_key = EditorGUILayout.TextField("API Key", SnipeAuthKey.AuthKey);
			if (auth_key != SnipeAuthKey.AuthKey)
			{
				SnipeAuthKey.Set(auth_key);
				SnipeAuthKey.Save();
			}
			
			EditorGUILayout.Space();
			
			bool auth_valid = (!string.IsNullOrEmpty(SnipeAuthKey.AuthKey) && SnipeAuthKey.ProjectId > 0);

			EditorGUI.BeginDisabledGroup(!auth_valid);

			//if (!string.IsNullOrEmpty(SnipeAuthKey.AuthKey))
			//{
			//	EditorGUILayout.LabelField($"Project: [{SnipeAuthKey.ProjectId}] - extracted from the api key");
			//}
			
			EditorGUILayout.Space();
			EditorGUILayout.Space();

			GUILayout.BeginHorizontal();
			_directoryPath = EditorGUILayout.TextField("Directory", _directoryPath);
			if (GUILayout.Button("...", GUILayout.Width(40)))
			{
				string filename = (_variation == SnipeApiVariation.Service) ? "SnipeApiService.cs" : "SnipeApi.cs";
				string path = EditorUtility.SaveFolderPanel($"Choose location of {filename}", _directoryPath, "");
				if (!string.IsNullOrEmpty(path))
				{
					_directoryPath = path;
				}
			}
			GUILayout.EndHorizontal();
			
			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
			
			// GUILayout.Label("Snipe Version", GUILayout.Width(EditorGUIUtility.labelWidth));

			// int index = Array.IndexOf(SNIPE_VERSIONS, _snipeVersionSuffix);
			// index = EditorGUILayout.Popup(index, SNIPE_VERSIONS);
			// _snipeVersionSuffix = SNIPE_VERSIONS[index];
			
			bool serviceVariation = (_variation == SnipeApiVariation.Service);
			bool selectedVariation = EditorGUILayout.Toggle("Service class", serviceVariation);
			if (selectedVariation != serviceVariation)
			{
				_variation = selectedVariation ? SnipeApiVariation.Service : SnipeApiVariation.StaticClass;
			}

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
			
			if (string.IsNullOrEmpty(SnipeAuthKey.AuthKey))
			{
				Debug.LogError("DownloadSnipeApi - FAILED to get token");
				return;
			}

			using (var loader = new HttpClient())
			{
				loader.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeAuthKey.AuthKey);
				string url = $"https://edit.snipe.dev/api/v1/project/{SnipeAuthKey.ProjectId}/code/unityBindings{_snipeVersionSuffix}";
				if (_variation == SnipeApiVariation.Service)
					url += "1";
				
				var response = await loader.GetAsync(url);

				if (!response.IsSuccessStatusCode)
				{
					Debug.LogError($"DownloadSnipeApi - FAILED to get token; HTTP status: {(int)response.StatusCode} - {response.StatusCode}");
					return;
				}
				
				string filename = (_variation == SnipeApiVariation.Service) ? "SnipeApiService.cs" : "SnipeApi.cs";

				using (StreamWriter sw = File.CreateText(Path.Combine(_directoryPath, filename)))
				{
					await response.Content.CopyToAsync(sw.BaseStream);
				}
			}

			Debug.Log("DownloadSnipeApi - done");
		}
	}
}

#endif // UNITY_EDITOR
