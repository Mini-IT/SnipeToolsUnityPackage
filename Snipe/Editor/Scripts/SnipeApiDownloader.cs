#if UNITY_EDITOR

using System;
using System.IO;
using System.Text.RegularExpressions;
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

		private const string STATIC_FILE_NAME = "SnipeApi.cs";
		private const string SERVICE_FILE_NAME = "SnipeApiService.cs";

		private const string PREFS_API_VARIATION = "SnipeApiVariarion";

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

			try
			{
				_variation = (SnipeApiVariation)EditorPrefs.GetInt(PREFS_API_VARIATION, (int)SnipeApiVariation.StaticClass);
			}
			catch (Exception)
			{
				_variation = SnipeApiVariation.StaticClass;
			}

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
				if (path.EndsWith(STATIC_FILE_NAME))
				{
					// Application.dataPath ends with "Assets"
					// path starts with "Assets" and ends with "SnipeApi.cs"
					_directoryPath = Application.dataPath + path.Substring(6, path.Length - 17);
				}
				else if (path.EndsWith(SERVICE_FILE_NAME))
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
				string filename = (_variation == SnipeApiVariation.Service) ? SERVICE_FILE_NAME : STATIC_FILE_NAME;
				string path = EditorUtility.SaveFolderPanel($"Choose location of {filename}", _directoryPath, "");
				if (!string.IsNullOrEmpty(path))
				{
					_directoryPath = path;
				}
			}
			GUILayout.EndHorizontal();
			
			EditorGUILayout.Space();
			
			EditorGUILayout.HelpBox("Snipe Client package v.5+ only", MessageType.Warning);
			
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
				EditorPrefs.SetInt(PREFS_API_VARIATION, (int)_variation);
			}
			
			EditorGUILayout.EndHorizontal();
			
			EditorGUILayout.BeginHorizontal();

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
				
				string filename = (_variation == SnipeApiVariation.Service) ? SERVICE_FILE_NAME : STATIC_FILE_NAME;
				string filePath = Path.Combine(_directoryPath, filename);

				using (StreamWriter sw = File.CreateText(filePath))
				{
					await response.Content.CopyToAsync(sw.BaseStream);
				}

				if (_variation == SnipeApiVariation.Service)
				{
					string staticSnipeApiPath = Path.Combine(_directoryPath, STATIC_FILE_NAME);
					if (File.Exists(staticSnipeApiPath))
					{
						File.Delete(staticSnipeApiPath);
						using (StreamWriter sw = File.CreateText(staticSnipeApiPath))
						{
							sw.WriteLine("using MiniIT.Snipe.Api;");
							sw.WriteLine("namespace MiniIT.Snipe");
							sw.WriteLine("{");
							sw.WriteLine("\tpublic static class SnipeApi");
							sw.WriteLine("\t{");
							sw.WriteLine("\t\tpublic static SnipeApiService Service { get; } = new SnipeApiService();");
							sw.WriteLine("\t\tpublic static SnipeTables Tables => Service.Tables;");
							sw.WriteLine("\t\tpublic static LogicManager LogicManager => Service.LogicManager;");
							sw.WriteLine("\t\tpublic static CalendarManager CalendarManager => Service.CalendarManager;");

							string content = await response.Content.ReadAsStringAsync();
							string patternBefore = @"public\sSnipeApiModule\w*\s";
							string patternAfter = @"\s?{\s?get;";
							var regex = new Regex(patternBefore + @"\w*" + patternAfter);
							var regexBefore = new Regex(patternBefore);
							var regexAfter = new Regex(patternAfter);
							var matches = regex.Matches(content);
							foreach (var match in matches ) 
							{
								string module = regexBefore.Replace(regexAfter.Replace(match.ToString(), string.Empty), string.Empty);
								if (string.IsNullOrEmpty(module))
									continue;

								sw.WriteLine($"\t\tpublic static SnipeApiModule{module} {module} => Service.{module};");
							}

							sw.WriteLine("\t\tpublic static void Initialize() => _ = Service;");
							sw.WriteLine("\t}");
							sw.WriteLine("}");
						}
					}
				}
			}

			Debug.Log("DownloadSnipeApi - done");
		}
	}
}

#endif // UNITY_EDITOR
