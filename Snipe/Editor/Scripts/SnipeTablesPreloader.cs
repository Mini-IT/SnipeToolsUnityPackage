using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MiniIT.Snipe.Editor;
using System.Net.Http.Headers;

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

	[MenuItem ("Snipe/Preload Tables")]
	public static void Load()
	{
		using (var client = new HttpClient())
		{
			Load(client);
		}
	}

	private static void Load(HttpClient httpClient)
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
				Task.Delay(1000 * (retry + 1)).Wait();
			}
			else // no more reties
			{
#if UNITY_CLOUD_BUILD
				throw new BuildFailedException("Failed to fetch tables list");
#endif
			}
		}

		if (s_versions == null || s_versions.Count == 0)
		{
			Debug.Log("[SnipeTablesPreloader] - Tables list is empty");
			return;
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
					FileUtil.DeleteFileOrDirectory(filename);
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

			if (!Task.Run(() => LoadTable(httpClient, tablename)).Wait(180000))
			{
				Debug.LogWarning($"[SnipeTablesPreloader] Loading \"{tablename}\" FAILED by timeout");
			}

			Debug.Log($"[SnipeTablesPreloader] {tablename} - finish loading");
		}

		Debug.Log("[SnipeTablesPreloader] Load - tables processing finished. Invoking AssetDatabase.Refresh");

		AssetDatabase.Refresh();
		
		Debug.Log("[SnipeTablesPreloader] Load - done");
	}

	public static bool DownloadTablesList(HttpClient httpClient)
	{
		Debug.Log("[SnipeTablesPreloader] DownloadTablesList - start");

		if (string.IsNullOrEmpty(SnipeAuthKey.AuthKey))
		{
			SnipeAuthKey.Load();
		}
		if (string.IsNullOrEmpty(SnipeAuthKey.AuthKey))
		{
			Debug.Log("[SnipeTablesPreloader] - FAILED - invalid AuthKey");
			return false;
		}

		string projectStringID;

		Debug.Log($"[SnipeTablesPreloader] project id = {SnipeAuthKey.ProjectId}");

		if (SnipeAuthKey.ProjectId > 0)
		{
			Debug.Log($"[SnipeTablesPreloader] Fetching projects list");

			try
			{
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeAuthKey.AuthKey);
				var content = httpClient.GetStringAsync($"https://edit.snipe.dev/api/v1/project/{SnipeAuthKey.ProjectId}/stringID").Result;

				Debug.Log($"[SnipeTablesPreloader] {content}");

				var responseData = new ProjectStringIdResponseData();
				UnityEditor.EditorJsonUtility.FromJsonOverwrite(content, responseData);
				projectStringID = responseData.stringID;
				Debug.Log($"[SnipeTablesPreloader] Project StringID request errorCode = {responseData.errorCode}");
				Debug.Log($"[SnipeTablesPreloader] Project StringID = {projectStringID}");
			}
			catch (Exception e)
			{
				Debug.LogError($"[SnipeTablesPreloader] FAILED to fetch projects list: {e}");
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
		}

		Debug.Log("[SnipeTablesPreloader] DownloadTablesList - done");
		return true;
	}

	private static void ParseVersions(string json)
	{
		var listWrapper = new TablesListResponseListWrapper();
		UnityEditor.EditorJsonUtility.FromJsonOverwrite(json, listWrapper);
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

	protected static async Task LoadTable(HttpClient httpClient, string tableName)
	{
		if (!s_versions.TryGetValue(tableName, out string version))
		{
			Debug.LogError("[SnipeTablesPreloader] Failed to load table - " + tableName + " - Version unknown");
			return;
		}

		string url = GetTableUrl(tableName, version);
		
		Debug.Log("[SnipeTablesPreloader] Loading table " + url);
		
		try
		{
			HttpResponseMessage response = null;
			for (int retry = 0; retry < LOADING_RETIES_COUNT; retry++)
			{
				try
				{
					var loaderTask = httpClient.GetAsync(url);

					await loaderTask;

					if (loaderTask.IsFaulted || loaderTask.IsCanceled || !loaderTask.Result.IsSuccessStatusCode)
					{
						Debug.LogError($"[SnipeTablesPreloader] Failed to load table - {tableName}   (loader failed) - StatusCode: {loaderTask.Result.StatusCode}");
					}
						
					response = loaderTask.Result;
				}
				catch (Exception loader_ex)
				{
					Debug.LogError("[SnipeTablesPreloader] Failed to load table - " + tableName + " - loader exception: " + loader_ex.ToString());
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
			}

			if (response != null && !response.IsSuccessStatusCode)
			{
				Debug.LogError($"[SnipeTablesPreloader] LoadTable {tableName} - Failed - http error: {response.StatusCode}");
#if UNITY_CLOUD_BUILD
				throw new BuildFailedException($"Failed to load table - {tableName}");
#endif
				return;
			}

			string cachePath = GetTableFilePath(tableName, version);

			using (var file_content_stream = await response.Content.ReadAsStreamAsync())
			{
				SaveToCache(file_content_stream, cachePath);
			}
		}
		catch (Exception e)
		{
			Debug.LogError("[SnipeTablesPreloader] Failed to load table - " + tableName + " - " + e.ToString());
#if UNITY_CLOUD_BUILD
			throw new BuildFailedException($"Failed to load table - {tableName}");
#endif
		}
	}

	private static void SaveToCache(Stream content, string cachePath)
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
#if UNITY_CLOUD_BUILD
				throw new BuildFailedException($"Failed to save table - {cachePath}");
#endif
			}
		}
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
