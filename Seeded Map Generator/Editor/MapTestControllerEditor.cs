using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MapTestController))]
public class MapTestControllerEditor : Editor
{
    MapTestController mapTestController;

    private void OnEnable()
    {
        mapTestController = target as MapTestController;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();//Draws default inspector

        DrawButton("Generate New Map");
        DrawButton("Test Generation");
        DrawButton("Showcase Generation");
    }

    private void DrawButton(string name)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(name))
        {
            mapTestController.Invoke(name.Replace(" ", ""), 0f);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }
}