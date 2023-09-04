using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// This is just a small class created to hold data to generate with
/// It's not required to use but is helpful
/// </summary>
[CreateAssetMenu(fileName = "Tileset", menuName = "Map Generation/Tileset")]
public class Tileset : ScriptableObject
{
    [SerializeField] private Color cameraBackground;
    public Color CameraBackground => cameraBackground;
    [SerializeField] private TileBase background;
    public TileBase Background => background;
    [SerializeField] private TileBase collision;
    public TileBase Collision => collision;
    [SerializeField] private TileBase underside;
    public TileBase Underside => underside;
    [SerializeField] private TileBase foreground;
    public TileBase Foreground => foreground;
    [SerializeField] private List<GameObject> decoreObjects;
    public List<GameObject> DecoreObjects => decoreObjects;
}