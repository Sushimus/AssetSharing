using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MapTestController : MonoBehaviour
{
    public static MapTestController Instance;

    [SerializeField] bool defaultTesting;

    [Header("Stats")]
    [SerializeField] private string seed;
    [Range(1, 4)]
    [SerializeField] private int segmentCount = 3;
    [SerializeField] private bool advancedOptions = false;
    [Range(1, 5)]
    [SerializeField] private int segmentLength = 3;
    [Range(1, 15)]
    [SerializeField] private int scalingFactor = 7;
    [Range(0f, 1f)]
    [SerializeField] private float segmentWidth = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] private float bendiness = 0.5f;

    [Header("Overrides")]
    [SerializeField] private bool overrideValues = false;
    [SerializeField] private int overrideSegmentCount = 10;
    [SerializeField] private int overrideSegmentLength = 7;
    [SerializeField] private int overrideScalingFactor = 20;
    [SerializeField] private float overrideSegmentWidth = 2f;
    [SerializeField] private float overrideBendiness = 2f;

    [Header("Options")]
    [SerializeField] bool optionsEnabled = false;
    [SerializeField] bool allowUnderside = false;
    [SerializeField] bool allowIslands = false;
    [SerializeField] bool allowNoise = false;
    [SerializeField] bool allowCollisionOverlap = false;
    [SerializeField] int undersideDepth = 0;
    [SerializeField] Vector4 collisionDepths = new Vector4(0f, 0f, 0f, 0f);

    [Header("References")]
    [SerializeField] private MapManager mapManager;
    [SerializeField] private Tileset tileset;
    [SerializeField] private List<Tileset> tilesets;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (!overrideValues) { CameraController.Instance.Init(new Vector2(segmentLength * scalingFactor * 1.5f, segmentLength * scalingFactor * 1.5f)); }
        else { CameraController.Instance.Init(new Vector2(overrideSegmentLength * overrideScalingFactor * 1.5f, overrideSegmentLength * overrideScalingFactor * 1.5f)); }

        if (tileset != null && tileset.CameraBackground != null) { FindObjectOfType<Camera>().backgroundColor = tileset.CameraBackground; }

        GenerateNewMap();
    }

    private bool RandomBoolean(int index = 0) => Convert.ToBoolean(RandomInteger(index) % 2);
    private int RandomInteger(int index = 0) => (int)char.GetNumericValue(index.ToString(), Math.Abs(index) % (index.ToString().Length - 1));
    public void ShowcaseGeneration()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.EnterPlaymode(); }
        else if (0 < tilesets.Count)
        {
            int randomSeed = Math.Abs(DateTime.Now.Millisecond.ToString().GetHashCode());
            int randomIndex = randomSeed % tilesets.Count;
            if (tilesets[randomIndex] != null && tilesets[randomIndex].CameraBackground != null) { FindObjectOfType<Camera>().backgroundColor = tilesets[randomIndex].CameraBackground; }

            bool tileHasUnderside = (tilesets[randomIndex].Underside != null) ? RandomBoolean(randomSeed + 3) : false;
            int tileUndersideDepth = (tileHasUnderside) ? (1 + RandomInteger(randomSeed)) / 2 : -1;

            Vector4 tileCollisionDepths = (tilesets[randomIndex].Underside != null) ? new Vector4((1f + RandomInteger(randomSeed)) / 2f, (1f + RandomInteger(randomSeed + 1)) / 2f, (1f + RandomInteger(randomSeed + 2)) / 2f, (1f + RandomInteger(randomSeed + 3)) / 2f) : new Vector4();
            tileCollisionDepths.x = Mathf.CeilToInt(tileCollisionDepths.x);
            tileCollisionDepths.y = Mathf.CeilToInt(tileCollisionDepths.y);
            tileCollisionDepths.z = Mathf.CeilToInt(tileCollisionDepths.z);
            tileCollisionDepths.w = Mathf.CeilToInt(tileCollisionDepths.w);

            mapManager.TilesInit(tilesets[randomIndex].Background, tilesets[randomIndex].Collision, tilesets[randomIndex].Underside, tilesets[randomIndex].Foreground);
            mapManager.Init($"{randomSeed}", -1, -1, -1, -1f, -1f, false);
            mapManager.OptionsInit(RandomBoolean(randomSeed), RandomBoolean(randomSeed + 1), RandomBoolean(randomSeed + 2), tileHasUnderside, tileUndersideDepth, tileCollisionDepths);

            mapManager.GenerateMap();
        }
        else
        {
            Debug.LogWarning("No Tilesets To Reference!");
        }
    }

    public void GenerateNewMap()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.EnterPlaymode(); }
        else
        {
            if (tileset != null) { mapManager.TilesInit(tileset.Background, tileset.Collision, tileset.Underside, tileset.Foreground); }

            if (defaultTesting) { mapManager.Init(); }
            else
            {
                if (advancedOptions && overrideValues)
                {
                    if (optionsEnabled)
                    {
                        mapManager.Init(seed, overrideSegmentCount, overrideSegmentLength, overrideScalingFactor, overrideSegmentWidth, overrideBendiness, false);
                        mapManager.OptionsInit(allowCollisionOverlap, allowNoise, allowIslands, allowUnderside, undersideDepth, collisionDepths);
                    }
                    else { mapManager.Init(seed, overrideSegmentCount, overrideSegmentLength, overrideScalingFactor, overrideSegmentWidth, overrideBendiness); }
                }
                else if (advancedOptions)
                {
                    if (optionsEnabled)
                    {
                        mapManager.Init(seed, segmentCount, segmentLength, scalingFactor, segmentWidth, bendiness, false);
                        mapManager.OptionsInit(allowCollisionOverlap, allowNoise, allowIslands, allowUnderside, undersideDepth, collisionDepths);
                    }
                    else { mapManager.Init(seed, segmentCount, segmentLength, scalingFactor, segmentWidth, bendiness); }
                }
                else
                {
                    if (optionsEnabled)
                    {
                        mapManager.Init(seed, segmentCount, -1, -1, -1f, -1f, false);
                        mapManager.OptionsInit(allowCollisionOverlap, allowNoise, allowIslands, allowUnderside, undersideDepth, collisionDepths);
                    }
                    else { mapManager.Init(seed, segmentCount); }
                }
            }

            mapManager.GenerateMap();
        }
    }

    public void TestGeneration()
    {
        for (int i = 0; i < 100; i++)
        {
            GenerateNewMap();
        }
    }
}
