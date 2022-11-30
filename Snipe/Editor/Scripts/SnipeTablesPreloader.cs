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
	private static HashSet<string> mTableNames;
	
	private static string mVersion;
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

	private static string GetTableUrl(string table_name)
	{
		return $"{mTablesUrl}/{table_name}.json.gz";
	}

	private static string GetTableFilePath(string table_name)
	{
		string filename = $"{mVersion}_{table_name}.jsongz";

		// NOTE: There is a bug - only lowercase works
		// (https://issuetracker.unity3d.com/issues/android-loading-assets-from-assetbundles-takes-significantly-more-time-when-the-project-is-built-as-an-aab)
		filename = filename.ToLower();

		return Path.Combine(mStreamingAssetsPath, filename);
	}

	[MenuItem ("Snipe/Preload Tables")]
	public static void Load()
	{
		Debug.Log("[SnipeTablesPreloader] Load - started");
		
		mStreamingAssetsPath = Application.streamingAssetsPath;

		for (int retry = 0; retry < LOADING_RETIES_COUNT; retry++)
		{
			if (DownloadTablesList())
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

		if (mTableNames == null || mTableNames.Count == 0)
		{
			Debug.Log("[SnipeTablesPreloader] - Tables list is empty");
			return;
		}
		
		Debug.Log("[SnipeTablesPreloader] Total tables count = " + mTableNames.Count);
		
		var files = Directory.EnumerateFiles(mStreamingAssetsPath, "*.jsongz*");
		foreach (string filename in files)
		{
			foreach (string tablename in mTableNames)
			{
				if (filename.Contains($"_{tablename}."))
				{
					Debug.Log("[SnipeTablesPreloader] Delete " + tablename);
					FileUtil.DeleteFileOrDirectory(filename);
					break;
				}
			}
		}
		
		string version_file_path = GetTablesVersionFilePath();
		if (File.Exists(version_file_path))
			File.Delete(version_file_path);

		for (int retry = 0; retry < LOADING_RETIES_COUNT; retry++)
		{
			if (!Task.Run(LoadVersion).Wait(180000))
			{
				Debug.LogError("[SnipeTablesPreloader] LoadVersion FAILED by timeout");
				continue;
			}

			if (!string.IsNullOrWhiteSpace(mVersion))
				break;

			if (retry < LOADING_RETIES_COUNT - 1)
			{
				Debug.Log($"[SnipeTablesPreloader] - LoadVersion FAILED - retry {retry}");
				Task.Delay(1000 * (retry + 1)).Wait();
			}
		}
		
		if (string.IsNullOrWhiteSpace(mVersion))
		{
			Debug.LogError("[SnipeTablesPreloader] FAILED to fetch version");
#if UNITY_CLOUD_BUILD
			throw new BuildFailedException("Failed to fetch tables version");
#endif
		}	

		foreach (string tablename in mTableNames)
		{
			Debug.Log($"[SnipeTablesPreloader] {tablename} - start loading");

			if (!Task.Run(() => LoadTable(tablename)).Wait(180000))
			{
				Debug.LogWarning($"[SnipeTablesPreloader] Loading \"{tablename}\" FAILED by timeout");
			}

			Debug.Log($"[SnipeTablesPreloader] {tablename} - finish loading");
		}

		Debug.Log("[SnipeTablesPreloader] Load - tables processing finished. Invoking AssetDatabase.Refresh");

		AssetDatabase.Refresh();
		
		Debug.Log("[SnipeTablesPreloader] Load - done");
	}

	public static bool DownloadTablesList()
	{
		Debug.Log("[SnipeTablesPreloader] DownloadTablesList - start");

		if (string.IsNullOrEmpty(SnipeAuthKey.AuthKey))
			SnipeAuthKey.Load();
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

			using (var project_string_id_client = new HttpClient())
			{
				try
				{
					project_string_id_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeAuthKey.AuthKey);
					var content = project_string_id_client.GetStringAsync($"https://edit.snipe.dev/api/v1/project/{SnipeAuthKey.ProjectId}/stringID").Result;

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
			}

			Debug.Log($"[SnipeTablesPreloader] Fetching tables list for project {project_string_id}");
			
			mTablesUrl = GetTablesBaseUrl(project_string_id);
			
			Debug.Log($"[SnipeTablesPreloader] TablesUrl = {mTablesUrl}");

			using (var client = new HttpClient())
			{
				try
				{
					client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeAuthKey.AuthKey);
					var content = client.GetStringAsync($"https://edit.snipe.dev/api/v1/project/{SnipeAuthKey.ProjectId}/tableFiles").Result;

					Debug.Log($"[SnipeTablesPreloader] {content}");

					var list_wrapper = new TablesListResponseListWrapper();
					UnityEditor.EditorJsonUtility.FromJsonOverwrite(content, list_wrapper);
					if (list_wrapper.data is List<TablesListResponseListItem> list)
					{
						Debug.Log($"[SnipeTablesPreloader] Parsed tables count = {list.Count}");

						mTableNames = new HashSet<string>();

						foreach (var item in list)
						{
							string table_name = item.stringID;
							if (!string.IsNullOrEmpty(table_name))
							{
								mTableNames.Add(table_name);
							}
						}
					}
				}
				catch (Exception e)
				{
					Debug.LogError($"[SnipeTablesPreloader] FAILED to fetch tables list: {e}");
					return false;
				}
			}
		}

		Debug.Log("[SnipeTablesPreloader] DownloadTablesList - done");
		return true;
	}

	private static async Task LoadVersion()
	{
		mVersion = "";
		string url = $"{mTablesUrl}/version.txt";
		
		Debug.Log($"[SnipeTablesPreloader] LoadVersion from {url}");
		
		try
		{
			var loader = new HttpClient();
			var loader_task = loader.GetAsync(url);

			await loader_task;

			if (loader_task.IsFaulted || loader_task.IsCanceled)
			{
				Debug.LogWarning("[SnipeTablesPreloader] LoadVersion - Failed to load tables version - (loader failed)");
				return;
			}
			
			HttpResponseMessage response = loader_task.Result;
			if (!response.IsSuccessStatusCode)
			{
				Debug.LogWarning($"[SnipeTablesPreloader] LoadVersion - Failed - http error: {response.StatusCode}");
				return;
			}
			
			using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync()))
			{
				mVersion = reader.ReadLine().Trim();
			}
			
			Debug.Log($"[SnipeTablesPreloader] Version = {mVersion}");
			
			if (!string.IsNullOrWhiteSpace(mVersion))
			{
				// save to file
				File.WriteAllText(GetTablesVersionFilePath(), mVersion);
			}
		}
		catch (Exception)
		{
			mVersion = "";
			Debug.LogError("[SnipeTablesPreloader] LoadVersion - Failed to read tables version");
		}
	}
	
	protected static async Task LoadTable(string table_name)
	{
		string url = GetTableUrl(table_name);
		
		Debug.Log("[SnipeTablesPreloader] Loading table " + url);
		
		try
		{
			using (var loader = new HttpClient())
			{
				HttpResponseMessage response = null;
				for (int retry = 0; retry < LOADING_RETIES_COUNT; retry++)
				{
					try
					{
						var loader_task = loader.GetAsync(url);

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
				}

				string cache_path = GetTableFilePath(table_name);

				using (var file_content_stream = await response.Content.ReadAsStreamAsync())
				{
					using (FileStream cache_write_stream = new FileStream(cache_path, FileMode.Create, FileAccess.Write))
					{
						try
						{
							file_content_stream.Position = 0;
							file_content_stream.CopyTo(cache_write_stream);

							Debug.Log("[SnipeTablesPreloader] Table saved: " + cache_path);
						}
						catch (Exception ex)
						{
							Debug.LogError("[SnipeTablesPreloader] Failed to save - " + table_name + " - " + ex.ToString());
#if UNITY_CLOUD_BUILD
							throw new BuildFailedException($"Failed to save table - {table_name}");
#endif
						}
					}
				}
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

	[System.Serializable]
	internal class TablesListResponseListWrapper
	{
		public List<TablesListResponseListItem> data;
	}

	[System.Serializable]
	internal class TablesListResponseListItem
	{
		public string stringID;
		public string name;
	}

	[System.Serializable]
	internal class ProjectStringIdResponseData
	{
		public string errorCode;
		public string stringID;
	}

}