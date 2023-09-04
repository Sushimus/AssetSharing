using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Assertions;

//TODO Allow for creation of new tiles
//TODO Allow for extension of path
//TODO Allow for alteration of tiles; Some units might be used for this
//--- Also useful for drawing paths
//TODO Allow generation of tile structures
//TODO Allow placement of tile structures

/// <summary>
/// While originally made for RTS/TD application the generation should be robust enough for other uses.
/// 
/// Quick rundown of inner-workings:
/// 1. Initialise parameters; Ensure needed components are present.
/// 2. Generate a list of Vector3Int lists, starting at origin and extending outwards.
/// 3. Fill each named list (background, collision, etc) with Vector3Int based on parameters & locations above.
/// 4. Place a tile of needed type at every location in named lists.
/// NOTE: The tile placement is LAST to happen. Everything until FillMap() is solely Vector3Int lists.
/// 
/// Quick rundown of variables:
/// segmentCount is how many pieces of the map to generate.
/// segmentWidth is self-explainatory. Worth noting however is high segment widths with low length can be used to make the map 'less noodley'
/// bendiness is used in quite a bit of the randomness; Lower bendiness = less chaotic maps
/// SegmentEndLocations is a seperate public variable from mapSegments due to its usefulness.
///     These are the same Vector3Int as mapSegments[Segment][mapSegments[Segment].Count - 1] (remember the map generates outwards, ends are last in the lists)
///     Could be used for anything from locations of interest to player spawn locations (in my case each player of the RTS spawns at a different end)
/// NoiseLocations is a list of all the spots in the background layer where noise has generated
///     Could be useful for placement of non-tile things like trees
///     Be sure to account for the 0.5f distance from tile center due to Vector3Ints being used
/// allowCollisionOverlap is for tilesets where the wall tiles aren't full tiles
/// collisionDepths are how thick walls should be in each direction. Pass in a Vector4(north, east, south, west). Defaults to 1f
/// Remaining variables are self-explainatory
/// NOTE: Values can be made larger than default ranges if you pass an 'illegal' value in. Do at your own risks.
/// 
/// This basic seeded map generation is free for all to use.
/// Created by sushimus | https://github.com/Sushimus
/// </summary>
public class MapManager : MonoBehaviour
{
    [Header("Stats")]//TODO No way to edit in settings menu
    [SerializeField] private int seed;
    [Range(1, 4)]
    [SerializeField] int segmentCount;
    [Range(1, 5)]
    [SerializeField] private int segmentLength;
    [Range(1, 10)]
    [SerializeField] private int scalingFactor;
    [Range(0f, 1f)]
    [SerializeField] private float segmentWidth;
    [Range(0f, 1f)]//Anything above 2f may cause crashes.
    [SerializeField] private float bendiness;
    [SerializeField] private List<List<Vector3Int>> mapSegments;
    public List<Vector3Int> SegmentNodeLocations;
    private List<Vector3Int> segmentEndLocations;
    public List<Vector3Int> SegmentEndLocations => segmentEndLocations;

    [Header("Options")]
    [SerializeField] bool allowUnderside;
    [SerializeField] bool allowIslands;
    [SerializeField] bool allowNoise;
    [SerializeField] bool allowCollisionOverlap;
    [SerializeField] int undersideDepth;
    [SerializeField] Vector4 collisionDepths;

    [Header("DebugOptions")]
    [SerializeField] bool showTiles = true;
    [SerializeField] bool showSkeleton;
    [SerializeField] bool showBackground;
    [SerializeField] bool showCollision;
    [SerializeField] bool showUnderside;
    [SerializeField] bool showForeground;
    [SerializeField] bool showAll;

    [Header("References:")]
    [SerializeField] private TileBase backgroundTile;
    [SerializeField] private Tilemap background;
    private HashSet<Vector3Int> backgroundLocations;
    private HashSet<Vector3Int> backgroundNoise;
    public List<Vector3Int> NoiseLocations => new List<Vector3Int>(backgroundNoise);
    [SerializeField] private TileBase collisionTile;
    [SerializeField] private Tilemap collision;
    private HashSet<Vector3Int> collisionLocations;
    [SerializeField] private TileBase undersideTile;
    private HashSet<Vector3Int> undersideLocations;
    [SerializeField] private TileBase foregroundTile;
    [SerializeField] private Tilemap foreground;
    private HashSet<Vector3Int> foregroundLocations;
    
