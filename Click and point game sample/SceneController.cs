using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public class SceneController : SimpleSingleton<SceneController>, ISaveable
{
    public ISaveableOrder LoadOrder => ISaveableOrder.Scenes;
    public Stack<string> loadedScenes= new Stack<string>();
    public bool CanSave() => true;
    [SerializeField]
    float fadingTime;
    const float defultFadingTime = 1;
    public GameObject loadingScreen;
    Image loadingImage;
    Color imageColor;
    float alpha;
    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
        //SceneManager.sceneLoaded += OnSceneLoaded;
        loadingImage = loadingScreen.GetComponent<Image>();
        imageColor = loadingImage.color;
        if (fadingTime == 0)
            fadingTime = defultFadingTime;
    }

    public void PrintSeceneStack()
    {
        PrintStack(loadedScenes);
    }
    public void PrintStack(Stack<string> s)
    {
        if (s.Count <= 0)
        {
            Debug.LogError("stack is empty");
            return;
        }
        string x = s.Peek();
        s.Pop();
        PrintStack(s);
        Debug.Log(x + " ");
        s.Push(x);
    }
    public void LoadLastScene()
    {
        if (loadedScenes.Count > 1)
        {
            string sceneName = loadedScenes.Pop();
            if (!string.IsNullOrEmpty(sceneName))
                StartCoroutine(LoadSceneWithAnimation(sceneName, false));
        }
        else
        {
            Debug.LogError("No previous scene loaded becuase there is no way to go back on the stalk. i <=1");

        }
    }
    public void LoadNextScene(string sceneToLoadName)
    {
        if (!string.IsNullOrEmpty(sceneToLoadName))
            StartCoroutine(LoadSceneWithAnimation(sceneToLoadName,true));
        else Debug.LogError("scene to load  name is empty", this);
    }
    public void LoadNextScene(int sceneIntIndex)
    {
        StartCoroutine( LoadSceneWithAnimation(sceneIntIndex, true));
    }
    private IEnumerator LoadSceneWithAnimation(string sceneName, bool changeLastSceneName)
    {
        // save progress of this sence before moving to the next one
        // adding current scene to stack
        if(changeLastSceneName) // going forwards
            loadedScenes.Push(SceneManager.GetActiveScene().name);
        loadingScreen.SetActive(true);
        yield return StartCoroutine(FadeToBlackAnimation());
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;
        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }
        asyncLoad.allowSceneActivation = true;
        yield return StartCoroutine(FadeFromBlackAnimation());
        loadingScreen.SetActive(false);
        yield return StartCoroutine(SaveGameManager.SaveGameCoroutine());
    }
    private IEnumerator LoadSceneWithAnimation(int sceneIntIndex, bool changeLastSceneName)
    {
        //yield return StartCoroutine(SaveGameManager.SaveGameCoroutine());
        if (changeLastSceneName) // going forwards
            loadedScenes.Push(SceneManager.GetActiveScene().name);
        loadingScreen.SetActive(true);
        yield return StartCoroutine(FadeToBlackAnimation());
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIntIndex);
        asyncLoad.allowSceneActivation = false;
        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }
        asyncLoad.allowSceneActivation = true;
        yield return StartCoroutine(FadeFromBlackAnimation());
        loadingScreen.SetActive(false);
        yield return StartCoroutine(SaveGameManager.SaveGameCoroutine());
    }
    private IEnumerator FadeToBlackAnimation()
    {
        float startValue = 0;
        float targetValue = 1;
        if(loadingImage.color.a !=startValue)
            loadingImage.color = new Color(imageColor.r, imageColor.b, imageColor.g, startValue);
        float time = 0;

        while (time < fadingTime)
        {
            alpha = Mathf.Lerp(startValue, targetValue, time / fadingTime);
            loadingImage.color = new Color(imageColor.r, imageColor.b,imageColor.g,alpha);
            time += Time.deltaTime;
            yield return null;
        }
    }
    private IEnumerator FadeFromBlackAnimation()
    {
        float startValue = 1;
        float targetValue = 0;
        if (loadingImage.color.a != startValue)
            loadingImage.color = new Color(imageColor.r, imageColor.b, imageColor.g, startValue);
        float time = 0;

        while (time < fadingTime)
        {
            alpha = Mathf.Lerp(startValue, targetValue, time / fadingTime);
            loadingImage.color = new Color(imageColor.r, imageColor.b, imageColor.g, alpha);
            time += Time.deltaTime;
            yield return null;
        }
    }

    #region Savable
    public void Save(GameState state)
    {
        state.ActiveScene = SceneManager.GetActiveScene().name;
        if (loadedScenes != null)
        {
            Debug.Log("Saving Scene Controller with stack length of: " + loadedScenes);
            state.LoadedScenes = loadedScenes;
        }
        else
        {
            Debug.LogError("local stack is null", this);
        }
    }

    public void Load(GameState state)
    {
        if (state.LoadedScenes != null)
        {
            Debug.Log("Loading Scene Controller with stack length of: " + state.LoadedScenes);
            loadedScenes = state.LoadedScenes;
        }
        else
        {
            Debug.LogError("saved stack is null", this);
        }        
    }

    #endregion
}

