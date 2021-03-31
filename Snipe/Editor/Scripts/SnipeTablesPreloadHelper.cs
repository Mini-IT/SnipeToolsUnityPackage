using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

public class SnipeTablesPreloadHelper
{
	private static string mPrefsPrefix;

	public static string GetTableListFilePath()
	{
		return Path.Combine(Application.dataPath, "snipe_tables.txt");
	}
	
	public static string GetTablesUrl(string project_string_id)
	{
		return $"https://static-dev.snipe.dev/{project_string_id}/";
	}

	// [MenuItem ("Snipe/Download Tables List")]
	public static async Task DownloadTablesList()
	{
		UnityEngine.Debug.Log("[SnipeTablesPreloadHelper] DownloadResponseList - start");
		
		string token = await RequestAuthToken();
		if (string.IsNullOrEmpty(token))
		{
			UnityEngine.Debug.Log("[SnipeTablesPreloadHelper] - FAILED to get token");
			return;
		}
		
		RefreshPrefsPrefix();
		string project_id = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.project_id");
		string project_string_id = "";
		
		UnityEngine.Debug.Log($"[SnipeTablesPreloadHelper] project id = {project_id}");
		
		if (!string.IsNullOrEmpty(project_id))
		{
			using (var projects_list_client = new HttpClient())
			{
				projects_list_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
				var response = await projects_list_client.GetAsync($"https://edit.snipe.dev/api/v1/projects");
				var content = await response.Content.ReadAsStringAsync();
				
				// UnityEngine.Debug.Log($"[SnipeTablesPreloadHelper] {content}");
				
				var list_wrapper = new ResponseListWrapper();
				UnityEditor.EditorJsonUtility.FromJsonOverwrite(content, list_wrapper);
				if (list_wrapper.data is List<ResponseListItem> list)
				{
					foreach (var item in list)
					{
						if (Convert.ToString(item.id) == project_id)
						{
							project_string_id = item.stringID;
							break;
						}
					}
				}
				
				UnityEngine.Debug.Log($"[SnipeTablesPreloadHelper] Project StringID = {project_string_id}");
			}
			
			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
				var response = await client.GetAsync($"https://edit.snipe.dev/api/v1/project/{project_id}/tableTypes");
				var content = await response.Content.ReadAsStringAsync();
				
				var list_wrapper = new ResponseListWrapper();
				UnityEditor.EditorJsonUtility.FromJsonOverwrite(content, list_wrapper);
				if (list_wrapper.data is List<ResponseListItem> list)
				{
					var file_path = GetTableListFilePath();
					UnityEngine.Debug.Log($"[SnipeTablesPreloadHelper] {file_path}");
					if (File.Exists(file_path))
					{
						FileUtil.DeleteFileOrDirectory(file_path);
					}
					
					using (StreamWriter sw = File.CreateText(file_path))
					{					
						foreach (var item in list)
						{
							string table_name = item.stringID;
							if (!string.IsNullOrEmpty(table_name))
							{
								sw.WriteLine(table_name);
							}
						}
						
						if (!string.IsNullOrEmpty(project_string_id))
						{
							sw.WriteLine(GetTablesUrl(project_string_id));
						}
					}
				}
			}
		}
		
		UnityEngine.Debug.Log("[SnipeTablesPreloadHelper] DownloadResponseList - done");
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
			Debug.Log($"[SnipeTablesPreloadHelper] Failed to auth");
			return null;
		}
		
		string content = loader_response.Content.ReadAsStringAsync().Result;
		Debug.Log(content);
		
		var response = new SnipeAuthLoginResponseData();
		UnityEditor.EditorJsonUtility.FromJsonOverwrite(content, response);
		
		return response.token;
	}
	
	private static void RefreshPrefsPrefix()
	{
		mPrefsPrefix = SnipeApiDownloader.RefreshPrefsPrefix();
	}
}

#pragma warning disable 0649

[System.Serializable]
internal class SnipeAuthLoginResponseData
{
	public string token;
}

[System.Serializable]
internal class ResponseListWrapper
{
	public List<ResponseListItem> data;
}

[System.Serializable]
internal class ResponseListItem
{
	public int id;
	public string stringID;
}

#pragma warning restore 0649