    /// <summary>
    /// Initialisation function for map creation.
    /// Values default to randoms within my preferred values.
    /// Use -1(f) to use default if only modifying a certain inputs.
    /// </summary>
    public void Init(string seed = "", int segmentCount = -1, int segmentLength = -1, int scalingFactor = -1, float segmentWidth = -1f, float bendiness = -1f, bool defaultOptions = true)
    {
        ComponentCheck();
        if (defaultOptions) { OptionsInit(); }

        this.seed = (seed.Equals("")) ? Math.Abs(DateTime.Now.Millisecond.ToString().GetHashCode()) : Math.Abs(seed.GetHashCode());
        this.segmentCount = (0 <= segmentCount) ? segmentCount : 1 + SeededRandomInteger(this.seed) % 4;
        this.segmentLength = (0 <= segmentLength) ? segmentLength : 1 + SeededRandomInteger(this.seed / 2) % 5;
        this.scalingFactor = (0 <= scalingFactor) ? scalingFactor : 5 + (SeededRandomInteger(this.seed / 3) * 7) % 11;
        this.segmentWidth = (0 <= segmentWidth) ? segmentWidth : 0.5f + ((SeededRandomInteger(this.seed / 4)) / 20f) * SeededRandomNegative(this.seed / 2 + 1);
        this.bendiness = (0 <= bendiness) ? bendiness : 0.5f + ((SeededRandomInteger(this.seed / 5)) / 20f) * SeededRandomNegative(this.seed / 2) + (Mathf.CeilToInt((float)segmentCount / 3) % 2) * (0.34f + ((float)segmentCount % 3) / 3);
        Debug.Log($"<b>Seed: {this.seed} | Stats: ({this.segmentCount})({this.segmentLength})({this.scalingFactor})({this.segmentWidth})({this.bendiness})</b>");
    }

    //Ensures all needed components are present.
    //If you think this is overkill you should've seen how long it took me to realise I was missing a grid component.
    //There is no warning in Unity by default.
    //You're welcome.
    private void ComponentCheck()
    {
        if (FindObjectOfType<Grid>() == null) { Debug.LogWarning("There is no Grid component in the scene!"); }
        if (FindObjectOfType<Tilemap>() == null) { Debug.LogWarning("There are no Tilemaps in the scene!"); }
        if (FindObjectOfType<TilemapRenderer>() == null) { Debug.LogWarning("There are no TilemapRenderers in the scene!"); }

        if (background == null) { Debug.LogWarning("No Tilemap has been assigned to background! This will not generate!"); }
        if (undersideTile == null) { Debug.LogWarning("No tile has been given for the underside tile! This will not generate!"); }
        if (backgroundTile == null) { Debug.LogWarning("No tile has been given for the background tile! This will not generate!"); }
        if (background != null && background.gameObject.GetComponent<Tilemap>() == null) { Debug.LogWarning("Your collision lacks a Tilemap!"); }
        if (background != null && background.gameObject.GetComponent<TilemapRenderer>() == null) { Debug.LogWarning("Your collision lacks a TilemapRenderer!"); }

        if (collision == null) { Debug.LogWarning("No Tilemap has been assigned to collision! This will not generate!"); }
        if (collisionTile == null) { Debug.LogWarning("No tile has been given for the collision tile! This will not generate!"); }
        if (collision != null && collision.gameObject.GetComponent<Tilemap>() == null) { Debug.LogWarning("Your collision lacks a Tilemap!"); }
        if (collision != null && collision.gameObject.GetComponent<TilemapRenderer>() == null) { Debug.LogWarning("Your collision lacks a TilemapRenderer!"); }
        if (collision != null && collision.gameObject.GetComponent<CompositeCollider2D>() == null) { Debug.LogWarning("Your collision lacks a CompositeCollider2D!"); }
        if (collision != null && collision.gameObject.GetComponent<Rigidbody2D>() == null) { Debug.LogWarning("Your collision lacks a Rigidbody2D!"); }
        if (collision != null && collision.gameObject.GetComponent<TilemapCollider2D>() == null) { Debug.LogWarning("Your collision lacks a TilemapCollider2D!"); }

        if (foreground == null) { Debug.LogWarning("No Tilemap has been assigned to foreground! This will not generate!"); }
        if (foregroundTile == null) { Debug.LogWarning("No tile has been given for the foreground tile! This will not generate!"); }
        if (foreground != null && collision.gameObject.GetComponent<Tilemap>() == null) { Debug.LogWarning("Your collision lacks a Tilemap!"); }
        if (foreground != null && collision.gameObject.GetComponent<TilemapRenderer>() == null) { Debug.LogWarning("Your collision lacks a TilemapRenderer!"); }
    }

    /// <summary>
    /// Optional toggles to allow better customised generation.
    /// Values default to my preferred values.
    /// </summary>
    public void OptionsInit(bool allowCollisionOverlap = false, bool allowNoise = true, bool allowIslands = true, bool allowUnderside = true, int undersideDepth = 1, Vector4 collisionDepths = default)
    {
        this.allowUnderside = allowUnderside;
        this.allowIslands = allowIslands;
        this.allowNoise = allowNoise;
        this.allowCollisionOverlap = allowCollisionOverlap;
        this.undersideDepth = undersideDepth;
        this.collisionDepths = (collisionDepths == default) ? new Vector4(1f, 1f, 1f, 1f) : new Vector4((int)collisionDepths.x, (int)collisionDepths.y, (int)collisionDepths.z, (int)collisionDepths.w);
    }

    /// <summary>
    /// Niche function I found useful in testing, mostly useless.
    /// Use null to skip tile type
    /// </summary>
    public void TilesInit(TileBase backgroundTile = null, TileBase collisionTile = null, TileBase undersideTile = null, TileBase foregroundTile = null)
    {
        if (backgroundTile != null) { this.backgroundTile = backgroundTile; }
        if (collisionTile != null) { this.collisionTile = collisionTile; }
        if (undersideTile != null) { this.undersideTile = undersideTile; }
        if (foregroundTile != null) { this.foregroundTile = foregroundTile; }
    }

