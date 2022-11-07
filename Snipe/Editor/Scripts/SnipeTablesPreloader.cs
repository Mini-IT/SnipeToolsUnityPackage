using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using MiniIT.Snipe.Editor;
using System.Net.Http.Headers;

public class SnipeTablesPreloader : IPreprocessBuildWithReport
{
	private static string mTablesUrl;
	private static HashSet<string> mTableNames;
	
	private static string mVersion;
	private static string mStreamingAssetsPath;
	
	public int callbackOrder { get { return 10; } }
	public void OnPreprocessBuild(BuildReport report)
	{
		StaticOnPreprocessBuild();
	}
	
	// [MenuItem ("Snipe/Preload TEST")]
	public static void StaticOnPreprocessBuild()
	{
		Load();
		
		Debug.Log("[PreloadSnipeTables] - OnPreprocessBuild finished");
	}
	
	private static string GetTablesVersionFilePath()
	{
		return Path.Combine(Application.streamingAssetsPath, "snipe_tables_version.txt");
	}

	[MenuItem ("Snipe/Preload Tables")]
	public static async void Load()
	{
		Debug.Log("[PreloadSnipeTables] - started");
		
		mStreamingAssetsPath = Application.streamingAssetsPath;

		await DownloadTablesList();

		if (mTableNames == null || mTableNames.Count == 0)
		{
			Debug.Log("[PreloadSnipeTables] - Tables list is empty");
			return;
		}
		
		Debug.Log("[PreloadSnipeTables] Tables count = " + mTableNames.Count);
		
		var files = Directory.EnumerateFiles(mStreamingAssetsPath, "*.jsongz*");
		foreach (string filename in files)
		{
			foreach (string tablename in mTableNames)
			{
				if (filename.Contains($"_{tablename}."))
				{
					Debug.Log("Delete " + tablename);
					FileUtil.DeleteFileOrDirectory(filename);
					break;
				}
			}
		}
		
		string version_file_path = GetTablesVersionFilePath();
		if (File.Exists(version_file_path))
			File.Delete(version_file_path);
		
		Task.Run(async () => { await LoadVersion(); }).Wait(10000);
		
		if (!string.IsNullOrWhiteSpace(mVersion))
		{
			foreach (string tablename in mTableNames)
			{
				Task.Run(async () => { await LoadTable(tablename); }).Wait(10000);
			}
		}
		
        AssetDatabase.Refresh();
		
		Debug.Log("[PreloadSnipeTables] - done");
	}

	public static async Task DownloadTablesList()
	{
		Debug.Log("[SnipeTablesPreloader] DownloadResponseList - start");

		if (string.IsNullOrEmpty(SnipeAuthKey.AuthKey))
			SnipeAuthKey.Load();
		if (string.IsNullOrEmpty(SnipeAuthKey.AuthKey))
		{
			Debug.Log("[SnipeTablesPreloader] - FAILED - invalid AuthKey");
			return;
		}

		string project_string_id = "";

		Debug.Log($"[SnipeTablesPreloader] project id = {SnipeAuthKey.ProjectId}");

		if (SnipeAuthKey.ProjectId > 0)
		{
			Debug.Log($"[SnipeTablesPreloader] Fetching projects list");

			using (var project_string_id_client = new HttpClient())
			{
				project_string_id_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeAuthKey.AuthKey);
				var response = await project_string_id_client.GetAsync($"https://edit.snipe.dev/api/v1/project/{SnipeAuthKey.ProjectId}/stringID");
				var content = await response.Content.ReadAsStringAsync();

				// Debug.Log($"[SnipeTablesPreloader] {content}");

				var response_data = new ProjectStringIdResponseData();
				UnityEditor.EditorJsonUtility.FromJsonOverwrite(content, response_data);
				project_string_id = response_data.stringID;
				Debug.Log($"[SnipeTablesPreloader] Project StringID request errorCode = {response_data.errorCode}");
				Debug.Log($"[SnipeTablesPreloader] Project StringID = {project_string_id}");
			}

			Debug.Log($"[SnipeTablesPreloader] Fetching tables list for project {project_string_id}");

			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SnipeAuthKey.AuthKey);
				var response = await client.GetAsync($"https://edit.snipe.dev/api/v1/project/{SnipeAuthKey.ProjectId}/tableTypes");
				var content = await response.Content.ReadAsStringAsync();

