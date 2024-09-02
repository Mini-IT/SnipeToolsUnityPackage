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
			Debug.Log("[SnipeTablesPreloader] OnPreprocessBuild - started");

			Load();

			Debug.Log("[SnipeTablesPreloader] OnPreprocessBuild - finished");
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
			using (var client = new HttpClient())
			{
				var loadingTask = Task.Run(() => Load(client));
				loadingTask.Wait();

				if (!loadingTask.IsCompletedSuccessfully || !loadingTask.Result)
				{
					Debug.LogError("[SnipeTablesPreloader] Loading FAILED");
#if UNITY_CLOUD_BUILD
				throw new BuildFailedException("Failed to fetch tables list");
#endif
				}
			}

			Debug.Log("[SnipeTablesPreloader] Load - tables processing finished. Invoking AssetDatabase.Refresh");

			AssetDatabase.Refresh();

			Debug.Log("[SnipeTablesPreloader] Load - done");
		}

		private static async Task<bool> Load(HttpClient httpClient)
		{
			Debug.Log("[SnipeTablesPreloader] Load - started");

			s_streamingAssetsPath = Application.streamingAssetsPath;

			for (int retry = 0; retry < LOADING_RETIES_COUNT; retry++)
			{
				if (DownloadTablesList(httpClient))
				{
					break;
				}

				if (retry < LOADING_RETIES_COUNT - 1)
				{
					Debug.Log($"[SnipeTablesPreloader] - DownloadTablesList FAILED - retry {retry}");
					await Task.Delay(1000 * (retry + 1));
				}
				else // no more reties
				{
					return false;
				}
			}

			if (s_versions == null || s_versions.Count == 0)
			{
				Debug.Log("[SnipeTablesPreloader] - Tables list is empty");
				return false;
			}

			Debug.Log("[SnipeTablesPreloader] Total tables count = " + s_versions.Count);

			var files = Directory.EnumerateFiles(s_streamingAssetsPath, "*.jsongz*");
			foreach (string filename in files)
			{
				foreach (string tablename in s_versions.Keys)
				{
					if (filename.ToLower().Contains($"_{tablename.ToLower()}."))
					{
						Debug.Log("[SnipeTablesPreloader] Delete " + filename);
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
				Debug.Log($"[SnipeTablesPreloader] {tablename} - start loading");

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

				Debug.Log($"[SnipeTablesPreloader] {tablename} - finish loading");
			}

			return true;
		}

		public static bool DownloadTablesList(HttpClient httpClient)
		{
			Debug.Log("[SnipeTablesPreloader] DownloadTablesList - start");

			if (string.IsNullOrEmpty(SnipeToolsConfig.AuthKey))
			{
				SnipeToolsConfig.Load();
			}
			if (string.IsNullOrEmpty(SnipeToolsConfig.AuthKey))
			{
				Debug.Log("[SnipeTablesPreloader] - FAILED - invalid AuthKey");
				return false;
			}
			if (SnipeToolsConfig.ProjectId <= 0)
			{
				Debug.Log("[SnipeTablesPreloader] - FAILED - invalid project id");
				return false;
			}


			Debug.Log($"[SnipeTablesPreloader] project id = {SnipeToolsConfig.ProjectId}");

			Debug.Log($"[SnipeTablesPreloader] Fetching projects list");

			string projectStringID = FetchProjectID(httpClient);
			if (string.IsNullOrEmpty(projectStringID))
			{
				return false;
			}

			Debug.Log($"[SnipeTablesPreloader] Fetching tables list for project {projectStringID}");

			s_tablesUrl = GetTablesBaseUrl(projectStringID);

			Debug.Log($"[SnipeTablesPreloader] TablesUrl = {s_tablesUrl}");

			try
			{
				string url = GetVersionsUrl();
				var content = httpClient.GetStringAsync(url).Result;

				Debug.Log($"[SnipeTablesPreloader] {content}");

				using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
				{
					string path = Path.Combine(s_streamingAssetsPath, "snipe_tables.json");
					SaveToCache(stream, path);
				}

				ParseVersions(content);
			}
			catch (Exception e)
			{
				Debug.Log($"[SnipeTablesPreloader] FAILED to fetch tables list: {e}");
				return false;
			}

			Debug.Log("[SnipeTablesPreloader] DownloadTablesList - done");
			return true;
		}

		private static string FetchProjectID(HttpClient httpClient)
		{
			string projectStringID;

			try
			{
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeToolsConfig.AuthKey);
				string url = $"https://edit.snipe.dev/api/v1/project/{SnipeToolsConfig.ProjectId}/stringID";
				var content = httpClient.GetStringAsync(url).Result;

				Debug.Log($"[SnipeTablesPreloader] {content}");

				var responseData = fastJSON.JSON.ToObject<ProjectStringIdResponseData>(content);
				projectStringID = responseData.stringID;
				Debug.Log($"[SnipeTablesPreloader] Project StringID request errorCode = {responseData.errorCode}");
				Debug.Log($"[SnipeTablesPreloader] Project StringID = {projectStringID}");
			}
			catch (Exception e)
			{
				Debug.LogError($"[SnipeTablesPreloader] FAILED to fetch projects list: {e}");
				return null;
			}

			return projectStringID;
		}

		private static void ParseVersions(string json)
		{
			var listWrapper = fastJSON.JSON.ToObject<TablesListResponseListWrapper>(json);
			if (listWrapper.tables is List<TablesListResponseListItem> list)
			{
				Debug.Log($"[SnipeTablesPreloader] Parsed tables count = {list.Count}");

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
				Debug.LogError("[SnipeTablesPreloader] Failed to load table - " + tableName + " - Version unknown");
				return false;
			}

			string url = GetTableUrl(tableName, version);

			Debug.Log("[SnipeTablesPreloader] Loading table " + url);

			httpClient.Timeout = TimeSpan.FromSeconds(6);
			
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
							Debug.LogError($"[SnipeTablesPreloader] Failed to load table - {tableName}   (loader failed) - StatusCode: {loaderTask.Result.StatusCode}");
						}

						response = loaderTask.Result;
					}
					catch (OperationCanceledException)
					{
						Debug.LogError($"[SnipeTablesPreloader] Failed to load table - {tableName} - cancelled");
						Debug.Log($"cancellationToken.IsCancellationRequested = {cancellationToken.IsCancellationRequested}");
						return false;
					}
					catch (Exception le)
					{
						Debug.LogError($"[SnipeTablesPreloader] Failed to load table - {tableName} - loader exception: {le}");
					}

					if (cancellationToken.IsCancellationRequested)
					{
						return false;
					}

					if (response != null)
					{
						Debug.Log($"[SnipeTablesPreloader] StatusCode: {response.StatusCode}");

						if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
						{
							break;
						}
					}

					if (retry < LOADING_RETIES_COUNT - 1)
					{
						Debug.Log($"[SnipeTablesPreloader] Failed to load table - {tableName}   (loader failed) - rety {retry}");
						await Task.Delay(3000 * (retry + 1));
					}

					if (cancellationToken.IsCancellationRequested)
					{
						return false;
					}
				}

				if (response != null && !response.IsSuccessStatusCode)
				{
					Debug.LogError($"[SnipeTablesPreloader] LoadTable {tableName} - Failed - http error: {response.StatusCode}");
					return false;
				}

				string cachePath = GetTableFilePath(tableName, version);

				using (var file_content_stream = await response.Content.ReadAsStreamAsync())
				{
					if (!SaveToCache(file_content_stream, cachePath))
					{
						return false;
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogError("[SnipeTablesPreloader] Failed to load table - " + tableName + " - " + e.ToString());
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

					Debug.Log("[SnipeTablesPreloader] Saved: " + cachePath);
				}
				catch (Exception ex)
				{
					Debug.LogError("[SnipeTablesPreloader] Failed to save - " + cachePath + " - " + ex.ToString());
					return false;
				}
			}

			return true;
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

		[System.Serializable]
		internal class ProjectStringIdResponseData
		{
			public string errorCode;
			public string stringID;
		}

	}
}
