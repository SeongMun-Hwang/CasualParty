using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq; // Needed for Distinct() in color selection

using static SavePaths;

public class BlockSpawner_2D : MonoBehaviour
{
    public static BlockSpawner_2D Instance { get; private set; }

    [System.Serializable]
    public struct HSVColor
    {
        [Range(0f, 1f)] public float h;
        [Range(0f, 1f)] public float s;
        [Range(0f, 1f)] public float v;
        public Color color; // For visual preview in Inspector

        public HSVColor(float h, float s, float v)
        {
            this.h = h;
            this.s = s;
            this.v = v;
            this.color = Color.HSVToRGB(h, s, v);
        }

        public Color ToColor()
        {
            return Color.HSVToRGB(h, s, v);
        }
    }

    [System.Serializable]
    public class BlockSpawnItem
    {
        public bool isActive = true;
        public BlockArray block;
    }

    /// <summary>
    /// A group consisting of a main BlockArray and several variant BlockArrays.
    /// Used to group similar shapes together to prevent them from overwhelming the spawn pool.
    /// </summary>
    [System.Serializable]
    public class BlockSpawnGroup
    {
        public string groupName; // Helpful for organizing in the Inspector
        public bool isActive = true;
        [Tooltip("The primary BlockArray for this group.")]
        public BlockSpawnItem mainBlock;
        [Tooltip("List of alternative BlockArrays that can be spawned for this group.")]
        public List<BlockSpawnItem> variants;

        /// <summary>
        /// Checks if this group has at least one active block item.
        /// </summary>
        public bool HasActiveItems()
        {
            if (!isActive) return false;
            if (mainBlock != null && mainBlock.isActive && mainBlock.block != null) return true;
            if (variants != null)
            {
                foreach (var v in variants)
                    if (v != null && v.isActive && v.block != null) return true;
            }
            return false;
        }

        /// <summary>
        /// Picks either the main block or one of its variants at random from the active ones.
        /// </summary>
        public BlockArray GetRandomBlock()
        {
            if (!isActive) return null;

            List<BlockArray> activeOptions = new List<BlockArray>();
            if (mainBlock != null && mainBlock.isActive && mainBlock.block != null)
                activeOptions.Add(mainBlock.block);

            if (variants != null)
            {
                foreach (var variant in variants)
                {
                    if (variant != null && variant.isActive && variant.block != null)
                        activeOptions.Add(variant.block);
                }
            }

            if (activeOptions.Count == 0) return null;

            return activeOptions[Random.Range(0, activeOptions.Count)];
        }
    }

    [Header("Spawning Configuration")]
    [SerializeField] private GameObject blockContainerPrefab; // The empty container prefab with Block_2D script
    [SerializeField] private List<BlockSpawnGroup> blockGroups; // List of all possible root block groups
    [SerializeField] private List<Transform> spawnPositions;

    [Header("Color Configuration")]
    [SerializeField] private List<HSVColor> availableColors = new List<HSVColor>()
    {
        new HSVColor(0.00f, 0.5f, 1.0f), // Soft Red
        new HSVColor(0.33f, 0.5f, 1.0f), // Soft Green
        new HSVColor(0.60f, 0.5f, 1.0f), // Soft Blue
        new HSVColor(0.13f, 0.5f, 1.0f), // Soft Yellow
        new HSVColor(0.80f, 0.5f, 1.0f), // Soft Magenta
        new HSVColor(0.08f, 0.5f, 1.0f), // Soft Orange
    };

