#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MiniIT.Snipe.Unity.Editor
{
	public class AuthKeyWidget : VisualElement
	{
		private TextField _authKeyField;
		private Label _projectStringIdLabel;
		private Button _fetchProjectStringIdButton;
		private VisualElement _authErrorContainer;

		public new class UxmlFactory : UxmlFactory<AuthKeyWidget, UxmlTraits> { }
		public new class UxmlTraits : VisualElement.UxmlTraits { }

		public AuthKeyWidget()
		{
			var tree = UIUtility.LoadUxml("AuthKeyWidget");
			if (tree != null)
			{
				tree.CloneTree(this);
			}

			var style = UIUtility.LoadStyleSheet("AuthKeyWidget");
			if (style != null)
			{
				styleSheets.Add(style);
			}

			_authKeyField = this.Q<TextField>("auth-key");
			_projectStringIdLabel = this.Q<Label>("project-string-id");
			_fetchProjectStringIdButton = this.Q<Button>("fetch-psid");
			_authErrorContainer = this.Q<VisualElement>("auth-error");

			SnipeToolsConfig.Load();

			if (_authKeyField != null)
			{
				_authKeyField.isDelayed = true;
				_authKeyField.value = SnipeToolsConfig.AuthKey ?? string.Empty;
				_authKeyField.RegisterValueChangedCallback(evt =>
				{
					if (evt.newValue == SnipeToolsConfig.AuthKey)
					{
						return;
					}

					if (SnipeToolsConfig.TrySetAuthKey(evt.newValue))
					{
						SnipeToolsConfig.Save();
						UpdateProjectStringId();
					}
					UpdateAuthState();
				});
			}

			if (_fetchProjectStringIdButton != null)
			{
				_fetchProjectStringIdButton.clicked += () =>
				{
					string psid = SnipeToolsConfig.GetProjectStringID(true);
					if (!string.IsNullOrEmpty(psid))
					{
						SnipeToolsConfig.Save();
					}
					UpdateProjectStringId();
					UpdateAuthState();
				};
			}

			UpdateProjectStringId();
			UpdateAuthState();
		}

		private void UpdateAuthState()
		{
			bool isValid = SnipeToolsConfig.IsAuthKeyValid;
			if (_authErrorContainer != null)
			{
				_authErrorContainer.style.display = isValid ? DisplayStyle.None : DisplayStyle.Flex;
			}
			if (_fetchProjectStringIdButton != null)
			{
				string psid = SnipeToolsConfig.GetProjectStringID(false);
				bool displayFetchButton = isValid && string.IsNullOrEmpty(psid);
				_fetchProjectStringIdButton.style.display = displayFetchButton ? DisplayStyle.Flex : DisplayStyle.None;
			}
		}

		private void UpdateProjectStringId()
		{
			if (_projectStringIdLabel != null)
			{
				string psid = SnipeToolsConfig.GetProjectStringID(false) ?? string.Empty;
				_projectStringIdLabel.text = "Project String ID: " + psid;
			}
		}
	}
}

#endif


