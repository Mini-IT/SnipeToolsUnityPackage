#if UNITY_EDITOR

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public class SnipeApiDownloader : EditorWindow
{
	private static readonly string[] SNIPE_VERSIONS = new string[] { "V5", "V6" };

	private string mProjectId = "1";
	private string mDirectoryPath;
	private string mLogin;
	private string mPassword;
	private string mSnipeVersionSuffix = SNIPE_VERSIONS[0]; //"V5";
	private bool mGetTablesList = true;
	
	private static string mPrefsPrefix;
	
	public static string RefreshPrefsPrefix()
	{
		if (string.IsNullOrEmpty(mPrefsPrefix))
		{
			var hash = System.Security.Cryptography.MD5.Create().ComputeHash(UTF8Encoding.UTF8.GetBytes(Application.dataPath));
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < hash.Length; i++)
			{
				builder.Append(hash[i].ToString("x2"));
			}
			mPrefsPrefix = builder.ToString();
		}
		
		return mPrefsPrefix;
	}

	[MenuItem("Snipe/Download SnipeApi")]
	public static void ShowWindow()
	{
		EditorWindow.GetWindow(typeof(SnipeApiDownloader));
	}

	protected void OnEnable()
	{
		RefreshPrefsPrefix();

		mProjectId = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.project_id", mProjectId);
		mDirectoryPath = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.directory", mDirectoryPath);
		mLogin = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.login", mLogin);
		mPassword = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.password", mPassword);
		mSnipeVersionSuffix = EditorPrefs.GetString($"{mPrefsPrefix}_SnipeApiDownloader.snipe_version_suffix", mSnipeVersionSuffix);

		if (string.IsNullOrEmpty(mDirectoryPath))
			mDirectoryPath = Application.dataPath;
	}

	protected void OnDisable()
	{
		EditorPrefs.SetString($"{mPrefsPrefix}_SnipeApiDownloader.project_id", mProjectId);
		EditorPrefs.SetString($"{mPrefsPrefix}_SnipeApiDownloader.directory", mDirectoryPath);
		EditorPrefs.SetString($"{mPrefsPrefix}_SnipeApiDownloader.login", mLogin);
		EditorPrefs.SetString($"{mPrefsPrefix}_SnipeApiDownloader.password", mPassword);
		EditorPrefs.SetString($"{mPrefsPrefix}_SnipeApiDownloader.snipe_version_suffix", mSnipeVersionSuffix);
	}

	void OnGUI()
	{
		mProjectId = EditorGUILayout.TextField("Project ID", mProjectId);

		GUILayout.BeginHorizontal();
		mDirectoryPath = EditorGUILayout.TextField("Directory", mDirectoryPath);
		if (GUILayout.Button("..."))
		{
			string path = EditorUtility.SaveFolderPanel("Choose location of SnipeApi.cs", mDirectoryPath, "");
			if (!string.IsNullOrEmpty(path))
			{
				mDirectoryPath = path;
			}
		}
		GUILayout.EndHorizontal();

		mLogin = EditorGUILayout.TextField("Login", mLogin);
		mPassword = EditorGUILayout.PasswordField("Password", mPassword);

		EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(mLogin) || string.IsNullOrEmpty(mPassword));

		mGetTablesList = EditorGUILayout.Toggle("Get tables list", mGetTablesList);

		GUILayout.BeginHorizontal();
		GUILayout.Label("Snipe Version");
		int index = Array.IndexOf(SNIPE_VERSIONS, mSnipeVersionSuffix);
		index = EditorGUILayout.Popup(index, SNIPE_VERSIONS);
		mSnipeVersionSuffix = SNIPE_VERSIONS[index];
		
		if (GUILayout.Button("Download"))
		{
			DownloadSnipeApiAndClose();
		}
		GUILayout.EndHorizontal();
		EditorGUI.EndDisabledGroup();
	}

	private async void DownloadSnipeApiAndClose()
	{
		DownloadSnipeApi();
		await System.Threading.Tasks.Task.Yield();
		if (mGetTablesList)
		{
			await SnipeTablesPreloadHelper.DownloadTablesList();
		}
		this.Close();
	}
	public void DownloadSnipeApi()
	{
		UnityEngine.Debug.Log("DownloadSnipeApi - start");

		Process process = new Process();
		process.StartInfo.WorkingDirectory = Application.dataPath + "/..";
		process.StartInfo.FileName = "curl";
		process.StartInfo.Arguments = $"-s -X POST \"https://edit.snipe.dev/api/v1/auth\" -d \"login={mLogin}&password={mPassword}\"";
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.RedirectStandardOutput = true;
		process.Start();

		StreamReader reader = process.StandardOutput;
		string output = reader.ReadToEnd();

		UnityEngine.Debug.Log("output " + output);

		LoginResponseData response = new LoginResponseData();
		UnityEditor.EditorJsonUtility.FromJsonOverwrite(output, response);
		string token = response.token;
		if (string.IsNullOrEmpty(token))
		{
			UnityEngine.Debug.Log("DownloadSnipeApi - FAILED to get token");
			return;
		}

		process = new Process();
		process.StartInfo.WorkingDirectory = mDirectoryPath;
		process.StartInfo.FileName = "curl";
		process.StartInfo.Arguments = $"-o SnipeApi.cs -H \"Authorization: Bearer {token}\" \"https://edit.snipe.dev/api/v1/project/{mProjectId}/code/unityBindings{mSnipeVersionSuffix}\"";
		process.Start();

		UnityEngine.Debug.Log("DownloadSnipeApi - done");
	}
}

internal class LoginResponseData
{
	#pragma warning disable 0649
	// public string errorCode;
	public string token;
	
	#pragma warning restore 0649
}

#endif // UNITY_EDITOR
