using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

/// <summary>
/// A singleton, which connects different parts of the game
/// and allows them to interact with each other
/// </summary>
public class GameManager : SimpleSingleton<GameManager>, ISaveable
{
    public InventoryPanel inventoryPanel;
    public Inventory inventory;
    public Waypoint startingNode;
    public CameraControl cameraControl;
    public Waypoint currentWaypoint = null;
    internal SceneController sceneController;
    public InventoryItem itemDragged { get; private set; }
    public ISaveableOrder LoadOrder => ISaveableOrder.Last;
    public bool CanSave() => true;

    protected void Start()
    {
        if (currentWaypoint == null)
        {
            ArriveToStartingNode();
        }

        InvokeRepeating(nameof(SaveGame), 30.0f, 30.0f); // Repeat save every 30 seconds
    }
    private void Awake()
    {
        sceneController = GameObject.Find("Scene Controller").GetComponent<SceneController>();
        
    }

    protected void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (currentWaypoint != null)
            {
                if (currentWaypoint.returnNode != null)
                {
                    currentWaypoint.Leave();
                    currentWaypoint.returnNode.Arrive(instant: false);
                }
                else
                {
                    SceneController.Instance.LoadLastScene();
                    //SaveGameManager.LoadSceneFromState(lastSceneState);
                }
            }
        }

        // TODO: Debug only?
        if (Input.GetKeyDown(KeyCode.S))
        {
            SaveGame();
        }

        //DEBUG CLICK
        //if (Input.GetMouseButtonDown(0))
        //{
        //    RaycastHit hit;
        //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        //    if (Physics.Raycast(ray, out hit))
        //    {
        //        print($"{hit.transform.name} object is clicked by mouse");
        //    }
        //}
    }

    private void SaveGame()
    {
        StartCoroutine(SaveGameManager.SaveGameCoroutine());
    }

    public static void MovePlayerTo(Waypoint waypoint, bool instant)
    {
        if (Instance.currentWaypoint != null)
        {
            Instance.currentWaypoint.Leave();
        }
        Instance.currentWaypoint = waypoint;
        Instance.cameraControl.AlignTo(waypoint.cameraView, instant);
    }

    public bool IsClickOnScene()
    {
        return inventoryPanel.ContainsScreenPoint(Input.mousePosition);
    }

    public void StartDragItem(InventoryItem item)
    {
        SetDropAccept(true);
        itemDragged = item;
    }

    public void StopDragItem()
    {
        SetDropAccept(false);
        itemDragged = null;
    }

    private void SetDropAccept(bool isEnabled)
    {
        if (!currentWaypoint)
        {
            return;
        }

        foreach (DropAcceptable dropAcceptable in currentWaypoint.transform.GetComponentsInChildren<DropAcceptable>())
        {
            dropAcceptable.IsEnabled = isEnabled;
        }

        foreach (Clickable clickable in currentWaypoint.transform.GetComponentsInChildren<Clickable>()
            .Where(dr => dr.gameObject != currentWaypoint.trigger.gameObject))
        {
            clickable.IsEnabled = !isEnabled;
        }
    }

    public void Save(GameState state)
    {
        Debug.Log("Saving GameManager");
        state.ActiveScene = gameObject.scene.name;
        state.ActiveWaypoint = currentWaypoint ? currentWaypoint.uniqueId : string.Empty;
        state.ActiveScene = SceneManager.GetActiveScene().name;
    }

    public void Load(GameState state)
    {
        //Assert.AreEqual(state.ActiveScene, gameObject.scene.name, "state.ActiveScene does not match Loaded Scene. Trying to load wrong scene?");
        if (!string.IsNullOrWhiteSpace(state.ActiveWaypoint))
        {
            foreach (Waypoint wp in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Waypoint>()))
            {
                if (wp.uniqueId == state.ActiveWaypoint)
                {
                    wp.Arrive(instant: true);
                    return;
                }
            }
        }

        ArriveToStartingNode();
    }

    public void ArriveToStartingNode()
    {
        Assert.IsTrue(SceneManager.GetActiveScene().name == SceneDictionary.MainMenu || startingNode, "No starting Waypoint defined");
        
        if (startingNode)
        {
            startingNode.Arrive(instant: true);
        }
    }

}
