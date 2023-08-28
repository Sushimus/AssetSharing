using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MapManager))]
public class MapManagerEditor : Editor
{
    MapManager mapManager;

    private void OnEnable()
    {
        mapManager = target as MapManager;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();//Draws default inspector

        DrawButton("Generate Map");
        DrawButton("Reset Nodes");
        DrawButton("Regenerate Background");
        DrawButton("Regenerate Collision");
        DrawButton("Regenerate Foreground");
    }

    private void DrawButton(string name)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(name))
        {
            mapManager.Invoke(name.Replace(" ", ""), 0f);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }
}