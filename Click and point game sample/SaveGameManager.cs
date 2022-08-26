using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

public static class SaveGameManager
{
    private static readonly string SavegameFolder = $"{Application.persistentDataPath}/save/";
    private static readonly string SavegameFilename = $"savegame.sav";

    private static bool _savegameActionInProgress = false;
    private static readonly object _syncRoot = new object();
    private static GameState _lastLoadedState = null;
    public static void LoadLastSavedGame()
    {
        lock (_syncRoot)
        {
            LoadSceneFromState(LoadStateOrDefault());
        }
    }

    public static void LoadSceneWithState()
    {
        //LoadSceneFromState(LoadStateOrDefault(), sceneName);
    }
    public static void StartNewGame()
    {
        lock (_syncRoot)
        {
            GameState state = new GameState();
            state.IsNewGame = true;
            LoadSceneFromState(state);
        }
    }

    static SaveGameManager()
    {
        SceneManager.sceneLoaded += SceneManager_sceneLoaded;
    }

    private static string GetSavegameFilePath()
    {
        return SavegameFolder + SavegameFilename;
    }

    public static IEnumerator SaveGameCoroutine()
    {
        if (_savegameActionInProgress)
        {
            yield break;
        }

        lock (_syncRoot)
        {
            _savegameActionInProgress = true;

            GameState state = new GameState();
            foreach (ISaveable saveable in
                SceneManager.GetActiveScene()
                            .GetRootGameObjects()
                            .SelectMany(g => g.GetComponentsInChildren<ISaveable>()))
            {
                
                while (!saveable.CanSave())
                {
                    yield return null;
                }

                saveable.Save(state);
            }
            GameManager.Instance.sceneController.Save(state);
            SaveState(state);
            Debug.Log("Game saved");
            TextInfoManager.Instance.ShowMessage("Game saved");

            _savegameActionInProgress = false;
        }
    }
    public static void LoadSceneFromState(GameState state)
    {
        Assert.IsTrue(SceneManager.sceneCountInBuildSettings > 0, "There is only a single scene in build settings");

        if (_savegameActionInProgress)
        {
            return;
        }
        lock (_syncRoot)
        {
            _savegameActionInProgress = true;
            int sceneIndex = -1;
            sceneIndex = SceneUtility.GetBuildIndexByScenePath(state.ActiveScene);
            if (sceneIndex <= 0)
            {
                sceneIndex = 1;
            }
            _lastLoadedState = state;
            SceneController.Instance.LoadNextScene(sceneIndex);
        }
    }
    private static void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.IsValid())
        {
            if (_lastLoadedState != null) // already existing game
            {
                foreach (ISaveable saveable in
                    SceneManager.GetActiveScene()
                                .GetRootGameObjects()
                                .SelectMany(g => g.GetComponentsInChildren<ISaveable>())
                                .OrderBy(saveable => (int)saveable.LoadOrder))
                {
                    saveable.Load(_lastLoadedState);
                }
                Debug.Log(GameManager.Instance);
                GameManager.Instance.sceneController.Load(_lastLoadedState);
            }

            if (_lastLoadedState == null || _lastLoadedState.IsNewGame)
                // starting a new game
            {
                TextInfoManager.Instance.ShowWelcomeMessage();
            }

            _savegameActionInProgress = false;
        }
    }
    public static void SaveState(GameState state)
    {
        lock (_syncRoot)
        {
            Directory.CreateDirectory(SavegameFolder);
            using (FileStream stream = new FileStream(GetSavegameFilePath(), FileMode.Create))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, state);
            }
        }
    }
    public static GameState LoadState()
    {
        lock (_syncRoot)
        {
            using (FileStream stream = new FileStream(GetSavegameFilePath(), FileMode.Open))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(stream) as GameState;
            }
        }
    }
    public static GameState LoadStateOrDefault()
    {
        lock (_syncRoot)
        {
            if (File.Exists(GetSavegameFilePath()))
            {
                return LoadState();
            }

            return new GameState();
        }
    }

    public static bool DoesSavedStateExists()
    {
        return File.Exists(GetSavegameFilePath());
    }
}