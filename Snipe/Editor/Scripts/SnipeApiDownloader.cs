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

		private string mDirectoryPath;
		private string mSnipeVersionSuffix = "V6"; // SNIPE_VERSIONS[1]; //"V6";

		[MenuItem("Snipe/Download SnipeApi...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<SnipeApiDownloader>("SnipeApi");
		}

		protected void OnEnable()
		{
			SnipeAuthKey.Load();

			string[] results = AssetDatabase.FindAssets("SnipeApi");
			if (results != null && results.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(results[0]);
				if (path.EndsWith("SnipeApi.cs"))
				{
					// Application.dataPath edns with "Assets"
					// path starts with "Assets" and ends with "SnipeApi.cs"
					mDirectoryPath = Application.dataPath + path.Substring(6, path.Length - 17);
				}
			}
			
			if (string.IsNullOrEmpty(mDirectoryPath))
			{
				mDirectoryPath = Application.dataPath;
			}
			
			if (SnipeAutoUpdater.AutoUpdateEnabled)
				SnipeAutoUpdater.CheckUpdateAvailable();
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
			mDirectoryPath = EditorGUILayout.TextField("Directory", mDirectoryPath);
			if (GUILayout.Button("...", GUILayout.Width(40)))
			{
				string path = EditorUtility.SaveFolderPanel("Choose location of SnipeApi.cs", mDirectoryPath, "");
				if (!string.IsNullOrEmpty(path))
				{
					mDirectoryPath = path;
				}
			}
			GUILayout.EndHorizontal();
			
			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
			
			// GUILayout.Label("Snipe Version", GUILayout.Width(EditorGUIUtility.labelWidth));

			// int index = Array.IndexOf(SNIPE_VERSIONS, mSnipeVersionSuffix);
			// index = EditorGUILayout.Popup(index, SNIPE_VERSIONS);
			// mSnipeVersionSuffix = SNIPE_VERSIONS[index];

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
				string url = $"https://edit.snipe.dev/api/v1/project/{SnipeAuthKey.ProjectId}/code/unityBindings{mSnipeVersionSuffix}";
				var response = await loader.GetAsync(url);

				if (!response.IsSuccessStatusCode)
				{
					Debug.LogError($"DownloadSnipeApi - FAILED to get token; HTTP status: {(int)response.StatusCode} - {response.StatusCode}");
					return;
				}

				using (StreamWriter sw = File.CreateText(Path.Combine(mDirectoryPath, "SnipeApi.cs")))
				{
					await response.Content.CopyToAsync(sw.BaseStream);
				}
			}

			Debug.Log("DownloadSnipeApi - done");
		}
	}
}

#endif // UNITY_EDITOR
