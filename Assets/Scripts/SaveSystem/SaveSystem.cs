using UnityEngine;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Threading.Tasks;
using static SerializationAssistant;

/// <summary>
/// A singleton class that handles saving and loading
/// </summary>
public class SaveSystem : Singleton<SaveSystem>
{
    /// <summary>
    /// An inner class representing a save file. Should not do
    /// anything besides hold values.
    /// </summary>
    [System.Serializable]
    public class Save
    {
        public Feature[] features;

        public string pathName;
        public SerializableVector3 sylviePosition;
        public HashSet<string> visitedAreas;
    }

    /// <summary>
    /// The currently loaded game save. Used in <see cref="SaveGame(Feature[])"/>
    /// to add features to the current save. Can be updated by calling
    /// <see cref="LoadSave(Save)"/>
    /// </summary>
    public static Save loadedSave;

    public const string DUMMY_FILE_NAME = "save0";

    public enum Feature
    {
        SylviePosition,
        VisitedAreas,
        DialogueVariables,
    }

    void Awake()
    {
        loadedSave = null;
        InitializeSingleton(gameObject);
    }

    /// <summary>
    /// Takes an existing save (or creates a new one), and saves the specified
    /// features of the game.
    /// </summary>
    /// Some parts of the game don't need to be saved all the time (e.g.
    /// visited areas only need to be updated when a new area is unlocked), so
    /// features are chosen as a list.
    /// <param name="from">The existing save to add onto. If null, creates a
    /// new save file.</param>
    /// <param name="features">The various parts of the game to add to the
    /// existing save.</param>
    /// <returns>A save file representing the state of the world</returns>
    public static Save GenerateSave(Save from, params Feature[] features)
    {
        Save save = from;
        save ??= new()
        {
            pathName = DUMMY_FILE_NAME,
        };
        save.features = features;

        foreach (Feature f in save.features)
        {
            switch (f)
            {
                case Feature.SylviePosition:
                    GameObject sylvie = GameObject.FindWithTag("Player");
                    save.sylviePosition = sylvie.transform.position.ToSerializable();
                    break;

                case Feature.VisitedAreas:
                    save.visitedAreas = VisitedAreaManager.visitedAreas;
                    break;
            }
        }

        return save;
    }

    /// <summary>
    /// Write the given Save to a file based on its path name.
    /// Blocks when run synchronously, should be run in a background thread.
    /// </summary>
    /// <param name="save">The save file to write</param>
    public static void SaveToFile(Save save)
    {
        Debug.Log($"Saving to {Application.dataPath}");

        // The size of the buffer for the save file
        // (Shouldn't affect performance too much, as this file should
        // remain fairly small)
        const int BUF_SIZE = 1_000;

        BinaryFormatter bf = new();
        string realPath = Path.ChangeExtension(Path.Combine(Application.dataPath, save.pathName), ".sylvie");
        using FileStream fs = new(
            realPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            bufferSize: BUF_SIZE,
            useAsync: true);
        bf.Serialize(fs, save);
    }

    /// <summary>
    /// Load a Save from the given file.
    /// Runs synchronously (blocks), so it could be run on a background thread.
    /// </summary>
    /// <param name="filename">The name of the file to load (just the part 
    /// before the extension, "save0" right now</param>
    /// <returns>A Save if the file is found</returns>
    public static Save LoadFromFile(string filename)
    {
        try
        {
            // The size of the buffer for the save file
            // (Shouldn't affect performance too much, as this file should
            // remain fairly small)
            const int BUF_SIZE = 1_000;

            BinaryFormatter bf = new();
            string realPath = Path.ChangeExtension(Path.Combine(Application.dataPath, filename), ".sylvie");
            using FileStream fs = new(
                realPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: BUF_SIZE,
                useAsync: true);
            Save save = (Save)bf.Deserialize(fs);

            Debug.Log($"Loaded from {Application.dataPath}");

            return save;
        }
        catch (System.Exception e)
        {
            Debug.Log($"Couldn't load from {Application.dataPath}: {e.Message}");

            return null;
        }
    }

    /// <summary>
    /// Takes a given Save and transforms the state of the world to match it.
    /// </summary>
    /// Updates the <see cref="loadedSave"/> value.
    /// <param name="save">The Save file to be loaded</param>
    public static void LoadSave(Save save)
    {
        if (save == null)
        {
            Debug.LogWarning("Tried to load a null save");
            return;
        }

        foreach (Feature f in save.features)
        {
            switch (f)
            {
                case Feature.SylviePosition:
                    GameObject sylvie = GameObject.FindWithTag("Player");
                    sylvie.transform.position = save.sylviePosition.ToVector3();
                    break;

                case Feature.VisitedAreas:
                    VisitedAreaManager.visitedAreas = save.visitedAreas;
                    break;
            }
        }

        loadedSave = save;
    }

    /// <summary>
    /// Saves the specified features onto <see cref="loadedSave"/> (or creates
    /// a new save) and writes the save to a file.
    /// </summary>
    public static void SaveGame(params Feature[] features)
    {
        Save save = GenerateSave(loadedSave, features);
        Task.Run(() => SaveToFile(save));
    }

    /// <summary>
    /// Loads the game from the save at the default file name.
    /// </summary>
    /// If the file does not exist, this will do nothing.
    public static void TryLoadGame()
    {
        LoadSave(LoadFromFile(DUMMY_FILE_NAME));
    }
}
