using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

[CustomEditor(typeof(AssetScriptImporter))]
[CanEditMultipleObjects]
public class ScriptedAssetImporterEditor : ScriptedImporterEditor
{
    const float MinEditorHeight = 100;

    string m_Text;
    bool m_Mixed;
    bool m_Modified;

    float m_EditorHeight = 300;
    Vector2 m_DragStart;
    float m_DragStartHeight;

    float EditorHeight
    {
        get
        {
            return m_EditorHeight;
        }
        set
        {
            m_EditorHeight = value;
            EditorUserSettings.SetConfigValue(nameof(ScriptedAssetImporterEditor) + "." + nameof(EditorHeight), m_EditorHeight.ToString());
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();

        if (float.TryParse(EditorUserSettings.GetConfigValue(nameof(ScriptedAssetImporterEditor) + "." + nameof(EditorHeight)), out var value))
        {
            m_EditorHeight = value;
        }

        ReloadSource();
    }

    void ReloadSource()
    {
        m_Text = null;
        m_Mixed = false;

        foreach (AssetScriptImporter obj in targets)
        {
            var src = File.ReadAllText(obj.assetPath);

            if (m_Text != null && m_Text != src)
            {
                m_Mixed = true;
            }

            m_Text = src;
        }
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.showMixedValue = m_Mixed;

        EditorGUI.BeginChangeCheck();

        m_Text = EditorGUILayout.TextArea(m_Text, GUILayout.Height(EditorHeight));

        if (EditorGUI.EndChangeCheck())
        {
            m_Modified = true;
            m_Mixed = false;
        }

        var dragRect = GUILayoutUtility.GetRect(Screen.width, 4);
        EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.ResizeVertical);

        var dragAreaID = GUIUtility.GetControlID(FocusType.Passive);

        if (Event.current.type == EventType.MouseDown)
        {
            if (dragRect.Contains(Event.current.mousePosition))
            {
                m_DragStart = Event.current.mousePosition;
                m_DragStartHeight = EditorHeight;
                GUIUtility.hotControl = dragAreaID;
                Event.current.Use();
            }
        }
        else if (GUIUtility.hotControl == dragAreaID)
        {
            if (Event.current.type == EventType.MouseDrag)
            {
                EditorHeight = Mathf.Max(m_DragStartHeight + (Event.current.mousePosition.y - m_DragStart.y), MinEditorHeight);
                Event.current.Use();
            }
            else if (Event.current.type == EventType.Ignore || Event.current.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                Event.current.Use();
            }
        }

        ApplyRevertGUI();
    }

    protected override void Apply()
    {
        m_Modified = false;

        AssetDatabase.StartAssetEditing();

        foreach (AssetScriptImporter obj in targets)
        {
            File.WriteAllText(obj.assetPath, m_Text);
        }

        AssetDatabase.StopAssetEditing();
    }

    protected override void ResetValues()
    {
        m_Modified = false;
        ReloadSource();
    }

    public override bool HasModified()
    {
        return m_Modified;
    }
}