    public void GenerateMap()
    {
        if (FindObjectOfType<Grid>() != null && FindObjectOfType<Tilemap>() != null && FindObjectOfType<TilemapRenderer>() != null)
        {
            SetNodeLocations();

            if (backgroundTile != null && background != null) { GenerateBackground(); }
            if (collisionTile != null && collision != null) { GenerateCollision(); }
            if (allowUnderside && undersideTile != null && background != null) { GenerateUnderside(); }
            if (foregroundTile != null && foreground != null) { GenerateForeground(); }

            FillMap();
            ExportSegmentLocations();
        }
    }

    public void PlaceStructure(Vector3Int location)
    {
        //TODO This will be added in a later build! A rough implementation is already in the works
        //However as it isn't currently needed this feature will be on hold while I move forwards with other aspects of my project

        //Center of structures is at (1, 1)
    }

    public void ResetNodes() => SetNodeLocations();
    private void SetNodeLocations()
    {
        MapSegmentsInit();
        GenerateMapSegments();
    }

    private void MapSegmentsInit()
    {
        mapSegments = new List<List<Vector3Int>>();
        for (int i = 0; i < segmentCount; i++)
        {
            mapSegments.Add(new List<Vector3Int>());
        }
    }

    //This is janky but basically I'm leveraging Unity's Transform methods instead of doing the trig myself.
    //It spawns in a temporary gameObject, then rotates and moves that object around as needed.
    private void GenerateMapSegments()
    {
        Transform segmentTracer = new GameObject().GetComponent<Transform>();
        segmentEndLocations = new List<Vector3Int>();

        for (int i = 0; i < segmentCount; i++)
        {
            segmentTracer.position = (1 < i) ? Intersection(i) : new Vector3Int();

            int distLeft = 2 * segmentLength * scalingFactor;
            distLeft -= Mathf.FloorToInt(Vector3Int.Distance(Vector3Int.FloorToInt(segmentTracer.position), new Vector3Int()));
            do
            {
                RotatePathTracer(segmentTracer, i, distLeft);
                distLeft = MoveSegmentTracer(segmentTracer, i, distLeft);
            } while (0 < distLeft);
            
            segmentEndLocations.Add(mapSegments[i][mapSegments[i].Count - 1]);
        }

        Destroy(segmentTracer.gameObject);
    }

    private Vector3Int Intersection(int playerCount)
    {
        //Sets intersection based on bendiness, where bendiness determines the chance to intersect on previous player's path
        Vector3Int[] sectLocations = new Vector3Int[10];
        for (int i = 0; i < sectLocations.Length; i++)
        {
            if (i < 10 - Mathf.FloorToInt(bendiness * 10))
            {
                sectLocations[i] = new Vector3Int();
            }
            else
            {
                sectLocations[i] = mapSegments[i % 2][0];
            }
        }

        sectLocations = FisherYateShuffle(sectLocations);

        return sectLocations[SeededRandomInteger(SeededRandomInteger(playerCount))];
    }

    private void RotatePathTracer(Transform pathTracer, int currentPlayer, int distLeft)
    {
        //Random angle based on seed, target player, node count, and bendiness
        float randAngle = SeededRandomSquare(distLeft) % (30f + 15f * (segmentCount - 2)) + (15f + 7.5f * (segmentCount - 2));
        randAngle = (0 < SeededRandomBinary(distLeft)) ? randAngle * -1 : randAngle;
        randAngle *= bendiness;

        //Rotate paths random offset based on bendiness
        randAngle += SeededRandomSquare() * bendiness + SeededRandomSquare();

        //Rotate paths different directions
        randAngle += currentPlayer * (360f / segmentCount);

        pathTracer.rotation = Quaternion.Euler(0f, 0f, randAngle);
    }

    private int MoveSegmentTracer(Transform pathTracer, int currentPlayer, int distLeft)
    {
        int avgDist = Mathf.CeilToInt((float)distLeft / NodeCount(currentPlayer));

        //Random distance within +-2 of avgDist, based on seed; Bendiness of 0 will always be avgDist
        int bendiLength = Mathf.CeilToInt(2f * bendiness);
        int randDist = (0 < distLeft) ? SeededRandomInteger() % avgDist + bendiLength : 0;
        randDist = (randDist < avgDist - bendiLength) ? avgDist - bendiLength : randDist;
        if (randDist * 2 < distLeft)
        {
            pathTracer.Translate(Vector3.up * randDist);
            distLeft -= randDist;
        }
        else
        {
            pathTracer.Translate(Vector3.up * distLeft);
            distLeft -= distLeft;
        }
        
        mapSegments[currentPlayer].Add(new Vector3Int((int)pathTracer.position.x, Mathf.CeilToInt(pathTracer.position.y)));
        return distLeft;
    }

    private int NodeCount(int currentPlayer)
    {
        int nodeCount = SeededRandomInteger(currentPlayer) % segmentLength;//Ensures at most a path node is every 5 units
        nodeCount = (nodeCount < 3) ? 3 : nodeCount;//Ensures at least one node

        return nodeCount;
    }