    private List<BlockArray> _cachedAllPossibleBlocks;
    public List<BlockArray> AllPossibleBlocks
    {
        get
        {
            if (_cachedAllPossibleBlocks == null)
            {
                _cachedAllPossibleBlocks = blockGroups
                    .SelectMany(g => 
                    {
                        var list = new List<BlockArray>();
                        if (g.mainBlock != null && g.mainBlock.block != null) list.Add(g.mainBlock.block);
                        if (g.variants != null) 
                            list.AddRange(g.variants.Where(v => v != null && v.block != null).Select(v => v.block));
                        return list;
                    })
                    .Distinct()
                    .ToList();
            }
            return _cachedAllPossibleBlocks;
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SpawnBlocks()
    {
        // 1. Clear existing blocks
        ClearSpawnedBlocks();

        if (blockGroups.Count == 0 || spawnPositions.Count == 0)
        {
            Debug.LogError("BlockSpawner_2D: Configuration missing.");
            return;
        }

        // 2. Determine a valid set of blocks (without instantiation)
        var validBlockSet = FindValidBlockSet();

        // 3. Instantiate and setup the valid blocks
        for (int i = 0; i < validBlockSet.Count; i++)
        {
            var data = validBlockSet[i];
            Transform spawnPos = spawnPositions[i];
            
            GameObject blockGO = Instantiate(blockContainerPrefab, spawnPos.position, Quaternion.identity, spawnPos);
            Block_2D blockScript = blockGO.GetComponent<Block_2D>();
            blockScript.Initialize(data.blockArray, data.rotation, data.color);
            
            spawnedBlocks.Add(blockGO);
        }

        if (GameManager_2D.Instance != null)
            GameManager_2D.Instance.SaveGameData();
    }

    private struct PendingBlockData
    {
        public BlockArray blockArray;
        public int rotation;
        public Color color;
    }

    private List<PendingBlockData> FindValidBlockSet()
    {
        List<PendingBlockData> pendingSet = new List<PendingBlockData>();
        bool isPlaceableSetFound = false;
        int maxAttempts = 100; // Safety break

        // Filter active groups once outside the loop
        var activeGroupIndexes = new List<int>();
        for (int i = 0; i < blockGroups.Count; i++)
        {
            if (blockGroups[i].isActive) activeGroupIndexes.Add(i);
        }

        if (activeGroupIndexes.Count == 0)
        {
            Debug.LogWarning("BlockSpawner_2D: No active block groups found!");
            return pendingSet;
        }

        while (!isPlaceableSetFound && maxAttempts-- > 0)
        {
            pendingSet.Clear();
            
            // Pick unique groups from active ones
            var selectedGroupIndexes = activeGroupIndexes
                .OrderBy(x => Random.value)
                .Take(spawnPositions.Count)
                .ToList();

            // Prepare colors
            var chosenColors = availableColors
                .OrderBy(x => Random.value)
                .Take(spawnPositions.Count)
                .Select(hsv => hsv.ToColor())
                .ToList();

            for (int i = 0; i < selectedGroupIndexes.Count; i++)
            {
                BlockArray actualBlock = blockGroups[selectedGroupIndexes[i]].GetRandomBlock();
                if (actualBlock == null) continue;

                pendingSet.Add(new PendingBlockData 
                { 
                    blockArray = actualBlock, 
                    rotation = Random.Range(0, 4), 
                    color = chosenColors[i] 
                });
            }

            // Validation check (without instantiation)
            foreach (var pending in pendingSet)
            {
                List<Vector2Int> shape = pending.blockArray.GetShape(pending.rotation);
                if (GameManager_2D.Instance.gridManager.IsValidPlacementForAll(shape)) // Optimized check
                {
                    isPlaceableSetFound = true;
                    break;
                }
            }
        }
        return pendingSet;
    }

    private void ClearSpawnedBlocks()
    {
        foreach (GameObject block in spawnedBlocks)
            if (block != null) Destroy(block);
        spawnedBlocks.Clear();
    }

    [System.Serializable]
    public class BlockSaveData_2D
    {
        public int blockArrayIndex;
        public int spawnIndex;
        public int rotationStep;
        public SerializableColor blockColor;
    }

    [System.Serializable]
    public class BlockSaveDatas_2D
    {
        public List<BlockSaveData_2D> blocks;
    }

    private readonly List<GameObject> spawnedBlocks = new List<GameObject>();

    public void BlockPlaced(GameObject blockGO, Vector2Int gridPosition, List<Vector2Int> shape, Color blockColor)
    {
        spawnedBlocks.Remove(blockGO);
        StartCoroutine(BlockPlacedRoutine(gridPosition, shape, blockColor));
    }

    private System.Collections.IEnumerator BlockPlacedRoutine(Vector2Int gridPosition, List<Vector2Int> shape, Color blockColor)
    {
        // Actually place the block on the grid
        int clearCount = GridManager_2D.Instance.PlaceBlock(gridPosition, shape, blockColor);

        if (clearCount > 0)
        {
            // Wait for sequential clear animations to finish
            float waitTime = clearCount * GridManager_2D.Instance.clearAnimationSequentialDelay + 0.4f;
            yield return new WaitForSeconds(waitTime);
        }

        if (spawnedBlocks.Count == 0)
        {
            SpawnBlocks();
        }

        if (GameManager_2D.Instance != null)
        {
            GameManager_2D.Instance.SaveGameData();
        }
        CheckForGameOver();
    }

    public void CheckForGameOver()
    {
        GameManager_2D.Instance.CheckGameOver();
    }

    public List<GameObject> GetSpawnedBlocks()
    {
        return spawnedBlocks;
    }

    public void SaveBlockData_2D()
    {
        BlockSaveDatas_2D blockSaveDatas = new BlockSaveDatas_2D();
        blockSaveDatas.blocks = new List<BlockSaveData_2D>();

        for (int i = 0; i < spawnedBlocks.Count; i++)
        {
            GameObject go = spawnedBlocks[i];
            Block_2D block = go.GetComponent<Block_2D>();
            if (block == null) continue;
            
            int blockArrayIndex = AllPossibleBlocks.IndexOf(block.BlockData);
            int spawnIndex = spawnPositions.IndexOf(go.transform.parent);
            
            blockSaveDatas.blocks.Add(new BlockSaveData_2D
            {
                blockArrayIndex = blockArrayIndex,
                spawnIndex = spawnIndex,
                rotationStep = block.CurrentRotationStep,
                blockColor = block.blockColor
            });
        }
        string json = JsonUtility.ToJson(blockSaveDatas);
        File.WriteAllText(BlockDataPath, json);
    }

    public void LoadBlockData_2D()
    {
        ClearSpawnedBlocks();

        if (File.Exists(BlockDataPath))
        {
            string json = File.ReadAllText(BlockDataPath);
            BlockSaveDatas_2D blockSaveDatas = JsonUtility.FromJson<BlockSaveDatas_2D>(json);

            foreach (BlockSaveData_2D data in blockSaveDatas.blocks)
            {
                if (data.blockArrayIndex < 0 || data.blockArrayIndex >= AllPossibleBlocks.Count) continue;
                if (data.spawnIndex < 0 || data.spawnIndex >= spawnPositions.Count) continue;

                Transform spawnPos = spawnPositions[data.spawnIndex];
                GameObject blockGO = Instantiate(blockContainerPrefab, spawnPos.position, Quaternion.identity, spawnPos);
                Block_2D blockScript = blockGO.GetComponent<Block_2D>();
                blockScript.Initialize(AllPossibleBlocks[data.blockArrayIndex], data.rotationStep, data.blockColor);
                
                spawnedBlocks.Add(blockGO);
            }
        }
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(BlockSpawner_2D.HSVColor))]
public class HSVColorDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        UnityEditor.EditorGUI.BeginProperty(position, label, property);

        position = UnityEditor.EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        float fieldWidth = position.width / 4f;
        Rect hRect = new Rect(position.x, position.y, fieldWidth - 5, position.height);
        Rect sRect = new Rect(position.x + fieldWidth, position.y, fieldWidth - 5, position.height);
        Rect vRect = new Rect(position.x + fieldWidth * 2, position.y, fieldWidth - 5, position.height);
        Rect colorRect = new Rect(position.x + fieldWidth * 3, position.y, fieldWidth, position.height);

        var hProp = property.FindPropertyRelative("h");
        var sProp = property.FindPropertyRelative("s");
        var vProp = property.FindPropertyRelative("v");
        var colorProp = property.FindPropertyRelative("color");

        UnityEditor.EditorGUI.BeginChangeCheck();
        float h = UnityEditor.EditorGUI.FloatField(hRect, hProp.floatValue);
        float s = UnityEditor.EditorGUI.FloatField(sRect, sProp.floatValue);
        float v = UnityEditor.EditorGUI.FloatField(vRect, vProp.floatValue);
        if (UnityEditor.EditorGUI.EndChangeCheck())
        {
            hProp.floatValue = Mathf.Clamp01(h);
            sProp.floatValue = Mathf.Clamp01(s);
            vProp.floatValue = Mathf.Clamp01(v);
            colorProp.colorValue = Color.HSVToRGB(hProp.floatValue, sProp.floatValue, vProp.floatValue);
        }

        UnityEditor.EditorGUI.BeginChangeCheck();
        Color newColor = UnityEditor.EditorGUI.ColorField(colorRect, GUIContent.none, colorProp.colorValue, false, false, false);
        if (UnityEditor.EditorGUI.EndChangeCheck())
        {
            colorProp.colorValue = newColor;
            float newH, newS, newV;
            Color.RGBToHSV(newColor, out newH, out newS, out newV);
            hProp.floatValue = newH;
            sProp.floatValue = newS;
            vProp.floatValue = newV;
        }

        UnityEditor.EditorGUI.EndProperty();
    }
}
#endif
