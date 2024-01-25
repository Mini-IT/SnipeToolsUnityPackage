#if UNITY_EDITOR && SNIPE_6_1

using MiniIT.Snipe.Unity.Editor;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MiniIT.Snipe.Editor
{
	public class SnipeConfigDownloader : EditorWindow
	{
		private const string SA_FILE_NAME = "_snipe_config.json";
		private const string PREFS_PROPJECT_STRING_ID = "SnipeProjectStringID";
		private const string URL = "https://config.snipe.dev/api/v1/config";

		private MockApplicationInfo _appInfo;
		private string _projectStringID;
		private RuntimePlatform _platform;
		private bool _dev;
		private bool _amazon;

		[MenuItem("Snipe/Config...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow<SnipeConfigDownloader>("Snipe Config");
		}

		protected void OnEnable()
		{
			_projectStringID = EditorPrefs.GetString(PREFS_PROPJECT_STRING_ID);

			_platform = Application.platform;
			_appInfo = new MockApplicationInfo();
		}

		private void OnGUI()
		{
			EditorGUILayout.Space();
			
			EditorGUIUtility.labelWidth = 100;
			
			string projectStringID = EditorGUILayout.TextField("Project string ID", _projectStringID).Trim();
			if (projectStringID != _projectStringID)
			{
				_projectStringID = projectStringID;
				EditorPrefs.SetString(PREFS_PROPJECT_STRING_ID, _projectStringID);
			}
			
			EditorGUILayout.Space();

			_dev = EditorGUILayout.Toggle("Dev", _dev);

			_appInfo.ApplicationIdentifier = EditorGUILayout.TextField("App Identifier", _appInfo.ApplicationIdentifier);
			_appInfo.ApplicationVersion = EditorGUILayout.TextField("App Version", _appInfo.ApplicationVersion);

			_platform = (RuntimePlatform)EditorGUILayout.EnumPopup("Platform", _platform);
			if (_platform.ToString() != _appInfo.ApplicationPlatform)
			{
				_appInfo.ApplicationPlatform = _platform.ToString();
			}

			if (_platform == RuntimePlatform.Android)
			{
				_amazon = EditorGUILayout.Toggle("Amazon", _amazon);
			}
			else
			{
				_amazon = false;
			}

			EditorGUILayout.Space();
			
			EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_projectStringID));
			EditorGUILayout.BeginHorizontal();

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Download"))
			{
				DownloadConfigAndClose();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUI.EndDisabledGroup();
		}

		private async void DownloadConfigAndClose()
		{
			string json = await DownloadConfig();

			string filePath = Path.Combine(Application.streamingAssetsPath, SA_FILE_NAME);
			await File.WriteAllTextAsync(filePath, json);
		}

		private async Task<string> DownloadConfig()
		{
			Debug.Log("DownloadConfig - start");

			if (string.IsNullOrWhiteSpace( _projectStringID) )
			{
				return null;
			}

			var loader = new SnipeConfigLoader(_projectStringID, _appInfo);
			var config = await loader.Load();
			string json = fastJSON.JSON.ToJSON(config);

			Debug.Log(json);
			Debug.Log("DownloadConfig - done");

			return json;
		}
	}
}

#endif // UNITY_EDITOR
