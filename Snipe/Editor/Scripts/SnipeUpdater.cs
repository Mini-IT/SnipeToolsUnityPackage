#if UNITY_EDITOR

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

public class SnipeUpdater : EditorWindow
{
	private const string API_BASE_URL = "https://api.github.com/repos/Mini-IT/SnipeUnityPackage/";
	private const string PACKAGE_BASE_URL = "https://github.com/Mini-IT/SnipeUnityPackage.git";
	private const string PACKAGE_NAME = "com.miniit.snipe.client";

	private static ListRequest mPackageListRequest;
	private static AddRequest mPackageAddRequest;

	private static GitHubBranchesListWrapper mBranches;
	private static GitHubTagsListWrapper mTags;

	private static string[] mPackageVersions;
	private static int mCurrentVersionIndex = -1;
	private static int mSelectedVersionIndex;

	[MenuItem("Snipe/Updater")]
	public static void ShowWindow()
	{
		EditorWindow.GetWindow(typeof(SnipeUpdater));
	}
	
	private void OnEnable()
	{
		if (mBranches == null || mTags == null)
		{
			FetchBranchesList();
		}
	}

	void OnGUI()
	{
		if (mPackageListRequest != null || mBranches == null || mTags == null || mPackageVersions == null)
		{
			EditorGUILayout.LabelField("Fetching... please wait...");
		}
		if (mPackageAddRequest != null)
		{
			EditorGUILayout.LabelField("Installing... please wait...");
		}
		else
		{
			if (GUILayout.Button("Refresh"))
			{
				FetchBranchesList();
			}

			if (mPackageVersions != null)
			{
				EditorGUILayout.LabelField($"Current version (detected): {(mCurrentVersionIndex >= 0 ? mPackageVersions[mCurrentVersionIndex] : "unknown")}");
				
				GUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Version: ");
				mSelectedVersionIndex = EditorGUILayout.Popup(mSelectedVersionIndex, mPackageVersions);
				
				GUILayout.FlexibleSpace();
				if (mSelectedVersionIndex >= 0 && GUILayout.Button("SWITCH"))
				{
					string selected_vesion = mPackageVersions[mSelectedVersionIndex];
					string version_suffix = (selected_vesion == "master") ? "" : $"#{selected_vesion}";

					mPackageAddRequest = Client.Add($"{PACKAGE_BASE_URL}{version_suffix}");
					EditorApplication.update -= OnEditorUpdate;
					EditorApplication.update += OnEditorUpdate;
				}
				GUILayout.EndHorizontal();
			}
		}
	}

	// [MenuItem("Snipe/Fetch Branches List")]
	public static async Task FetchBranchesList()
	{
		mBranches = null;
		mTags = null;

		UnityEngine.Debug.Log("[SnipeUpdater] GetBranchesList - start");
		
		// UnityEngine.Debug.Log($"[SnipeUpdater] Fetching brunches list");
		
		mBranches = await RequestList<GitHubBranchesListWrapper>("branches");
		mTags = await RequestList<GitHubTagsListWrapper>("tags");

		int items_count = (mBranches?.items?.Count ?? 0) + (mTags?.items?.Count ?? 0);
		mPackageVersions = new string[items_count];
		mSelectedVersionIndex = 0;

		int i = 0;
		if (mBranches?.items != null)
		{
			foreach (var item in mBranches.items)
			{
				// UnityEngine.Debug.Log($"[SnipeUpdater] {item.name}");
				mPackageVersions[i++] = item.name;
			}
		}
		
		// UnityEngine.Debug.Log($"[SnipeUpdater] Fetching tags list");
		
		if (mTags?.items != null)
		{
			foreach (var item in mTags.items)
			{
				// UnityEngine.Debug.Log($"[SnipeUpdater] {item.name}");
				mPackageVersions[i++] = item.name;
			}
		}
		
		UnityEngine.Debug.Log("[SnipeUpdater] GetBranchesList - done");

		UnityEngine.Debug.Log("[SnipeUpdater] Check installed packages");

		mPackageListRequest = UnityEditor.PackageManager.Client.List(false, false);
		EditorApplication.update -= OnEditorUpdate;
		EditorApplication.update += OnEditorUpdate;
	}
	
	private static async Task<WrapperType> RequestList<WrapperType>(string url_suffix) where WrapperType : new()
	{
		UnityEngine.Debug.Log("[SnipeUpdater] RequestList - start - " + url_suffix);
		
		var list_wrapper = new WrapperType();
		
		using (var web_client = new HttpClient())
		{
			web_client.DefaultRequestHeaders.UserAgent.ParseAdd("SnipeUpdater");
			var response = await web_client.GetAsync($"{API_BASE_URL}{url_suffix}");
			var content = await response.Content.ReadAsStringAsync();
			
			// UnityEngine.Debug.Log($"[SnipeUpdater] {content}");
			
			UnityEditor.EditorJsonUtility.FromJsonOverwrite("{\"items\":" + content + "}", list_wrapper);
		}
		
		UnityEngine.Debug.Log("[SnipeUpdater] RequestList - done");
		return list_wrapper;
	}

	private static void OnEditorUpdate()
	{
		if (mPackageListRequest != null)
		{
			if (mPackageListRequest.IsCompleted)
			{
				if (mPackageListRequest.Status == StatusCode.Success)
				{
					foreach (var item in mPackageListRequest.Result)
					{
						if (item.name == PACKAGE_NAME)
						{
							UnityEngine.Debug.Log($"[SnipeUpdater] found package: {item.name} {item.version} {item.packageId}");

							int index = item.packageId.LastIndexOf(".git#");
							if (index > 0)
							{
								string package_version = item.packageId.Substring(index + ".git#".Length);
								mCurrentVersionIndex = mSelectedVersionIndex = Array.IndexOf(mPackageVersions, package_version);
							}
							else
							{
								mCurrentVersionIndex = mSelectedVersionIndex = Array.IndexOf(mPackageVersions, item.version);
							}
							break;
						}
					}
				}
				else if (mPackageListRequest.Status >= StatusCode.Failure)
				{
					Debug.Log($"[SnipeUpdater] Search failed : {mPackageListRequest.Error.message}");
				}

				mPackageListRequest = null;
				EditorApplication.update -= OnEditorUpdate;
			}
		}
		else if (mPackageAddRequest != null)
		{
			if (mPackageAddRequest.IsCompleted)
			{
				if (mPackageAddRequest.Status == StatusCode.Success)
					Debug.Log("[SnipeUpdater] Installed: " + mPackageAddRequest.Result.packageId);
				else if (mPackageAddRequest.Status >= StatusCode.Failure)
					Debug.Log($"[SnipeUpdater] Installed error: {mPackageAddRequest.Error.message}");

				mPackageAddRequest = null;
				EditorApplication.update -= OnEditorUpdate;
			}
		}
		else
		{
			EditorApplication.update -= OnEditorUpdate;
		}
	}
}

#pragma warning disable 0649

[System.Serializable]
internal class GitHubTagsListWrapper
{
	public List<GitHubTagsListItem> items;
}

[System.Serializable]
internal class GitHubBranchesListWrapper
{
	public List<GitHubBranchesListItem> items;
}

[System.Serializable]
internal class GitHubTagsListItem
{
	public string name;
	public string node_id;
	// public GitHubCommitData commit;
}

[System.Serializable]
internal class GitHubBranchesListItem
{
	public string name;
	public bool @projected;
	// public GitHubCommitData commit;
}

//[System.Serializable]
//internal class GitHubCommitData
//{
//	public int sha;
//	public string url;
//}

#pragma warning restore 0649

#endif // UNITY_EDITOR