    List<Vector3Int> debugBackground;
    List<Vector3Int> debugBackgroundPaths;
    List<Vector3Int> debugBackgroundExpand;
    List<Vector3Int> debugBackgroundIslands;
    List<Vector3Int> debugBackgroundNoise;
    List<Vector3Int> debugBackgroundRemove;
    public void RegenerateBackground() { GenerateBackground(); FillTiles(background, backgroundTile, backgroundLocations); }
    private void GenerateBackground()
    {
        debugBackground = new List<Vector3Int>();
        debugBackgroundPaths = new List<Vector3Int>();
        debugBackgroundExpand = new List<Vector3Int>();
        debugBackgroundIslands = new List<Vector3Int>();
        debugBackgroundNoise = new List<Vector3Int>();
        debugBackgroundRemove = new List<Vector3Int>();

        backgroundLocations = new HashSet<Vector3Int>();

        DrawBackgroundPaths();
        ExpandBackgroundPaths();

        if (allowNoise) { GenerateBackgroundNoise(); }
        if (allowIslands) { GenerateBackgroundIslands(); }

        foreach (Vector3Int coord in backgroundLocations)
        {
            debugBackground.Add(coord);
        }
    }

    private void DrawBackgroundPaths()
    {
        for (int i = 0; i < mapSegments.Count; i++)
        {
            for (int j = 0; j < mapSegments[i].Count; j++)
            {
                backgroundLocations.Add(mapSegments[i][j]);
                if (j == 0)
                {
                    DrawBresenhamLine(new Vector3Int(), mapSegments[i][j], backgroundLocations);
                }
                else
                {
                    DrawBresenhamLine(mapSegments[i][j], mapSegments[i][j - 1], backgroundLocations);
                }
            }
        }

        foreach (Vector3Int coord in backgroundLocations)
        {
            debugBackgroundPaths.Add(coord);
        }
    }

    private void ExpandBackgroundPaths()
    {
        List<Vector3Int> backgroundPathCoords = new List<Vector3Int>();
        backgroundPathCoords.AddRange(backgroundLocations);

        foreach (Vector3Int coord in backgroundPathCoords)
        {
            backgroundLocations.UnionWith(DrawBresenhamCircleFilled(coord, (int)(4 * segmentWidth) + 2));
        }

        foreach (Vector3Int coord in backgroundLocations)
        {
            if (!debugBackgroundPaths.Contains(coord))
            {
                debugBackgroundExpand.Add(coord);
            }
        }
    }
    
    private void GenerateBackgroundNoise()
    {
        RemoveBackgroundTiles();
        
        backgroundNoise = new HashSet<Vector3Int>();
        List<Vector3Int> noiseTiles = new List<Vector3Int>();
        for (int i = 0; i < (int)((segmentLength * scalingFactor * bendiness * (1 + SeededRandomInteger(segmentCount * segmentCount))) / (1f + segmentWidth)); i++)
        {
            noiseTiles.Add(NoiseTile(i, new HashSet<Vector3Int>(noiseTiles)));
        }

        backgroundNoise.UnionWith(noiseTiles);

        foreach (Vector3Int coord in backgroundNoise)
        {
            debugBackgroundNoise.Add(coord);
        }
    }
    
    private void RemoveBackgroundTiles()//TODO Very repeatative
    {
        int index = 0;
        Vector3Int[] shuffledBackground = new Vector3Int[backgroundLocations.Count];
        foreach (Vector3Int coord in backgroundLocations)
        {
            shuffledBackground[index] = coord;
            index += 1;
        }
        shuffledBackground = FisherYateShuffle(shuffledBackground);

        int tileHash;
        List<Vector3Int> removeTiles = new List<Vector3Int>();
        for (int i = 0; i < shuffledBackground.Length; i++)
        {
            tileHash = Math.Abs(shuffledBackground[i].GetHashCode());
            if ((tileHash % 100f) / 100f <= (1f / 50f) * (bendiness - (Mathf.CeilToInt((float)segmentCount / 3) % 2) * (0.34f + ((float)segmentCount % 3) / 3)))
            {
                removeTiles.Add(shuffledBackground[i]);
            }
        }
        
        foreach (Vector3Int coord in removeTiles)
        {
            debugBackgroundRemove.Add(coord);
            backgroundLocations.Remove(coord);
        }
    }
    
    private void GenerateBackgroundIslands()
    {
        bool generateTopLeft = true;
        bool generateTopRight = true;
        bool generateBottomLeft = true;
        bool generateBottomRight = true;
        List<Vector3Int> islandTiles = new List<Vector3Int>();
        HashSet<Vector3Int> islandLocations = IslandLocations();
        foreach (Vector3Int coord in islandLocations)
        {
            for (int j = 0; j < (int)(scalingFactor * segmentWidth) + (SeededRandomInteger(islandTiles.Count) / 3) - SeededRandomBinary(islandTiles.Count); j++)
            {
                if (generateTopLeft) { if (SeededRandomBinary(islandTiles.Count) == 0 && SeededRandomBinary(islandTiles.Count + 1) == 0) { generateTopLeft = false; } }
                if (generateTopRight) { if (SeededRandomBinary(islandTiles.Count + 2) == 0 && SeededRandomBinary(islandTiles.Count + 3) == 0) { generateTopRight = false; } }
                if (generateBottomLeft) { if (SeededRandomBinary(islandTiles.Count + 4) == 0 && SeededRandomBinary(islandTiles.Count + 5) == 0) { generateBottomLeft = false; } }
                if (generateBottomRight) { if (SeededRandomBinary(islandTiles.Count + 6) == 0 && SeededRandomBinary(islandTiles.Count + 7) == 0) { generateBottomRight = false; } }

                foreach (Vector3Int circle in DrawBresenhamCircleOutline(coord, j, generateTopLeft, generateTopRight, generateBottomLeft, generateBottomRight))
                {
                    islandTiles.Add(circle);
                }
            }

            generateTopLeft = true;
            generateTopRight = true;
            generateBottomLeft = true;
            generateBottomRight = true;
        }

        backgroundLocations.UnionWith(islandTiles);

        foreach (Vector3Int coord in islandTiles)
        {
            debugBackgroundIslands.Add(coord);
        }
    }