				// Debug.Log($"[SnipeTablesPreloader] {content}");

				var list_wrapper = new TablesListResponseListWrapper();
				UnityEditor.EditorJsonUtility.FromJsonOverwrite(content, list_wrapper);
				if (list_wrapper.data is List<TablesListResponseListItem> list)
				{
					Debug.Log($"[SnipeTablesPreloader] tables count = {list.Count}");

					mTableNames = new HashSet<string>();

					foreach (var item in list)
					{
						if (!item.isPublic)
							continue;

						string table_name = item.stringID;
						if (!string.IsNullOrEmpty(table_name))
						{
							mTableNames.Add(table_name);
						}
					}

					// common tables for all projects
					mTableNames.Add("Items");
					mTableNames.Add("Logic");
					mTableNames.Add("Calendar");
				}
			}
		}

		Debug.Log("[SnipeTablesPreloader] DownloadResponseList - done");
	}

	private static async Task LoadVersion()
	{
		mVersion = "";
		string url = $"{mTablesUrl}/version.txt";
		
		Debug.Log($"[PreloadSnipeTables] LoadVersion from {url}");
		
		try
		{
			var loader = new HttpClient();
			var loader_task = loader.GetAsync(url);

			await loader_task;

			if (loader_task.IsFaulted || loader_task.IsCanceled)
			{
				Debug.LogError("[PreloadSnipeTables] LoadVersion - Failed to load tables version - (loader failed)");
				return;
			}
			
			HttpResponseMessage response = loader_task.Result;
			if (!response.IsSuccessStatusCode)
			{
				Debug.LogError($"[PreloadSnipeTables] LoadVersion - Failed - http error: {response.StatusCode}");
				return;
			}
			
			using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync()))
			{
				mVersion = reader.ReadLine().Trim();
			}
			
			Debug.Log($"[PreloadSnipeTables] Version = {mVersion}");
			
			if (!string.IsNullOrWhiteSpace(mVersion))
			{
				// save to file
				File.WriteAllText(GetTablesVersionFilePath(), mVersion);
			}
		}
		catch (Exception)
		{
			mVersion = "";
			Debug.LogError("[PreloadSnipeTables] LoadVersion - Failed to read tables version");
		}
	}
	
	protected static async Task LoadTable(string table_name)
	{
		string url = $"{mTablesUrl}/{table_name}.json.gz";
		string filename = $"{mVersion}_{table_name}.jsongz";

		// NOTE: There is a bug - only lowercase works
		// (https://issuetracker.unity3d.com/issues/android-loading-assets-from-assetbundles-takes-significantly-more-time-when-the-project-is-built-as-an-aab)
		filename = filename.ToLower();
		
		Debug.Log(filename);
		string cache_path = Path.Combine(mStreamingAssetsPath, filename);
		
		Debug.Log("[PreloadSnipeTables] Loading table " + url);
		
		try
		{
			var loader = new HttpClient();
			var loader_task = loader.GetAsync(url);

			await loader_task;
			
			if (loader_task.IsFaulted || loader_task.IsCanceled || !loader_task.Result.IsSuccessStatusCode)
			{
				Debug.LogError($"[PreloadSnipeTables] Failed to load table - {table_name}   (loader failed)");
				return;
			}
			
			HttpResponseMessage response = loader_task.Result;
			if (!response.IsSuccessStatusCode)
			{
				Debug.LogError($"[PreloadSnipeTables] LoadTable {table_name} - Failed - http error: {response.StatusCode}");
				return;
			}
			
			using (var file_content_stream = await response.Content.ReadAsStreamAsync())
			{
				using (FileStream cache_write_stream = new FileStream(cache_path, FileMode.Create, FileAccess.Write))
				{
					try
					{
						file_content_stream.Position = 0;
						file_content_stream.CopyTo(cache_write_stream);
					}
					catch (Exception ex)
					{
						Debug.LogError("[PreloadSnipeTables] Failed to save - " + table_name + " - " + ex.Message);
					}
				}
			}
		}
		catch (Exception)
		{
			Debug.LogError("[PreloadSnipeTables] Failed to load table - " + table_name);
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
		public int id;
		public string stringID;
		public bool isPublic;
	}

	[System.Serializable]
	internal class ProjectStringIdResponseData
	{
		public string errorCode;
		public string stringID;
	}

}