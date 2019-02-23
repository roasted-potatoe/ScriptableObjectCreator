using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjectCreator.Editor
{
	[CustomEditor(typeof(ScriptableObjectCreator))]
	public class ScriptableObjectCreatorEditor : UnityEditor.Editor
	{
		private const string CONTROL_NAME_SEARCH = "ScriptSearch";
		private const string CONTROL_NAME_CREATE = "CreateText";

		private bool m_DrawDropdown;
		private bool m_WaitUp;
		private int m_CurrentSelected = -1;
		private string m_SearchText;
		private string m_CreateText;
		private Vector3 m_Scroll;

		private ScriptableObjectCreatorItem m_RootItem;
		private ScriptableObjectCreatorItem m_SearchItem;
		private ScriptableObjectCreatorItem m_CreateItem;
		private ScriptableObjectCreatorItem m_CurrentItem;
		private readonly List<ScriptableObjectCreatorItem> m_Items = new List<ScriptableObjectCreatorItem>();

		private void OnEnable()
		{
			m_SearchText = EditorPrefs.GetString(target.GetType().ToString() + CONTROL_NAME_SEARCH, string.Empty);
			CreateAllItems();
		}

		public override void OnInspectorGUI()
		{
			// If any change, reset the current selected
			if (m_CurrentSelected != -1 && (m_CurrentSelected < 0 || m_CurrentSelected >= m_Items.Count || !CanShow(m_Items[m_CurrentSelected])))
			{
				m_CurrentSelected = -1;
			}

			ManageInput();

			// Focus to the current input
			if (m_CurrentItem == m_CreateItem)
				EditorGUI.FocusTextInControl(CONTROL_NAME_CREATE);
			else
				EditorGUI.FocusTextInControl(CONTROL_NAME_SEARCH);

			using (new GUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();

				using (new GUILayout.VerticalScope())
				{
					GUIContent title = new GUIContent("Select Scriptable Object");
					Rect rect = GUILayoutUtility.GetRect(title, "AC Button");
					if (GUI.Button(rect, title, "AC Button"))
					{
						m_DrawDropdown = !m_DrawDropdown;
					}
				}

				GUILayout.FlexibleSpace();
			}

			if (!m_DrawDropdown)
				return;

			using (new GUILayout.HorizontalScope())
			{
				GUILayout.Space(Screen.width * 0.15f);

				using (new GUILayout.VerticalScope("box", GUILayout.ExpandWidth(true)))
				{
					DrawDropdown();
				}

				GUILayout.Space(Screen.width * 0.15f);
			}
		}

		private void DrawDropdown()
		{
			DrawSearch();

			bool headerFocused = m_CurrentItem.DrawAsHeader();
			if (headerFocused)
				m_CurrentSelected = -1;

			using (GUILayout.ScrollViewScope scope = new GUILayout.ScrollViewScope(m_Scroll))
			{
				if (m_CurrentItem == m_CreateItem)
				{
					DrawCreate();
				}
				else
				{
					for (int i = 0; i < m_Items.Count; i++)
					{
						ScriptableObjectCreatorItem item = m_Items[i];
						if (!CanShow(item))
							continue;

						bool focused = item.DrawItem(m_CurrentSelected == i);
						if (focused)
							m_CurrentSelected = i;
					}
				}

				m_Scroll = scope.scrollPosition;
			}
		}

		public override bool RequiresConstantRepaint()
		{
			return true;
		}

		private void CreateAllItems()
		{
			// Get all classes that inherit ScriptableObject from the Unity Assemblies, we will ignore our own classes that inherit from those
			List<Type> inheritTypes =
				AppDomain.CurrentDomain.GetAssemblies()
					.Select(x =>
					{
						return x
							.GetTypes()
							.Where(t =>
								!string.IsNullOrEmpty(t.Namespace) &&
								t.Namespace.StartsWith("Unity") &&
								t.IsSubclassOf(typeof(ScriptableObject)) &&
								!t.IsSubclassOf(typeof(UnityEditor.Editor))
							);
					})
					.SelectMany(x => x)
					.ToList();


			// Find all scripts in the project, each script found will be associated with a ScriptableObjectCreatorItem
			List<MonoScript> scripts = AssetDatabase.FindAssets("t:MonoScript")
				.Select(x => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(x)))
				.Where(t =>
					t.GetClass() != null &&
					!t.GetClass().IsAbstract &&
					t.GetClass().IsSubclassOf(typeof(ScriptableObject)) &&
					!inheritTypes.Any(x => t.GetClass().IsSubclassOf(x))
				)
				.ToList();

			m_Items.Clear();

			// Create the Root Item, it will be the first Item selected
			m_RootItem = new ScriptableObjectCreatorItem
			{
				m_Name = "Scriptable Object"
			};

			m_Items.Add(m_RootItem);

			foreach (MonoScript script in scripts)
			{
				ScriptableObjectCreatorItem lastFolder = null;
				string[] hierarchy = GetFolderHierarchy(script.GetClass());
				if (hierarchy == null)
					continue;

				for (int i = 0; i < hierarchy.Length; i++)
				{
					// Check if it already exists, if it does exist just keep going
					ScriptableObjectCreatorItem folder = m_Items.FirstOrDefault(x => x.m_Name == hierarchy[i]);
					if (folder == null)
					{
						// Use the Namespace info to create Folders
						folder = new ScriptableObjectCreatorItem
						{
							m_Name = hierarchy[i],
							m_Parent = i == 0 ? m_RootItem : lastFolder
						};

						folder.m_Clicked = () => { m_CurrentItem = folder; };
						folder.m_ClickedAsHeader = () => { m_CurrentItem = folder.m_Parent; };

						m_Items.Add(folder);
					}

					lastFolder = folder;
				}

				// Create a Script Item
				ScriptableObjectCreatorItem item = new ScriptableObjectCreatorItem
				{
					m_Name = script.GetClass().Name,
					m_Script = script,
					m_Parent = lastFolder ?? m_RootItem
				};

				item.m_Clicked = () => { AddScript(item.m_Script); };

				m_Items.Add(item);
			}

			// Sort all items by Name
			m_Items.Sort((a, b) => a.m_Name.CompareTo(b.m_Name));

			// Create Item that will allow the user to create their own scripts. It will always be at the bottom of the list.
			m_CreateItem = new ScriptableObjectCreatorItem
			{
				m_Name = "New Script",
				m_Parent = m_RootItem
			};

			m_CreateItem.m_Clicked = () =>
			{
				m_CreateItem.m_Parent = m_CurrentItem;
				m_CurrentItem = m_CreateItem;
				m_CreateText = string.IsNullOrEmpty(m_SearchText) ? "NewScriptableObject" : m_SearchText;
				m_SearchText = string.Empty;
			};

			m_CreateItem.m_ClickedAsHeader = () => { m_CurrentItem = m_CurrentItem.m_Parent; };
			m_Items.Add(m_CreateItem);

			// Create Item that will show that the user is currently searching. It doesn't need to be in the items list.
			m_SearchItem = new ScriptableObjectCreatorItem
			{
				m_Name = "Search",
				m_Parent = m_RootItem,
				m_ClickedAsHeader = () =>
				{
					m_SearchText = string.Empty;
					m_CurrentItem = m_RootItem;
					EditorGUI.FocusTextInControl(null);
				}
			};

			m_CurrentItem = m_RootItem;
		}

		private string[] GetFolderHierarchy(Type type)
		{
			AddScriptableObjectMenuAttribute attribute = type.GetCustomAttribute<AddScriptableObjectMenuAttribute>();
			if (attribute != null)
				return attribute.menu?.Split('/');

			if (!string.IsNullOrEmpty(type.Namespace))
				return type.Namespace.Split('.');

			return new string[0];
		}

		private bool CanShow(ScriptableObjectCreatorItem item)
		{
			if (m_CurrentItem == m_SearchItem)
			{
				if ((item.m_Script != null && item.m_Name.ToLower().Contains(m_SearchText.ToLower())) || item == m_CreateItem)
					return true;
			}
			else if (item.m_Parent == m_CurrentItem || item == m_CreateItem)
				return true;

			return false;
		}

		private void AddScript(MonoScript script)
		{
			serializedObject.Update();
			serializedObject.FindProperty("m_Script").objectReferenceValue = script;
			serializedObject.ApplyModifiedProperties();
		}

		private void DrawSearch()
		{
			EditorGUI.BeginChangeCheck();
			using (new GUILayout.HorizontalScope())
			{
				GUI.SetNextControlName(CONTROL_NAME_SEARCH);
				m_SearchText = GUILayout.TextField(m_SearchText, "ToolbarSeachTextField");

				if (GUILayout.Button("", string.IsNullOrEmpty(m_SearchText) ? "ToolbarSeachCancelButtonEmpty" : "ToolbarSeachCancelButton"))
					m_SearchText = "";
			}

			if (!EditorGUI.EndChangeCheck())
				return;

			// If searching for nothing, the Root Item will be the Current Item
			if (string.IsNullOrEmpty(m_SearchText))
			{
				m_CurrentItem = m_RootItem;
				m_CurrentSelected = -1;
			}
			// Else we're searching and the Current Item is the Search Item
			else
			{
				m_CurrentItem = m_SearchItem;
				m_CurrentSelected = 0;
			}

			// Save the Search Text (Unity does this for Add Component so we'll do the same)
			EditorPrefs.SetString(target.GetType().ToString() + CONTROL_NAME_SEARCH, m_SearchText);
		}

		private void DrawCreate()
		{
			using (new GUILayout.VerticalScope())
			{
				GUILayout.Label("Name");
				GUI.SetNextControlName(CONTROL_NAME_CREATE);
				m_CreateText = EditorGUILayout.TextField(m_CreateText);
				GUILayout.Label("Script will be added to ScriptableObject after the project reloads", EditorStyles.miniBoldLabel);

				GUILayout.Space(EditorGUIUtility.singleLineHeight * 1.5f);

				if (!m_WaitUp && Event.current.type == EventType.KeyUp && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
				{
					CreateNewScript(m_CreateText);
				}

				if (Event.current.type == EventType.KeyUp)
				{
					// Because we check for KeyUp, we need to wait till the previous Input has completed.
					m_WaitUp = false;
				}

				if (GUILayout.Button("Create and Add"))
				{
					CreateNewScript(m_CreateText);
				}
			}
		}

		private const string NEW_SCRIPT =
			@"using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class {scriptname} : ScriptableObject
{

}
";

		private void CreateNewScript(string scriptName)
		{
			try
			{
				string newFile = NEW_SCRIPT.Replace("{scriptname}", m_CreateText);
				string path = GetPathNextToTarget(m_CreateText);

				// Write the stream
				StreamWriter writer = File.CreateText(path);
				writer.WriteLine(newFile);
				writer.Close();

				// Refresh, then Get the newly created MonoScript
				AssetDatabase.Refresh();
				MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

				if (script != null)
				{
					AddScript(script);
				}
			}
			catch
			{
				// StreamWriter failed or something like that.
				EditorUtility.DisplayDialog("Error", "Could not create script", "ok");
			}
		}

		private string GetPathNextToTarget(string scriptName)
		{
			string path = AssetDatabase.GetAssetPath(target);
			return Path.Combine(Path.GetDirectoryName(path), scriptName + ".cs");
		}

		private void ManageInput()
		{
			if (Event.current.type != EventType.KeyDown)
				return;

			switch (Event.current.keyCode)
			{
				case KeyCode.DownArrow:
				{
					do
					{
						m_CurrentSelected++;
					}
					while (m_CurrentSelected < m_Items.Count && !CanShow(m_Items[m_CurrentSelected]));

					if (m_CurrentSelected >= m_Items.Count)
					{
						m_CurrentSelected = -1;
					}

					Event.current.Use();
					break;
				}
				case KeyCode.UpArrow:
				{
					do
					{
						m_CurrentSelected--;
					}
					while (m_CurrentSelected >= 0 && !CanShow(m_Items[m_CurrentSelected]));

					if (m_CurrentSelected < -1)
					{
						for (int i = m_Items.Count - 1; i >= 0; i--)
						{
							if (!CanShow(m_Items[i]))
								continue;

							m_CurrentSelected = i;
							break;
						}
					}

					Event.current.Use();
					break;
				}
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
				{
					if (GUI.GetNameOfFocusedControl() != CONTROL_NAME_CREATE)
					{
						if (m_CurrentSelected != -1 && m_Items[m_CurrentSelected].m_Clicked != null)
							m_Items[m_CurrentSelected].m_Clicked();

						Event.current.Use();
						m_WaitUp = true;
					}

					break;
				}
				case KeyCode.Escape:
				{
					m_CurrentItem.m_ClickedAsHeader?.Invoke();

					Event.current.Use();
					break;
				}
			}
		}
	}
}