    private HashSet<Vector3Int> IslandLocations()
    {
        int islandCount = segmentLength + (int)(segmentLength * bendiness);
        HashSet<Vector3Int> islandLocations = new HashSet<Vector3Int>();
        for (int i = 0; i < islandCount; i++)
        {
            islandLocations.Add(NoiseTile(i, islandLocations) * new Vector3Int(-1, -1));
        }

        return islandLocations;
    }

    List<Vector3Int> debugCollision;
    List<Vector3Int> debugCollisionOutline;
    List<Vector3Int> debugCollisionExpand;
    List<Vector3Int> debugCollisionNoise;
    public void RegenerateCollision() { GenerateCollision(); FillTiles(collision, collisionTile, collisionLocations); }
    private void GenerateCollision()
    {
        debugCollision = new List<Vector3Int>();
        debugCollisionOutline = new List<Vector3Int>();
        debugCollisionExpand = new List<Vector3Int>();
        debugCollisionNoise = new List<Vector3Int>();

        collisionLocations = new HashSet<Vector3Int>();

        Vector4 depthsLeft = collisionDepths;
        depthsLeft -= DrawCollisionOutline(depthsLeft);
        ExpandCollisionOutline(depthsLeft);
        if (allowNoise) { AddCollisionNoise(); }

        foreach (Vector3Int coord in collisionLocations)
        {
            debugCollision.Add(coord);
        }
    }
    
    private Vector4 DrawCollisionOutline(Vector4 depths)
    {
        Vector3Int tileCheck = new Vector3Int();
        int overlap = (allowCollisionOverlap) ? 0 : 1;
        foreach (Vector3Int coord in backgroundLocations)
        {
            for (int i = 0; i < 4; i++)
            {
                if (1 < i) { tileCheck.x = (i % 2 == 0) ? coord.x + 1 : coord.x - 1; tileCheck.y = coord.y; }
                else { tileCheck.x = coord.x; tileCheck.y = (i % 2 == 0) ? coord.y + 1 : coord.y - 1; }

                if (!backgroundLocations.Contains(tileCheck))
                {
                    if (allowCollisionOverlap)
                    {
                        if (1 < i) { tileCheck.x = (i % 2 == 0) ? tileCheck.x - 1 : tileCheck.x + 1; tileCheck.y = coord.y; }
                        else { tileCheck.x = coord.x; tileCheck.y = (i % 2 == 0) ? tileCheck.y - 1 : tileCheck.y + 1; }
                    }

                    if (AllowCollisionGeneration(tileCheck, depths)) { collisionLocations.Add(tileCheck); debugCollisionOutline.Add(tileCheck); }
                }
            }
        }

        return new Vector4(1f, 1f, 1f, 1f);
    }

    private bool AllowCollisionGeneration(Vector3Int tileCheck, Vector4 depths)
    {
        bool allowGeneration = false;

        tileCheck.y -= 1;//Check tile under north
        if (!allowGeneration && 0f < depths.x && backgroundLocations.Contains(tileCheck)) { allowGeneration = true; }
        tileCheck.y += 1;

        tileCheck.x -= 1;//Check tile left of east
        if (!allowGeneration && 0f < depths.y && backgroundLocations.Contains(tileCheck)) { allowGeneration = true; }
        tileCheck.x += 1;

        tileCheck.y += 1;//Check tile above south
        if (!allowGeneration && 0f < depths.z && backgroundLocations.Contains(tileCheck)) { allowGeneration = true; }
        tileCheck.y -= 1;

        tileCheck.x += 1;//Check tile right of west
        if (!allowGeneration && 0f < depths.w && backgroundLocations.Contains(tileCheck)) { allowGeneration = true; }
        tileCheck.x -= 1;

        return allowGeneration;
    }
    
