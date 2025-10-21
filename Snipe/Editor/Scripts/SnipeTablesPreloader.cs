using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MiniIT.Snipe.Unity.Editor
{
	public class SnipeTablesPreloader : IPreprocessBuildWithReport
	{
		private const int LOADING_RETIES_COUNT = 4;

		private static string s_tablesUrl;

		private static Dictionary<string, string> s_versions = null;
		private static string s_streamingAssetsPath;

		public int callbackOrder => 10;

		public void OnPreprocessBuild(BuildReport report)
		{
			Log("OnPreprocessBuild - started");

			Load();

			Log("OnPreprocessBuild - finished");
		}

		private static string GetTablesVersionFilePath()
		{
			return Path.Combine(Application.streamingAssetsPath, "snipe_tables_version.txt");
		}

		public static string GetTablesBaseUrl(string projectStringID)
		{
			return $"https://static-dev.snipe.dev/{projectStringID}/";
		}

		private static string GetVersionsUrl()
		{
			return $"{s_tablesUrl}/version.json";
		}

		private static string GetTableUrl(string tableName, string version)
		{
			return $"{s_tablesUrl}/{version}_{tableName}.json.gz";
		}

		private static string GetTableFilePath(string tableName, string version)
		{
			string filename = $"{version}_{tableName}.jsongz";

			// NOTE: There is a bug - only lowercase works
			// (https://issuetracker.unity3d.com/issues/android-loading-assets-from-assetbundles-takes-significantly-more-time-when-the-project-is-built-as-an-aab)
			filename = filename.ToLower();

			return Path.Combine(s_streamingAssetsPath, filename);
		}

		[MenuItem("Snipe/Preload Tables")]
		public static void Load()
		{
			using (var httpClient = new HttpClient())
			{
				httpClient.Timeout = TimeSpan.FromSeconds(6);

				var loadingTask = Task.Run(() => Load(httpClient));
				loadingTask.Wait();

				if (!loadingTask.IsCompletedSuccessfully || !loadingTask.Result)
				{
					LogError("Loading FAILED");
#if UNITY_CLOUD_BUILD
				throw new BuildFailedException("Failed to fetch tables list");
#endif
				}
			}

			Log("Load - tables processing finished. Invoking AssetDatabase.Refresh");

			AssetDatabase.Refresh();

			Log("Load - done");
		}

		private static async Task<bool> Load(HttpClient httpClient)
		{
			Log("Load - started");

			s_streamingAssetsPath = Application.streamingAssetsPath;

			bool tablesListReady = false;

			for (int retry = 0; retry < LOADING_RETIES_COUNT; retry++)
			{
				tablesListReady = DownloadTablesList(httpClient);
				if (tablesListReady)
				{
					break;
				}

				if (retry < LOADING_RETIES_COUNT - 1)
				{
					Log($"- DownloadTablesList FAILED - retry {retry}");
					await Task.Delay(1000 * (retry + 1));
				}
				else // no more reties
				{
					return false;
				}
			}

			if (s_versions == null || s_versions.Count == 0)
			{
				Log("- Tables list is empty");
				return tablesListReady;
			}

			Log("Total tables count = " + s_versions.Count);

			var files = Directory.EnumerateFiles(s_streamingAssetsPath, "*.jsongz*");
			foreach (string filename in files)
			{
				foreach (string tablename in s_versions.Keys)
				{
					if (filename.ToLower().Contains($"_{tablename.ToLower()}."))
					{
						Log("Delete " + filename);
						if (File.Exists(filename))
						{
							File.Delete(filename);
						}
						break;
					}
				}
			}

			string versionFilePath = GetTablesVersionFilePath();
			if (File.Exists(versionFilePath))
			{
				File.Delete(versionFilePath);
			}

			foreach (string tablename in s_versions.Keys)
			{
				Log($"{tablename} - start loading");

				using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(18));
				var loadingTask = LoadTable(tablename, httpClient, cancellation.Token);
				await loadingTask;

				if (cancellation.IsCancellationRequested)
				{
					Debug.LogWarning($"[SnipeTablesPreloader] Loading \"{tablename}\" FAILED by timeout");
					return false;
				}

				if (!loadingTask.IsCompletedSuccessfully || !loadingTask.Result)
				{
					Debug.LogWarning($"[SnipeTablesPreloader] Loading \"{tablename}\" FAILED");
					return false;
				}

				Log($"{tablename} - finish loading");
			}

			return true;
		}

		public static bool DownloadTablesList(HttpClient httpClient)
		{
			Log("DownloadTablesList - start");

			if (string.IsNullOrEmpty(SnipeToolsConfig.AuthKey))
			{
				SnipeToolsConfig.Load();
			}
			if (string.IsNullOrEmpty(SnipeToolsConfig.AuthKey))
			{
				Log("- FAILED - invalid AuthKey");
				return false;
			}
			if (SnipeToolsConfig.ProjectId <= 0)
			{
				Log("- FAILED - invalid project id");
				return false;
			}


			Log($"project id = {SnipeToolsConfig.ProjectId}");

			Log($"Fetching projects list");

			string projectStringID = SnipeToolsConfig.GetProjectStringID();
			if (string.IsNullOrEmpty(projectStringID))
			{
				Log("Project String ID is null");
				return false;
			}

			projectStringID = UnstripProjectStringID(projectStringID);

			Log($"Fetching tables list for project {projectStringID}");

			s_tablesUrl = GetTablesBaseUrl(projectStringID);

			Log($"TablesUrl = {s_tablesUrl}");

			try
			{
				string url = GetVersionsUrl();
				var content = httpClient.GetStringAsync(url).Result;

				Log($"{content}");

				using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
				{
					string path = Path.Combine(s_streamingAssetsPath, "snipe_tables.json");
					SaveToCache(stream, path);
				}

				ParseVersions(content);
			}
			catch (Exception e)
			{
				Log($"FAILED to fetch tables list: {e}");
				return false;
			}

			Log("DownloadTablesList - done");
			return true;
		}

		// private static string FetchProjectID(HttpClient httpClient)
		// {
		// 	string projectStringID;
		//
		// 	try
		// 	{
		// 		httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeToolsConfig.AuthKey);
		// 		string url = $"https://edit.snipe.dev/api/v1/project/{SnipeToolsConfig.ProjectId}/stringID";
		// 		var content = httpClient.GetStringAsync(url).Result;
		//
		// 		Log($"{content}");
		//
		// 		var responseData = fastJSON.JSON.ToObject<ProjectStringIdResponseData>(content);
		// 		projectStringID = responseData.stringID;
		// 		Log($"Project StringID request errorCode = {responseData.errorCode}");
		// 		Log($"Project StringID = {projectStringID}");
		// 	}
		// 	catch (Exception e)
		// 	{
		// 		LogError($"FAILED to fetch projects list: {e}");
		// 		return null;
		// 	}
		//
		// 	return projectStringID;
		// }

		private static void ParseVersions(string json)
		{
			var listWrapper = fastJSON.JSON.ToObject<TablesListResponseListWrapper>(json);
			if (listWrapper.tables is List<TablesListResponseListItem> list)
			{
				Log($"Parsed tables count = {list.Count}");

				s_versions = new Dictionary<string, string>();

				foreach (var item in list)
				{
					if (!string.IsNullOrEmpty(item.name))
					{
						s_versions[item.name] = $"{item.version}";
					}
				}
			}
		}

		protected static async Task<bool> LoadTable(string tableName, HttpClient httpClient, CancellationToken cancellationToken)
		{
			if (!s_versions.TryGetValue(tableName, out string version))
			{
				LogError("Failed to load table - " + tableName + " - Version unknown");
				return false;
			}

			string url = GetTableUrl(tableName, version);

			Log("Loading table " + url);

			try
			{
				HttpResponseMessage response = null;
				for (int retry = 0; retry < LOADING_RETIES_COUNT; retry++)
				{
					try
					{
						var loaderTask = httpClient.GetAsync(url, cancellationToken);

						await loaderTask;

						if (loaderTask.IsFaulted || loaderTask.IsCanceled || !loaderTask.Result.IsSuccessStatusCode)
						{
							LogError($"Failed to load table - {tableName}   (loader failed) - StatusCode: {loaderTask.Result.StatusCode}");
						}

						response = loaderTask.Result;
					}
					catch (OperationCanceledException)
					{
						LogError($"Failed to load table - {tableName} - cancelled");
						Debug.Log($"cancellationToken.IsCancellationRequested = {cancellationToken.IsCancellationRequested}");
						return false;
					}
					catch (Exception le)
					{
						LogError($"Failed to load table - {tableName} - loader exception: {le}");
					}

					if (cancellationToken.IsCancellationRequested)
					{
						return false;
					}

					if (response != null)
					{
						Log($"StatusCode: {response.StatusCode}");

						if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
						{
							break;
						}
					}

					if (retry < LOADING_RETIES_COUNT - 1)
					{
						Log($"Failed to load table - {tableName}   (loader failed) - rety {retry}");
						await Task.Delay(3000 * (retry + 1));
					}

					if (cancellationToken.IsCancellationRequested)
					{
						return false;
					}
				}

				if (response != null && !response.IsSuccessStatusCode)
				{
					LogError($"LoadTable {tableName} - Failed - http error: {response.StatusCode}");
					return false;
				}

				string cachePath = GetTableFilePath(tableName, version);

				using (var fileContentStream = await response.Content.ReadAsStreamAsync())
				{
					if (!SaveToCache(fileContentStream, cachePath))
					{
						return false;
					}
				}
			}
			catch (Exception e)
			{
				LogError("Failed to load table - " + tableName + " - " + e.ToString());
				return false;
			}

			return true;
		}

		private static bool SaveToCache(Stream content, string cachePath)
		{
			using (var cacheWriteStream = new FileStream(cachePath, FileMode.Create, FileAccess.Write))
			{
				try
				{
					if (content.CanSeek)
					{
						content.Position = 0;
					}
					content.CopyTo(cacheWriteStream);

					Log("Saved: " + cachePath);
				}
				catch (Exception ex)
				{
					LogError("Failed to save - " + cachePath + " - " + ex.ToString());
					return false;
				}
			}

			return true;
		}

		public static string UnstripProjectStringID(string projectStringID)
		{
			if (projectStringID.EndsWith("_dev"))
			{
				return projectStringID;
			}

			if (projectStringID.EndsWith("_live"))
			{
				projectStringID = projectStringID[..^5];
			}

			return projectStringID + "_dev";
		}

		private static void Log(string message)
		{
			Debug.Log($"[{nameof(SnipeTablesPreloader)}] {message}");
		}

		private static void LogError(string message)
		{
			Debug.LogError($"[{nameof(SnipeTablesPreloader)}] {message}");
		}

		[System.Serializable]
		internal class TablesListResponseListWrapper
		{
			public List<TablesListResponseListItem> tables;
		}

		[System.Serializable]
		internal class TablesListResponseListItem
		{
			public string name;
			public long version;
		}

	}
}
