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
		private DropdownField _versionsDropdown;
		private Button _btnSwitchUpdate;
		private Button _btnUpdateTools;
		private Button _btnFetchVersions;
		public static event Action<PackageCollection> InstalledPackagesListFetched;

		private const string GIT_API_URL = "https://api.github.com/repos/Mini-IT/SnipeUnityPackage/";

		private static ListRequest s_packageListRequest;
		private static AddRequest s_packageAddRequest;

		private static GitHubBranchesListWrapper s_branches;
		private static GitHubTagsListWrapper s_tags;

		public static string[] SnipePackageVersions { get; private set; }
		public static int CurrentSnipePackageVersionIndex { get; private set; } = -1;
		private static int s_selectedSnipePackageVersionIndex;

		[MenuItem("Snipe/Updater...")]
		public static void ShowWindow()
		{
			EditorWindow.GetWindow(typeof(SnipeUpdater));
		}

		private void OnEnable()
		{
			if (s_branches == null || s_tags == null)
			{
				_ = FetchVersionsList();
			}
		}

		public void CreateGUI()
		{
			var root = rootVisualElement;
			UIUtility.LoadUI(root, "SnipeUpdater", "base");

			_btnUpdateTools = root.Q<Button>("update-tools");
			_btnFetchVersions = root.Q<Button>("fetch-versions");
			var statusLabel = root.Q<Label>("status");
            var currentVersionLabel = root.Q<Label>("current-version");
            _versionsDropdown = root.Q<DropdownField>("versions");
			_btnSwitchUpdate = root.Q<Button>("switch-update");

			_btnUpdateTools.clicked += async () =>
			{
				statusLabel.text = "Installing... please wait...";
				SetControlsEnabled(false);

				var request = InstallSnipeToolsPackage();
				while (!request.IsCompleted)
				{
					await Task.Delay(50);
				}

				statusLabel.text = request.Status == StatusCode.Success ? "Installed" : "Install error";
				SetControlsEnabled(true);

				if (request.Status == StatusCode.Success)
				{
					this.Close();
				}
			};

			_btnFetchVersions.clicked += async () =>
			{
				statusLabel.text = "Fetching... please wait...";
				SetControlsEnabled(false);

				await FetchVersionsList();
				statusLabel.text = string.Empty;
				SetControlsEnabled(true);

                RefreshUI(currentVersionLabel, _versionsDropdown);
			};

            _btnSwitchUpdate.clicked += () =>
			{
				if (s_selectedSnipePackageVersionIndex >= 0 && SnipePackageVersions != null)
				{
					string selectedVersion = SnipePackageVersions[s_selectedSnipePackageVersionIndex];
					string versionID = (selectedVersion == "master") ? "" : $"{selectedVersion}";
					InstallSnipePackage(versionID);
				}
			};

            if (_versionsDropdown != null)
            {
                _versionsDropdown.RegisterValueChangedCallback(evt =>
                {
                    var list = _versionsDropdown.choices;
                    s_selectedSnipePackageVersionIndex = (list != null) ? list.IndexOf(evt.newValue) : -1;
                });
            }

            RefreshUI(currentVersionLabel, _versionsDropdown);
		}

        private void RefreshUI(Label currentVersionLabel, DropdownField versionsDropdown)
		{
            if (currentVersionLabel == null || versionsDropdown == null)
            {
                return;
            }

            if (SnipePackageVersions == null)
			{
				currentVersionLabel.text = "unknown";
                versionsDropdown.choices = new List<string>();
                versionsDropdown.value = null;
				return;
			}

			string currentVersionName = CurrentSnipePackageVersionIndex >= 0 ? SnipePackageVersions[CurrentSnipePackageVersionIndex] : "unknown";
			currentVersionLabel.text = currentVersionName;
            var choices = new List<string>(SnipePackageVersions);
            versionsDropdown.choices = choices;
            int selectedIndex = s_selectedSnipePackageVersionIndex;
            if (selectedIndex < 0 || selectedIndex >= choices.Count)
            {
                selectedIndex = choices.Count > 0 ? 0 : -1;
            }
            if (selectedIndex >= 0)
            {
                versionsDropdown.SetValueWithoutNotify(choices[selectedIndex]);
                _btnSwitchUpdate?.SetEnabled(true);
            }
            else
            {
                versionsDropdown.value = null;
                _btnSwitchUpdate?.SetEnabled(false);
            }
		}

		internal static void InstallSnipePackage(string version)
		{
			string versionSuffix = string.IsNullOrEmpty(version) ? "" : $"#{version}";
			s_packageAddRequest = Client.Add($"{Packages.SnipeClient.Url}{versionSuffix}");
			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.update += OnEditorUpdate;
		}

		public static AddRequest InstallSnipeToolsPackage()
		{
			return Client.Add($"{Packages.SnipeTools.Url}");
		}

		public static async Task FetchVersionsList()
		{
			s_branches = null;
			s_tags = null;

			UnityEngine.Debug.Log("[SnipeUpdater] GetBranchesList - start");

			// UnityEngine.Debug.Log($"[SnipeUpdater] Fetching brunches list");

			s_branches = await RequestList<GitHubBranchesListWrapper>(GIT_API_URL, "branches");
			s_tags = await RequestList<GitHubTagsListWrapper>(GIT_API_URL, "tags");

			int itemsCount = (s_branches?.items?.Count ?? 0) + (s_tags?.items?.Count ?? 0);
			SnipePackageVersions = new string[itemsCount];
			s_selectedSnipePackageVersionIndex = 0;

			int i = 0;
			if (s_branches?.items != null)
			{
				foreach (var item in s_branches.items)
				{
					// UnityEngine.Debug.Log($"[SnipeUpdater] {item.name}");
					SnipePackageVersions[i++] = item.name;
				}
			}

			// UnityEngine.Debug.Log($"[SnipeUpdater] Fetching tags list");

			if (s_tags?.items != null)
			{
				foreach (var item in s_tags.items)
				{
					// UnityEngine.Debug.Log($"[SnipeUpdater] {item.name}");
					SnipePackageVersions[i++] = item.name;
				}
			}

			UnityEngine.Debug.Log("[SnipeUpdater] GetBranchesList - done");

			UnityEngine.Debug.Log("[SnipeUpdater] Check installed packages");

			FetchInstalledPackagesList();

			while (s_packageListRequest != null)
			{
				await Task.Delay(100);
			}
		}

		internal static void FetchInstalledPackagesList()
		{
			if (s_packageListRequest != null)
				return;

			s_packageListRequest = UnityEditor.PackageManager.Client.List(false, false);
			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.update += OnEditorUpdate;
		}

		internal static async Task<WrapperType> RequestList<WrapperType>(string gitBaseURL, string urlSuffix) where WrapperType : new()
		{
			UnityEngine.Debug.Log("[SnipeUpdater] RequestList - start - " + urlSuffix);

			var listWrapper = new WrapperType();

			using (var webClient = new HttpClient())
			{
				webClient.DefaultRequestHeaders.UserAgent.ParseAdd("SnipeUpdater");
				var response = await webClient.GetAsync($"{gitBaseURL}{urlSuffix}");
				var content = await response.Content.ReadAsStringAsync();

				// UnityEngine.Debug.Log($"[SnipeUpdater] {content}");

				UnityEditor.EditorJsonUtility.FromJsonOverwrite("{\"items\":" + content + "}", listWrapper);
			}

			UnityEngine.Debug.Log("[SnipeUpdater] RequestList - done");
			return listWrapper;
		}

		private static void OnEditorUpdate()
		{
			if (s_packageListRequest != null)
			{
				if (s_packageListRequest.IsCompleted)
				{
					if (s_packageListRequest.Status == StatusCode.Success)
					{
						OnInstalledPackagesListFetched(s_packageListRequest.Result);
					}
					else if (s_packageListRequest.Status >= StatusCode.Failure)
					{
						Debug.Log($"[SnipeUpdater] Failed to get installed packages list: {s_packageListRequest.Error.message}");
					}

					s_packageListRequest = null;
					EditorApplication.update -= OnEditorUpdate;
				}
			}
			else if (s_packageAddRequest != null)
			{
				if (s_packageAddRequest.IsCompleted)
				{
					if (s_packageAddRequest.Status == StatusCode.Success)
						Debug.Log("[SnipeUpdater] Installed: " + s_packageAddRequest.Result.packageId);
					else if (s_packageAddRequest.Status >= StatusCode.Failure)
						Debug.Log($"[SnipeUpdater] Installed error: {s_packageAddRequest.Error.message}");

					s_packageAddRequest = null;
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
						string packageVersion = item.packageId.Substring(index + ".git#".Length);
						CurrentSnipePackageVersionIndex = s_selectedSnipePackageVersionIndex = Array.IndexOf(SnipePackageVersions, packageVersion);
					}
					else
					{
						CurrentSnipePackageVersionIndex = s_selectedSnipePackageVersionIndex = Array.IndexOf(SnipePackageVersions, item.version);
					}
					break;
				}
			}

			InstalledPackagesListFetched?.Invoke(installedPackages);
		}

		private void SetControlsEnabled(bool enabled)
		{
			_btnUpdateTools?.SetEnabled(enabled);
			_btnFetchVersions?.SetEnabled(enabled);
			_btnSwitchUpdate?.SetEnabled(enabled);
			_versionsDropdown?.SetEnabled(enabled);
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