    private void ExpandCollisionOutline(Vector4 depths)
    {
        float maxDepth = (depths.y <= depths.x) ? depths.x : depths.y;
        maxDepth = (depths.z <= maxDepth) ? maxDepth : depths.z;
        maxDepth = (depths.w <= maxDepth) ? maxDepth : depths.w;

        Vector3Int tileCheck = new Vector3Int();
        HashSet<Vector3Int> collisionExpand = new HashSet<Vector3Int>();
        for (int i = 0; i < maxDepth; i++)
        {
            foreach (Vector3Int coord in collisionLocations)
            {
                if (0f < depths.x)//North
                {
                    tileCheck = coord;
                    tileCheck.y += (int)depths.x;
                    if (!backgroundLocations.Contains(tileCheck)) { collisionExpand.Add(tileCheck); debugCollisionExpand.Add(tileCheck); }
                }

                if (0f < depths.y)//East
                {
                    tileCheck = coord;
                    tileCheck.x += (int)depths.y;
                    if (!backgroundLocations.Contains(tileCheck)) { collisionExpand.Add(tileCheck); debugCollisionExpand.Add(tileCheck); }
                }

                if (0f < depths.z)//South
                {
                    tileCheck = coord;
                    tileCheck.y -= (int)depths.z;
                    if (!backgroundLocations.Contains(tileCheck)) { collisionExpand.Add(tileCheck); debugCollisionExpand.Add(tileCheck); }
                }

                if (0f < depths.w)//West
                {
                    tileCheck = coord;
                    tileCheck.x -= (int)depths.w;
                    if (!backgroundLocations.Contains(tileCheck)) { collisionExpand.Add(tileCheck); debugCollisionExpand.Add(tileCheck); }
                }
            }

            depths -= new Vector4(1f, 1f, 1f, 1f);
        }
        
        collisionLocations.UnionWith(collisionExpand);
    }

    private void AddCollisionNoise()
    {
        foreach (Vector3Int coord in backgroundNoise)
        {
            if (0 == SeededRandomInteger(coord.x + coord.y)) { return; }
            if (2 <= SeededRandomInteger(coord.x + coord.y) && !backgroundNoise.Contains(coord + new Vector3Int(1, 0)))
            {
                collisionLocations.Add(coord + new Vector3Int(1, 0));
                debugCollisionNoise.Add(coord + new Vector3Int(1, 0));
            }
            if (4 <= SeededRandomInteger(coord.x + coord.y))
            {
                collisionLocations.Add(coord + new Vector3Int(-1, 0));
                debugCollisionNoise.Add(coord + new Vector3Int(-1, 0));
            }
            if (6 <= SeededRandomInteger(coord.x + coord.y))
            {
                collisionLocations.Add(coord + new Vector3Int(0, 1));
                debugCollisionNoise.Add(coord + new Vector3Int(0, 1));
            }
            if (8 <= SeededRandomInteger(coord.x + coord.y))
            {
                collisionLocations.Add(coord + new Vector3Int(0, -1));
                debugCollisionNoise.Add(coord + new Vector3Int(0, -1));
            }

            if (SeededRandomSquare(coord.x + coord.y) + SeededRandomSquare(coord.x + coord.y + 1) < segmentLength * scalingFactor)
            {
                collisionLocations.Add(coord * new Vector3Int(-1, 1));
                debugCollisionNoise.Add(coord * new Vector3Int(-1, 1));
            }
        }
    }

    List<Vector3Int> debugUnderside;
    private void GenerateUnderside()
    {
        debugUnderside = new List<Vector3Int>();
        undersideLocations = new HashSet<Vector3Int>();

        HashSet<Vector3Int> checkLocations = new HashSet<Vector3Int>();
        checkLocations.UnionWith(backgroundLocations);
        if (allowNoise && backgroundNoise != null) { checkLocations.UnionWith(backgroundNoise); }
        if (collisionLocations != null) { checkLocations.UnionWith(collisionLocations); }
        //if (pathLocations != null) { checkLocations.UnionWith(pathLocations)}//TODO Allows for additional lists like walkways between points to have underside applied

        Vector3Int tileCheck = new Vector3Int();
        for (int i = 0; i < undersideDepth; i++)
        {
            foreach (Vector3Int coord in checkLocations)
            {
                tileCheck = coord + new Vector3Int(0, -(i + 1));
                if (!checkLocations.Contains(tileCheck)) { undersideLocations.Add(tileCheck); debugUnderside.Add(tileCheck); }
            }
        }
    }

    List<Vector3Int> debugForeground;
    public void RegenerateForeground() { GenerateForeground(); FillTiles(foreground, foregroundTile, foregroundLocations); }
    private void GenerateForeground()
    {
        debugForeground = new List<Vector3Int>();

        foregroundLocations = new HashSet<Vector3Int>();

        int index = 0;
        foreach (Vector3Int coord in backgroundLocations)
        {
            if (index % 10 == 0 && !collisionLocations.Contains(coord)) { foregroundLocations.Add(coord); }
            index++;
        }

        if (allowNoise && backgroundNoise != null)
        {
            index = 0;
            foreach (Vector3Int coord in backgroundNoise)
            {
                if (index % 10 == 0) { foregroundLocations.Add(coord);}
                index++;
            }
        }

        foreach (Vector3Int coord in foregroundLocations)
        {
            debugForeground.Add(coord);
        }
    }

