using System.Collections.Generic;
using UnityEngine;

public static class SavePaths
{
    public static string BlockDataPath => SaveManager.DataDirectory + "/blockData.json";
    public static string BoardDataPath => SaveManager.DataDirectory + "/save.json";
    public static string SettingDataPath => SaveManager.DataDirectory + "/setting.json";
    public static string PersonalDataPath => SaveManager.DataDirectory + "/personal.json";
    public static string TenSumDataPath => SaveManager.DataDirectory + "/TenSum.json";
    public static string MineSweeperDataPath => SaveManager.DataDirectory + "/MineSweeper.json";
}

[System.Serializable]
public class BlockSaveData
{
    public string prefabName;
    public int spawnIndex;
    public int rotationStep;
}
[System.Serializable]
public class BlockSaveDatas
{
    public List<BlockSaveData> blocks;
}
[System.Serializable]
public class SaveData
{
    public bool[] cubeFilledStates = new bool[81];
    public int score;
    public int combo;
}
[System.Serializable]
public class  AudioData
{
    public bool bgmMute;
    public bool sfxMute;
    public bool vibrationMute;
    public float bgmVolume = 1f;
    public float sfxVolume = 1f;
    public float vibrationValue = 1f;
}
[System.Serializable]
public class  PersonalData
{
    public int bestScore;
}
[System.Serializable]
public class TenSumData
{
    public int bestScore;
}
[System.Serializable]
public class MineSweeperData
{
    public float bestTimeBeginner = float.MaxValue;
    public float bestTimeIntermediate = float.MaxValue;
    public float bestTimeAdvanced = float.MaxValue;
}
