#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace MiniIT.Snipe.Unity.Editor
{
	public class SnipeUpdater : EditorWindow
	{
		public static event Action<PackageCollection> InstalledPackagesListFetched;

		internal const string GIT_API_URL = "https://api.github.com/repos/Mini-IT/SnipeUnityPackage/";

		private static ListRequest mPackageListRequest;
		private static AddRequest mPackageAddRequest;

		private static GitHubBranchesListWrapper mBranches;
		private static GitHubTagsListWrapper mTags;

		public static string[] SnipePackageVersions { get; private set; }
		public static int CurrentSnipePackageVersionIndex { get; private set; } = -1;
		private static int mSelectedSnipePackageVersionIndex;

		[MenuItem("Snipe/Updater...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow(typeof(SnipeUpdater));
		}

		private void OnEnable()
		{
			if (mBranches == null || mTags == null)
			{
				_ = FetchVersionsList();
			}
		}

		public void CreateGUI()
		{
			var root = rootVisualElement;
			UIUtility.LoadUI(root, "SnipeUpdater", "base");

			var btnUpdateTools = root.Q<Button>("update-tools");
			var btnFetchVersions = root.Q<Button>("fetch-versions");
			var statusLabel = root.Q<Label>("status");
			var currentVersionLabel = root.Q<Label>("current-version");
			var versionsPopup = root.Q<PopupField<string>>("versions");
			var btnSwitchUpdate = root.Q<Button>("switch-update");

			btnUpdateTools.clicked += async () =>
			{
				statusLabel.text = "Installing... please wait...";
				var request = InstallSnipeToolsPackage();
				while (!request.IsCompleted)
				{
					await Task.Delay(50);
				}
				statusLabel.text = request.Status == StatusCode.Success ? "Installed" : "Install error";
				if (request.Status == StatusCode.Success)
				{
					this.Close();
				}
			};

			btnFetchVersions.clicked += async () =>
			{
				statusLabel.text = "Fetching... please wait...";
				await FetchVersionsList();
				statusLabel.text = string.Empty;
				RefreshUI(currentVersionLabel, versionsPopup);
			};

			btnSwitchUpdate.clicked += () =>
			{
				if (mSelectedSnipePackageVersionIndex >= 0 && SnipePackageVersions != null)
				{
					string selected_version = SnipePackageVersions[mSelectedSnipePackageVersionIndex];
					string version_id = (selected_version == "master") ? "" : $"{selected_version}";
					InstallSnipePackage(version_id);
				}
			};

			RefreshUI(currentVersionLabel, versionsPopup);
		}

		private void RefreshUI(Label currentVersionLabel, PopupField<string> versionsPopup)
		{
			if (SnipePackageVersions == null)
			{
				currentVersionLabel.text = "unknown";
				versionsPopup.choices = new List<string>();
				versionsPopup.index = -1;
				return;
			}

			string current_version_name = CurrentSnipePackageVersionIndex >= 0 ? SnipePackageVersions[CurrentSnipePackageVersionIndex] : "unknown";
			currentVersionLabel.text = current_version_name;
			versionsPopup.choices = new List<string>(SnipePackageVersions);
			versionsPopup.index = mSelectedSnipePackageVersionIndex;
			versionsPopup.RegisterValueChangedCallback(evt =>
			{
				mSelectedSnipePackageVersionIndex = versionsPopup.index;
			});
		}

		internal static void InstallSnipePackage(string version)
		{
			string version_suffix = string.IsNullOrEmpty(version) ? "" : $"#{version}";
			mPackageAddRequest = Client.Add($"{Packages.SnipeClient.Url}{version_suffix}");
			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.update += OnEditorUpdate;
		}

		public static AddRequest InstallSnipeToolsPackage()
		{
			return Client.Add($"{Packages.SnipeTools.Url}");
		}

		public static async Task FetchVersionsList()
		{
			mBranches = null;
			mTags = null;

			UnityEngine.Debug.Log("[SnipeUpdater] GetBranchesList - start");

			// UnityEngine.Debug.Log($"[SnipeUpdater] Fetching brunches list");

			mBranches = await RequestList<GitHubBranchesListWrapper>(GIT_API_URL, "branches");
			mTags = await RequestList<GitHubTagsListWrapper>(GIT_API_URL, "tags");

			int items_count = (mBranches?.items?.Count ?? 0) + (mTags?.items?.Count ?? 0);
			SnipePackageVersions = new string[items_count];
			mSelectedSnipePackageVersionIndex = 0;

			int i = 0;
			if (mBranches?.items != null)
			{
				foreach (var item in mBranches.items)
				{
					// UnityEngine.Debug.Log($"[SnipeUpdater] {item.name}");
					SnipePackageVersions[i++] = item.name;
				}
			}

			// UnityEngine.Debug.Log($"[SnipeUpdater] Fetching tags list");

			if (mTags?.items != null)
			{
				foreach (var item in mTags.items)
				{
					// UnityEngine.Debug.Log($"[SnipeUpdater] {item.name}");
					SnipePackageVersions[i++] = item.name;
				}
			}

			UnityEngine.Debug.Log("[SnipeUpdater] GetBranchesList - done");

			UnityEngine.Debug.Log("[SnipeUpdater] Check installed packages");

			FetchInstalledPackagesList();

			while (mPackageListRequest != null)
			{
				await Task.Delay(100);
			}
		}

		internal static void FetchInstalledPackagesList()
		{
			if (mPackageListRequest != null)
				return;

			mPackageListRequest = UnityEditor.PackageManager.Client.List(false, false);
			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.update += OnEditorUpdate;
		}

		internal static async Task<WrapperType> RequestList<WrapperType>(string git_base_url, string url_suffix) where WrapperType : new()
		{
			UnityEngine.Debug.Log("[SnipeUpdater] RequestList - start - " + url_suffix);

			var list_wrapper = new WrapperType();

			using (var web_client = new HttpClient())
			{
				web_client.DefaultRequestHeaders.UserAgent.ParseAdd("SnipeUpdater");
				var response = await web_client.GetAsync($"{git_base_url}{url_suffix}");
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
						OnInstalledPackagesListFetched(mPackageListRequest.Result);
					}
					else if (mPackageListRequest.Status >= StatusCode.Failure)
					{
						Debug.Log($"[SnipeUpdater] Failed to get installed packages list: {mPackageListRequest.Error.message}");
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

		private static void OnInstalledPackagesListFetched(PackageCollection installedPackages)
		{
			foreach (var item in installedPackages)
			{
				if (item.name == Packages.SnipeClient.Name)
				{
					Debug.Log($"[SnipeUpdater] found package: {item.name} {item.version} {item.packageId}");

					int index = item.packageId.LastIndexOf(".git#");
					if (index > 0)
					{
						string package_version = item.packageId.Substring(index + ".git#".Length);
						CurrentSnipePackageVersionIndex = mSelectedSnipePackageVersionIndex = Array.IndexOf(SnipePackageVersions, package_version);
					}
					else
					{
						CurrentSnipePackageVersionIndex = mSelectedSnipePackageVersionIndex = Array.IndexOf(SnipePackageVersions, item.version);
					}
					break;
				}
			}

			InstalledPackagesListFetched?.Invoke(installedPackages);
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

}

#endif // UNITY_EDITOR