    private Vector3Int NoiseTile(int index, HashSet<Vector3Int> locationSet)
    {
        //Chooses positive/negative values for noiseTile; 0 is in line with origin
        Vector3Int noiseTile = new Vector3Int(SeededRandomBinary(index), SeededRandomBinary(index + 1));
        noiseTile = (noiseTile.x < 1) ? new Vector3Int(noiseTile.x - SeededRandomBinary(index + 2), noiseTile.y) : noiseTile;
        noiseTile = (noiseTile.y < 1) ? new Vector3Int(noiseTile.x, noiseTile.y - SeededRandomBinary(index + 3)) : noiseTile;

        //Places noiseTile on 3x3 grid within segmentLength of origin
        int segmentSize = segmentLength * (scalingFactor / 2);
        noiseTile *= new Vector3Int(segmentSize + ((int)Mathf.Cos(noiseTile.y) * segmentSize / 2) + SeededRandomInteger(index + 4), segmentSize + ((int)Mathf.Cos(noiseTile.x) * segmentSize / 2) + SeededRandomInteger(index + 5));
        noiseTile += new Vector3Int(SeededRandomNegative(index + 6) * noiseTile.y / 3, SeededRandomNegative(index + 7) * noiseTile.x / 3);

        //Prevents noise from repeatedly selecting tiles
        while (locationSet.Contains(noiseTile))
        {
            if (index % 4 == 0) { noiseTile += new Vector3Int(SeededRandomNegative(index + 10) * SeededRandomInteger(index + 13), SeededRandomNegative(index + 12) * SeededRandomInteger(index + 14)); }
            else if (index % 4 == 1) { noiseTile += new Vector3Int(SeededRandomNegative(index + 10) * SeededRandomInteger(index + 13), -SeededRandomNegative(index + 12) * SeededRandomInteger(index + 14)); }
            else if (index % 4 == 2) { noiseTile += new Vector3Int(-SeededRandomNegative(index + 10) * SeededRandomInteger(index + 13), SeededRandomNegative(index + 12) * SeededRandomInteger(index + 14)); }
            else { noiseTile += new Vector3Int(-SeededRandomNegative(index + 10) * SeededRandomInteger(index + 13), -SeededRandomNegative(index + 12) * SeededRandomInteger(index + 14)); }

            index += 1;
        }

        return noiseTile;
    }

    private void FillMap()
    {
        RemovePreviousMap();

        if (backgroundTile != null && background != null) { FillTiles(background, backgroundTile, backgroundLocations); }
        if (allowNoise && backgroundTile != null && background != null) { FillTiles(background, backgroundTile, backgroundNoise, false); }
        if (allowUnderside && undersideTile != null && background != null) { FillTiles(background, undersideTile, undersideLocations, false); }
        if (collisionTile != null && collision != null && background != null) { FillTiles(collision, collisionTile, collisionLocations); }
        if (foregroundTile != null && foreground != null) { FillTiles(foreground, foregroundTile, foregroundLocations); }
    }

    private void RemovePreviousMap()
    {
        if (background != null) { background.ClearAllTiles(); }
        if (collision != null) { collision.ClearAllTiles(); }
        if (foreground != null) { foreground.ClearAllTiles(); }
    }

    private void FillTiles(Tilemap map, TileBase tile, HashSet<Vector3Int> coords, bool resizeBounds = true)
    {
        if (resizeBounds) { map.origin = Vector3Int.zero; }
        foreach (Vector3Int coord in coords)
        {
            map.SetTile(coord, tile);
        }

        if (resizeBounds) { map.ResizeBounds(); }
    }

    private void ExportSegmentLocations()
    {
        SegmentNodeLocations = new List<Vector3Int>();
        for (int i = 0; i < mapSegments.Count; i++)
        {
            for (int j = 0; j < mapSegments[i].Count; j++)
            {
                SegmentNodeLocations.Add(mapSegments[i][j]);
            }
        }
    }

    //Very useful http://members.chello.at/~easyfilter/bresenham.html
    private void DrawBresenhamLine(Vector3Int origin, Vector3Int target, HashSet<Vector3Int> locationSet)
    {
        int dx = Math.Abs(target.x - origin.x);
        int dy = -Math.Abs(target.y - origin.y);
        int sx = origin.x < target.x ? 1 : -1;
        int sy = origin.y < target.y ? 1 : -1;
        int err = dx + dy, e2; /* error value e_xy */

        while (true)
        {
            locationSet.Add(new Vector3Int(origin.x, origin.y));
            if (origin.x == target.x && origin.y == target.y)
            {
                break;
            }

            e2 = 2 * err;
            if (e2 >= dy)/* e_xy+e_x > 0 */
            {
                err += dy;
                origin.x += sx;
            }
            if (e2 <= dx)/* e_xy+e_y < 0 */
            {
                err += dx;
                origin.y += sy;
            }
        }
    }

    private HashSet<Vector3Int> DrawBresenhamCircleFilled(Vector3Int origin, int radius, bool topLeft = true, bool topRight = true, bool bottomLeft = true, bool bottomRight = true)//TODO Leaves gaps on large circles
    {
        HashSet<Vector3Int> circleSet = new HashSet<Vector3Int>();
        for (int i = 0; i < radius; i++)
        {
            circleSet.UnionWith(DrawBresenhamCircleOutline(origin, i + 1, topLeft, topRight, bottomLeft, bottomRight));
        }

        return circleSet;
    }

