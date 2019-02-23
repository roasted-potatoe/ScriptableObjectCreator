using System;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjectCreator.Editor
{
	public class ScriptableObjectCreatorItem
	{
		public string m_Name;
		public MonoScript m_Script = null;
		public ScriptableObjectCreatorItem m_Parent = null;

		public Action m_Clicked = null;
		public Action m_ClickedAsHeader = null;

		private static readonly string LEFT_ARROW = ((char)9664).ToString();
		private static readonly string RIGHT_ARROW = ((char)9654).ToString();
		private static GUIStyle m_DrawItem = null;

		private static GUIStyle drawItem => m_DrawItem ?? (m_DrawItem = new GUIStyle((GUIStyle)"ToolbarButtonFlat")
		{
			alignment = TextAnchor.MiddleLeft,
			fixedHeight = EditorGUIUtility.singleLineHeight,
		});

		private static GUIStyle m_DrawHeader;

		private static GUIStyle drawHeader => m_DrawHeader ?? (m_DrawHeader = new GUIStyle((GUIStyle)"toolbarbutton"));

		public bool DrawItem(bool focus)
		{
			GUIContent content;
			if (m_Script != null)
				content = new GUIContent(m_Name, AssetPreview.GetMiniThumbnail(m_Script));
			else
				content = new GUIContent(RIGHT_ARROW + m_Name);

			Rect rect = GUILayoutUtility.GetRect(content, drawItem, GUILayout.ExpandWidth(true));
			bool isOver = rect.Contains(Event.current.mousePosition);

			switch (Event.current.type)
			{
				case EventType.Repaint:
					drawItem.Draw(rect, content, false, false, focus, focus);
					break;
				case EventType.MouseDown when isOver:
					Event.current.Use();
					m_Clicked?.Invoke();
					break;
			}

			return isOver || focus;
		}

		public bool DrawAsHeader()
		{
			GUIContent content;
			if (m_Parent != null)
				content = new GUIContent(LEFT_ARROW + m_Name);
			else
				content = new GUIContent(m_Name);

			Rect rect = GUILayoutUtility.GetRect(content, drawHeader, GUILayout.ExpandWidth(true));
			bool focus = rect.Contains(Event.current.mousePosition) && m_Parent != null;

			switch (Event.current.type)
			{
				case EventType.Repaint:
					drawHeader.Draw(rect, content, false, false, focus, focus);
					break;
				case EventType.MouseDown when focus:
					Event.current.Use();
					m_ClickedAsHeader?.Invoke();
					break;
			}

			return focus;
		}
	}
}