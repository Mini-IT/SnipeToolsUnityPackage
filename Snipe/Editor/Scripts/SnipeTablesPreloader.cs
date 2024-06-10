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

	private static string mTablesUrl;
	
	private static Dictionary<string, string> mVersions = null;
	private static string mStreamingAssetsPath;
	
	public int callbackOrder { get { return 10; } }
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
	
	public static string GetTablesBaseUrl(string project_string_id)
	{
		return $"https://static-dev.snipe.dev/{project_string_id}/";
	}

	private static string GetVersionsUrl()
	{
		return $"{mTablesUrl}/version.json";
	}

	private static string GetTableUrl(string table_name, string version)
	{
		return $"{mTablesUrl}/{version}_{table_name}.json.gz";
	}

	private static string GetTableFilePath(string table_name, string version)
	{
		string filename = $"{version}_{table_name}.jsongz";

		// NOTE: There is a bug - only lowercase works
		// (https://issuetracker.unity3d.com/issues/android-loading-assets-from-assetbundles-takes-significantly-more-time-when-the-project-is-built-as-an-aab)
		filename = filename.ToLower();

		return Path.Combine(mStreamingAssetsPath, filename);
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
		
		mStreamingAssetsPath = Application.streamingAssetsPath;

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

		if (mVersions == null || mVersions.Count == 0)
		{
			Debug.Log("[SnipeTablesPreloader] - Tables list is empty");
			return;
		}
		
		Debug.Log("[SnipeTablesPreloader] Total tables count = " + mVersions.Count);
		
		var files = Directory.EnumerateFiles(mStreamingAssetsPath, "*.jsongz*");
		foreach (string filename in files)
		{
			foreach (string tablename in mVersions.Keys)
			{
				if (filename.ToLower().Contains($"_{tablename.ToLower()}."))
				{
					Debug.Log("[SnipeTablesPreloader] Delete " + filename);
					FileUtil.DeleteFileOrDirectory(filename);
					break;
				}
			}
		}
		
		string version_file_path = GetTablesVersionFilePath();
		if (File.Exists(version_file_path))
			File.Delete(version_file_path);

		foreach (string tablename in mVersions.Keys)
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

		string project_string_id = "";

		Debug.Log($"[SnipeTablesPreloader] project id = {SnipeAuthKey.ProjectId}");

		if (SnipeAuthKey.ProjectId > 0)
		{
			Debug.Log($"[SnipeTablesPreloader] Fetching projects list");

			try
			{
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeAuthKey.AuthKey);
				var content = httpClient.GetStringAsync($"https://edit.snipe.dev/api/v1/project/{SnipeAuthKey.ProjectId}/stringID").Result;

				Debug.Log($"[SnipeTablesPreloader] {content}");

				var response_data = new ProjectStringIdResponseData();
				UnityEditor.EditorJsonUtility.FromJsonOverwrite(content, response_data);
				project_string_id = response_data.stringID;
				Debug.Log($"[SnipeTablesPreloader] Project StringID request errorCode = {response_data.errorCode}");
				Debug.Log($"[SnipeTablesPreloader] Project StringID = {project_string_id}");
			}
			catch (Exception e)
			{
				Debug.LogError($"[SnipeTablesPreloader] FAILED to fetch projects list: {e}");
				return false;
			}

			Debug.Log($"[SnipeTablesPreloader] Fetching tables list for project {project_string_id}");
			
			mTablesUrl = GetTablesBaseUrl(project_string_id);
			
			Debug.Log($"[SnipeTablesPreloader] TablesUrl = {mTablesUrl}");

			try
			{
				string url = GetVersionsUrl();
				var content = httpClient.GetStringAsync(url).Result;

				Debug.Log($"[SnipeTablesPreloader] {content}");
				
				using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
				{
					string path = Path.Combine(mStreamingAssetsPath, "snipe_tables.json");
					SaveToCache(stream, path);
				}

				ParseVersions(content);
			}
			catch (Exception e)
			{
				Debug.LogError($"[SnipeTablesPreloader] FAILED to fetch tables list: {e}");
				return false;
			}
		}

		Debug.Log("[SnipeTablesPreloader] DownloadTablesList - done");
		return true;
	}

	private static void ParseVersions(string json)
	{
		var list_wrapper = new TablesListResponseListWrapper();
		UnityEditor.EditorJsonUtility.FromJsonOverwrite(json, list_wrapper);
		if (list_wrapper.tables is List<TablesListResponseListItem> list)
		{
			Debug.Log($"[SnipeTablesPreloader] Parsed tables count = {list.Count}");

			mVersions = new Dictionary<string, string>();

			foreach (var item in list)
			{
				if (!string.IsNullOrEmpty(item.name))
				{
					mVersions[item.name] = $"{item.version}";
				}
			}
		}
	}

	protected static async Task LoadTable(HttpClient httpClient, string table_name)
	{
		if (!mVersions.TryGetValue(table_name, out string version))
		{
			Debug.LogError("[SnipeTablesPreloader] Failed to load table - " + table_name + " - Version unknown");
			return;
		}

		string url = GetTableUrl(table_name, version);
		
		Debug.Log("[SnipeTablesPreloader] Loading table " + url);
		
		try
		{
			HttpResponseMessage response = null;
			for (int retry = 0; retry < LOADING_RETIES_COUNT; retry++)
			{
				try
				{
					var loader_task = httpClient.GetAsync(url);

					await loader_task;

					if (loader_task.IsFaulted || loader_task.IsCanceled || !loader_task.Result.IsSuccessStatusCode)
					{
						Debug.LogError($"[SnipeTablesPreloader] Failed to load table - {table_name}   (loader failed) - StatusCode: {loader_task.Result.StatusCode}");
					}
						
					response = loader_task.Result;
				}
				catch (Exception loader_ex)
				{
					Debug.LogError("[SnipeTablesPreloader] Failed to load table - " + table_name + " - loader exception: " + loader_ex.ToString());
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
					Debug.Log($"[SnipeTablesPreloader] Failed to load table - {table_name}   (loader failed) - rety {retry}");
					await Task.Delay(3000 * (retry + 1));
				}
			}

			if (response != null && !response.IsSuccessStatusCode)
			{
				Debug.LogError($"[SnipeTablesPreloader] LoadTable {table_name} - Failed - http error: {response.StatusCode}");
#if UNITY_CLOUD_BUILD
				throw new BuildFailedException($"Failed to load table - {table_name}");
#endif
				return;
			}

			string cache_path = GetTableFilePath(table_name, version);

			using (var file_content_stream = await response.Content.ReadAsStreamAsync())
			{
				SaveToCache(file_content_stream, cache_path);
			}
		}
		catch (Exception e)
		{
			Debug.LogError("[SnipeTablesPreloader] Failed to load table - " + table_name + " - " + e.ToString());
#if UNITY_CLOUD_BUILD
			throw new BuildFailedException($"Failed to load table - {table_name}");
#endif
		}
	}

	private static void SaveToCache(Stream content, string cache_path)
	{
		using (FileStream cache_write_stream = new FileStream(cache_path, FileMode.Create, FileAccess.Write))
		{
			try
			{
				if (content.CanSeek)
				{
					content.Position = 0;
				}
				content.CopyTo(cache_write_stream);

				Debug.Log("[SnipeTablesPreloader] Saved: " + cache_path);
			}
			catch (Exception ex)
			{
				Debug.LogError("[SnipeTablesPreloader] Failed to save - " + cache_path + " - " + ex.ToString());
#if UNITY_CLOUD_BUILD
				throw new BuildFailedException($"Failed to save table - {cache_path}");
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