    private HashSet<Vector3Int> DrawBresenhamCircleOutline(Vector3Int origin, int radius, bool topLeft = true, bool topRight = true, bool bottomLeft = true, bool bottomRight = true)
    {
        int x = -radius;
        int y = 0;
        int err = 2 - 2 * radius;
        HashSet<Vector3Int> circleSet = new HashSet<Vector3Int>();

        do
        {
            if (topLeft) { circleSet.Add(new Vector3Int(origin.x - x, origin.y + y)); }
            if (topRight) { circleSet.Add(new Vector3Int(origin.x - y, origin.y - x)); }
            if (bottomLeft) { circleSet.Add(new Vector3Int(origin.x + x, origin.y - y)); }
            if (bottomRight) { circleSet.Add(new Vector3Int(origin.x + y, origin.y + x)); }

            radius = err;
            if (radius <= y)/* e_xy+e_y < 0 */
            {
                err += ++y * 2 + 1;
            }
            if (radius > x || err > y)/* e_xy+e_x > 0 or no 2nd y-step */
            {
                err += ++x * 2 + 1;
            }
        } while (x < 0);

        return circleSet;
    }

    private T[] FisherYateShuffle<T>(T[] array)
    {
        for (int i = 0; i < array.Length - 1; i++)
        {
            int rand = SeededRandomInteger(i) % (array.Length - 1);
            rand = (rand < i) ? i + (rand % (array.Length - 1) - i) : rand;

            T temp = array[i];
            array[i] = array[rand];
            array[rand] = temp;
        }

        return array;
    }

    private int SeededRandomInteger(int index = 0) => (int)char.GetNumericValue(seed.ToString(), Math.Abs(index) % (seed.ToString().Length - 1));//Range 0 - 9
    private int SeededRandomSquare(int index = 0) => (1 + SeededRandomInteger(index)) * (1 + SeededRandomInteger(index + 1));//Range 1 - 100
    private int SeededRandomBinary(int index = 0) => SeededRandomInteger(index) % 2;
    private int SeededRandomNegative(int index = 0) => (SeededRandomBinary(index) == 0) ? -1 : 1;

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            if (background != null) { background.GetComponent<TilemapRenderer>().enabled = showTiles; }
            if (collision != null) { collision.GetComponent<TilemapRenderer>().enabled = showTiles; }
            if (foreground != null) { foreground.GetComponent<TilemapRenderer>().enabled = showTiles; }

            if (showSkeleton)
            {
                for (int i = 0; i < segmentCount; i++)
                {
                    for (int j = 0; j < mapSegments[i].Count; j++)
                    {
                        Gizmos.color = Color.red;
                        if (j == 0)
                        {
                            Gizmos.DrawLine(mapSegments[i][j], new Vector3Int());
                        }
                        else
                        {
                            Gizmos.DrawLine(mapSegments[i][j], mapSegments[i][j - 1]);
                        }

                        Gizmos.DrawWireSphere(mapSegments[i][j], 0.5f);
                        if (j == mapSegments[i].Count - 1)
                        {
                            Gizmos.color = Color.yellow;
                            Gizmos.DrawWireSphere(mapSegments[i][j], 0.75f);
                        }
                    }
                }
            }

            if (showBackground && debugBackground != null)
            {
                DrawGizmosTiles(debugBackgroundPaths, Color.yellow);
                DrawGizmosTiles(debugBackgroundExpand, Color.magenta);
                DrawGizmosTiles(debugBackgroundNoise, Color.cyan);
                DrawGizmosTiles(debugBackgroundRemove, Color.red);
                DrawGizmosTiles(debugBackgroundIslands, Color.green);
            }

            if (showCollision && debugCollision != null)
            {
                DrawGizmosTiles(debugCollisionOutline, Color.yellow);
                DrawGizmosTiles(debugCollisionExpand, Color.magenta);
                DrawGizmosTiles(debugCollisionNoise, Color.cyan);
            }

            if (showUnderside && debugUnderside != null)
            {
                DrawGizmosTiles(debugUnderside, Color.yellow);
            }

            if (showForeground && debugForeground != null)
            {
                DrawGizmosTiles(debugForeground, Color.yellow);
            }

            if (showAll)
            {
                showBackground = false;
                showCollision = false;
                showUnderside = false;
                showForeground = false;

                if (debugBackground != null) { DrawGizmosTiles(debugBackground, Color.yellow); }
                if (debugCollision != null) { DrawGizmosTiles(debugCollision, Color.magenta); }
                if (debugUnderside != null) { DrawGizmosTiles(debugUnderside, Color.cyan); }
                if (debugForeground != null) { DrawGizmosTiles(debugForeground, Color.green, new Vector3(0.4f, 0.4f), true); }
            }
        }

        //Loose idea for map bounds
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(new Vector3Int(), new Vector3(segmentLength * scalingFactor * 2f, segmentLength * scalingFactor * 2f));
    }

    private void DrawGizmosTiles(List<Vector3Int> tiles, Color color, Vector3 size = default, bool circle = false)
    {

        Gizmos.color = color;
        Vector3 offset = new Vector3(0.5f, 0.5f);
        if (size == default) { size = new Vector3(0.5f, 0.5f); }
        for (int i = 0; i < tiles.Count; i++)
        {
            if (!circle) { Gizmos.DrawWireCube(tiles[i] + offset, size); }
            else { Gizmos.DrawWireSphere(tiles[i] + offset, size.x); }
        }
    }
}
