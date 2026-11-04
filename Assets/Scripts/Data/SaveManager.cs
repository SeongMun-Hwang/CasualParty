using System.IO;
using System;
using UnityEngine;

public static class SaveManager
{
#if UNITY_EDITOR
    // Allows automated tests to use an isolated directory without touching player saves.
    public static string TestDataDirectory;
#endif
    public static string DataDirectory
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(TestDataDirectory)) return TestDataDirectory;
#endif
            return Application.persistentDataPath;
        }
    }
    public static void SaveData<T>(string fileName, T data)
    {
        string path = Path.Combine(DataDirectory, fileName);
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonUtility.ToJson(data));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogWarning($"Could not save {fileName}: {e.Message}");
        }
    }

    public static T LoadData<T>(string fileName) where T : new()
    {
        return TryLoadData<T>(fileName, out var data) ? data : new T();
    }

    public static bool TryLoadData<T>(string fileName, out T data)
    {
        data = default;
        string path = Path.Combine(DataDirectory, fileName);
        if (!File.Exists(path)) return false;
        try
        {
            data = JsonUtility.FromJson<T>(File.ReadAllText(path));
            return data != null;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        {
            Debug.LogWarning($"Could not load {fileName}: {e.Message}");
            return false;
        }
    }

    public static bool Exists(string fileName)
    {
        return File.Exists(Path.Combine(DataDirectory, fileName));
    }

    public static void Delete(string fileName)
    {
        string path = Path.Combine(DataDirectory, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
