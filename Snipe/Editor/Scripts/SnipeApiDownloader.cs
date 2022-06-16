#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace MiniIT.Snipe.Editor
{

	public class SnipeApiDownloader : EditorWindow
	{
		private static readonly string[] SNIPE_VERSIONS = new string[] { "V5", "V6" };

		private string mProjectId = "1";
		private string mDirectoryPath;
		private string mLogin;
		private string mPassword;
		private string mSnipeVersionSuffix = SNIPE_VERSIONS[1]; //"V6";
		private bool mGetTablesList = true;

		private static string mPrefsPrefix;

		private string mToken;
		private string[] mProjectsList;
		private int mSelectedProjectIndex;

		public static string RefreshPrefsPrefix()
		{
			if (string.IsNullOrEmpty(mPrefsPrefix))
			{
				var hash = System.Security.Cryptography.MD5.Create().ComputeHash(UTF8Encoding.UTF8.GetBytes(Application.dataPath));
				StringBuilder builder = new StringBuilder();
				for (int i = 0; i < hash.Length; i++)
				{
					builder.Append(hash[i].ToString("x2"));
				}
				mPrefsPrefix = builder.ToString();
			}

			return mPrefsPrefix;
		}

		[MenuItem("Snipe/Download SnipeApi")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow(typeof(SnipeApiDownloader));
		}

		protected void OnEnable()
		{
			RefreshPrefsPrefix();

			mProjectId = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.project_id", mProjectId);
			mDirectoryPath = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.directory", mDirectoryPath);
			mLogin = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.login", mLogin);
			mPassword = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.password", mPassword);
			mSnipeVersionSuffix = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.snipe_version_suffix", mSnipeVersionSuffix);

			if (string.IsNullOrEmpty(mDirectoryPath))
				mDirectoryPath = Application.dataPath;
		}

		protected void OnDisable()
		{
			EditorPrefs.SetString($"{mPrefsPrefix}_SnipeApiDownloader.project_id", mProjectId);
			EditorPrefs.SetString($"{mPrefsPrefix}_SnipeApiDownloader.directory", mDirectoryPath);
			EditorPrefs.SetString($"{mPrefsPrefix}_SnipeApiDownloader.login", mLogin);
			EditorPrefs.SetString($"{mPrefsPrefix}_SnipeApiDownloader.password", mPassword);
			EditorPrefs.SetString($"{mPrefsPrefix}_SnipeApiDownloader.snipe_version_suffix", mSnipeVersionSuffix);
		}

		void OnGUI()
		{
			EditorGUIUtility.labelWidth = 100;

			mLogin = EditorGUILayout.TextField("Login", mLogin);
			mPassword = EditorGUILayout.PasswordField("Password", mPassword);

			EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(mLogin) || string.IsNullOrEmpty(mPassword));

			//mProjectId = EditorGUILayout.TextField("Project ID", mProjectId);

			GUILayout.BeginHorizontal();

			EditorGUILayout.LabelField($"Project: [{mProjectId}]");

			if (mProjectsList != null)
			{
				int selected_index = EditorGUILayout.Popup(mSelectedProjectIndex, mProjectsList);

				GUILayout.FlexibleSpace();
				if (selected_index != mSelectedProjectIndex)
				{
					mSelectedProjectIndex = selected_index;
					string selected_item = mProjectsList[mSelectedProjectIndex];
					if (int.TryParse(selected_item.Substring(0, selected_item.IndexOf("-")).Trim(), out int project_id))
					{
						mProjectId = project_id.ToString();
					}
				}
			}

			if (GUILayout.Button("Fetch Projects List"))
			{
				_ = FetchProjectsList();
			}

			GUILayout.EndHorizontal();

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

			mGetTablesList = EditorGUILayout.Toggle("Get tables list", mGetTablesList);

			EditorGUILayout.BeginHorizontal();

			GUILayout.Label("Snipe Version", GUILayout.Width(EditorGUIUtility.labelWidth));

			int index = Array.IndexOf(SNIPE_VERSIONS, mSnipeVersionSuffix);
			index = EditorGUILayout.Popup(index, SNIPE_VERSIONS);
			mSnipeVersionSuffix = SNIPE_VERSIONS[index];

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Download"))
			{
				DownloadSnipeApiAndClose();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUI.EndDisabledGroup();
		}

		private async Task FetchProjectsList()
		{
			mToken = await RequestAuthToken();
			if (string.IsNullOrEmpty(mToken))
			{
				UnityEngine.Debug.Log("DownloadSnipeApi - FAILED to get token");
				return;
			}

			using (var loader = new HttpClient())
			{
				loader.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mToken);
				var response = await loader.GetAsync($"https://edit.snipe.dev/api/v1/projects");
				
				if (!response.IsSuccessStatusCode)
				{
					UnityEngine.Debug.LogError($"DownloadSnipeApi - failed; HTTP status: {response.StatusCode}");
					return;
				}

				var content = await response.Content.ReadAsStringAsync();

				var list_wrapper = new ProjectsListResponseListWrapper();
				UnityEditor.EditorJsonUtility.FromJsonOverwrite(content, list_wrapper);
				if (list_wrapper.data is List<ProjectsListResponseListItem> list)
				{
					list.Sort((a, b) => { return a.id - b.id; });

					mProjectsList = new string[list.Count];
					for (int i = 0; i < list.Count; i++)
					{
						var item = list[i];
						mProjectsList[i] = $"{item.id} - {item.stringID} - {item.name} - {(item.isDev ? "DEV" : "LIVE")}";
						if (item.id.ToString() == mProjectId)
							mSelectedProjectIndex = i;
					}
				}
			}
		}

		private async void DownloadSnipeApiAndClose()
		{
			await DownloadSnipeApi();
			await System.Threading.Tasks.Task.Yield();
			if (mGetTablesList)
			{
				await SnipeTablesPreloadHelper.DownloadTablesList(mToken);
			}
			AssetDatabase.Refresh();
			this.Close();
		}

		public async Task DownloadSnipeApi()
		{
			UnityEngine.Debug.Log("DownloadSnipeApi - start");

			mToken = await RequestAuthToken();
			if (string.IsNullOrEmpty(mToken))
			{
				UnityEngine.Debug.LogError("DownloadSnipeApi - FAILED to get token");
				return;
			}

			using (var loader = new HttpClient())
			{
				loader.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mToken);
				var response = await loader.GetAsync($"https://edit.snipe.dev/api/v1/project/{mProjectId}/code/unityBindings{mSnipeVersionSuffix}");

				if (!response.IsSuccessStatusCode)
				{
					UnityEngine.Debug.LogError($"DownloadSnipeApi - FAILED to get token; HTTP status: {response.StatusCode}");
					return;
				}

				using (StreamWriter sw = File.CreateText(Path.Combine(mDirectoryPath, "SnipeApi.cs")))
				{
					await response.Content.CopyToAsync(sw.BaseStream);
				}
			}

			UnityEngine.Debug.Log("DownloadSnipeApi - done");
		}

		public static async Task<string> RequestAuthToken()
		{
			RefreshPrefsPrefix();
			string login = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.login");
			string password = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.password");

			var loader = new HttpClient();
			var request_data = new StringContent($"{{\"login\":\"{login}\",\"password\":\"{password}\"}}", Encoding.UTF8, "application/json");
			var loader_task = loader.PostAsync("https://edit.snipe.dev/api/v1/auth", request_data);
			var loader_response = await loader_task;

			if (loader_task.IsFaulted || loader_task.IsCanceled)
			{
				UnityEngine.Debug.Log($"[SnipeTablesPreloadHelper] Failed to auth");
				return null;
			}

			string content = loader_response.Content.ReadAsStringAsync().Result;
			UnityEngine.Debug.Log(content);

			var response = new SnipeAuthLoginResponseData();
			UnityEditor.EditorJsonUtility.FromJsonOverwrite(content, response);

			return response.token;
		}
	}

#pragma warning disable 0649

	[System.Serializable]
	internal class ProjectsListResponseListWrapper
	{
		public List<ProjectsListResponseListItem> data;
	}

	[System.Serializable]
	internal class ProjectsListResponseListItem
	{
		public int id;
		public string stringID;
		public string name;
		public bool isDev;
	}

#pragma warning restore 0649

}

#endif // UNITY_EDITOR
