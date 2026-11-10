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

            int count = mainBlock != null && mainBlock.isActive && mainBlock.block != null ? 1 : 0;
            if (variants != null) foreach (var variant in variants)
                if (variant != null && variant.isActive && variant.block != null) count++;
            if (count == 0) return null;
            int choice = Random.Range(0, count);
            if (mainBlock != null && mainBlock.isActive && mainBlock.block != null)
            {
                if (choice-- == 0) return mainBlock.block;
            }
            if (variants != null) foreach (var variant in variants)
                if (variant != null && variant.isActive && variant.block != null && choice-- == 0) return variant.block;
            return null;
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

    public void SpawnBlocks(bool saveAfterSpawn = true)
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

        if (saveAfterSpawn && GameManager_2D.Instance != null)
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

        var groupIndexes = new List<int>(activeGroupIndexes.Count);
        var colorIndexes = new List<int>(availableColors.Count);
        var placementCache = new Dictionary<BlockArray, sbyte[]>();
        if (availableColors.Count == 0) return pendingSet;
        while (!isPlaceableSetFound && maxAttempts-- > 0)
        {
            pendingSet.Clear();
            groupIndexes.Clear(); groupIndexes.AddRange(activeGroupIndexes);
            colorIndexes.Clear();
            for (int i = 0; i < availableColors.Count; i++) colorIndexes.Add(i);
            int count = Mathf.Min(spawnPositions.Count, groupIndexes.Count, colorIndexes.Count);
            ShuffleFirst(groupIndexes, count); ShuffleFirst(colorIndexes, count);
            for (int i = 0; i < count; i++)
            {
                var actualBlock = blockGroups[groupIndexes[i]].GetRandomBlock();
                if (actualBlock == null) continue;
                pendingSet.Add(new PendingBlockData { blockArray = actualBlock, rotation = Random.Range(0, 4), color = availableColors[colorIndexes[i]].ToColor() });
            }
            foreach (var pending in pendingSet)
            {
                if (!placementCache.TryGetValue(pending.blockArray, out var rotations))
                {
                    rotations = new sbyte[4]; placementCache.Add(pending.blockArray, rotations);
                }
                if (rotations[pending.rotation] == 0)
                    rotations[pending.rotation] = (sbyte)(GameManager_2D.Instance.gridManager.IsValidPlacementForAll(pending.blockArray.GetShape(pending.rotation)) ? 1 : -1);
                if (rotations[pending.rotation] == 1) { isPlaceableSetFound = true; break; }
            }
        }
        return pendingSet;
    }

    private static void ShuffleFirst(List<int> values, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int other = Random.Range(i, values.Count);
            int value = values[i]; values[i] = values[other]; values[other] = value;
        }
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
    public bool IsProcessingPlacement { get; private set; }
    private Block_2D draggingBlock;

    public bool TryBeginDrag(Block_2D block)
    {
        if (IsProcessingPlacement || draggingBlock != null) return false;
        draggingBlock = block;
        return true;
    }

    public void EndDrag(Block_2D block)
    {
        if (draggingBlock == block) draggingBlock = null;
    }

    public void BlockPlaced(GameObject blockGO, Vector2Int gridPosition, List<Vector2Int> shape, Color blockColor)
    {
        if (IsProcessingPlacement || GameManager_2D.Instance.IsGameOver() ||
            !spawnedBlocks.Contains(blockGO) || !GridManager_2D.Instance.IsValidPlacement(gridPosition, shape)) return;
        IsProcessingPlacement = true;
        spawnedBlocks.Remove(blockGO);
        StartCoroutine(BlockPlacedRoutine(gridPosition, shape, blockColor));
    }

    private System.Collections.IEnumerator BlockPlacedRoutine(Vector2Int gridPosition, List<Vector2Int> shape, Color blockColor)
    {
        // Actually place the block on the grid
        int clearCount = GridManager_2D.Instance.PlaceBlock(gridPosition, shape, blockColor);

        // Resolve the next hand before saving, even while the clear visuals are playing.
        if (spawnedBlocks.Count == 0)
        {
            SpawnBlocks(false);
        }

        if (GameManager_2D.Instance != null)
        {
            GameManager_2D.Instance.SaveGameData();
        }
        CheckForGameOver();
        if (clearCount > 0)
        {
            float waitTime = clearCount * GridManager_2D.Instance.clearAnimationSequentialDelay + 0.5f;
            yield return new WaitForSeconds(waitTime);
        }
        IsProcessingPlacement = false;
    }

    public void CheckForGameOver()
    {
        GameManager_2D.Instance.CheckGameOver();
    }

    public List<GameObject> GetSpawnedBlocks()
    {
        return spawnedBlocks;
    }

    public BlockSaveDatas_2D CaptureBlocks()
    {
        BlockSaveDatas_2D blockSaveDatas = new BlockSaveDatas_2D();
        blockSaveDatas.blocks = new List<BlockSaveData_2D>();

        for (int i = 0; i < spawnedBlocks.Count; i++)
        {
            GameObject go = spawnedBlocks[i];
            Block_2D block = go.GetComponent<Block_2D>();
            if (block == null) continue;
            
            int blockArrayIndex = AllPossibleBlocks.IndexOf(block.BlockData);
            int spawnIndex = spawnPositions.IndexOf(block.SpawnParent);
            
            blockSaveDatas.blocks.Add(new BlockSaveData_2D
            {
                blockArrayIndex = blockArrayIndex,
                spawnIndex = spawnIndex,
                rotationStep = block.CurrentRotationStep,
                blockColor = block.blockColor
            });
        }
        return blockSaveDatas;
    }

    public void LoadBlockData_2D(BlockSaveDatas_2D savedBlocks = null)
    {
        ClearSpawnedBlocks();

        if (savedBlocks == null) SaveManager.TryLoadData("blockData.json", out savedBlocks);
        if (savedBlocks != null && savedBlocks.blocks != null)
        {
            var usedSlots = new HashSet<int>();
            foreach (BlockSaveData_2D data in savedBlocks.blocks)
            {
                if (data == null || !usedSlots.Add(data.spawnIndex)) continue;
                if (data.blockArrayIndex < 0 || data.blockArrayIndex >= AllPossibleBlocks.Count) continue;
                if (data.spawnIndex < 0 || data.spawnIndex >= spawnPositions.Count) continue;

                Transform spawnPos = spawnPositions[data.spawnIndex];
                GameObject blockGO = Instantiate(blockContainerPrefab, spawnPos.position, Quaternion.identity, spawnPos);
                Block_2D blockScript = blockGO.GetComponent<Block_2D>();
                blockScript.Initialize(AllPossibleBlocks[data.blockArrayIndex], ((data.rotationStep % 4) + 4) % 4, data.blockColor);
                
                spawnedBlocks.Add(blockGO);
            }
        }
        if (spawnedBlocks.Count == 0) SpawnBlocks();
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
