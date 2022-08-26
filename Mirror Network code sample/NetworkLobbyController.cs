using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using UnityEngine.SceneManagement;

public class NetworkLobbyController : MonoBehaviourPunCallbacks, IConnectionCallbacks, ILobbyCallbacks
{
    #region CONST VALUES
    UILobbyController uiLobbyControllerInstance;
    const string GameVersion = "0.1"; // makes sure if players are on different versions, they can't connect toghter
    const int minPlayersPerRoom = 1, maxPlayersPerRoom = 4;
    const  string customNameKey = "n";
    const  string customToggleKey = "t";
    #endregion
    bool isConnecting; // are we currently trying to connect
    private ExitGames.Client.Photon.Hashtable custonRoomProperties = new ExitGames.Client.Photon.Hashtable();
    private void Awake()
    {
        // when you load a new sence for someone, load the same scene to everyone in that room
        PhotonNetwork.AutomaticallySyncScene = true;
    }
    private void Start()
    {
        uiLobbyControllerInstance = UILobbyController.instance;
    }
    public void ConnectToPhotonServer()
    {
        uiLobbyControllerInstance.OpenLoadingPanal();
        if (!PhotonNetwork.IsConnected)
        {
            // connect using the app id we set up
            PhotonNetwork.GameVersion = GameVersion;
            PhotonNetwork.ConnectUsingSettings();
        }
    }
    void InstatiatePlayer()
    {
        Debug.Log("Spawned a player");
        UILobbyController.index = PhotonNetwork.CurrentRoom.Players.Count;
        PhotonNetwork.Instantiate("Player", new Vector3(0f, 0f, 0f), Quaternion.identity, 0);
    }
    void OnPhotonInstantiate(PhotonMessageInfo info)
    {

    }
    public void TryConnectingAgainButton()
    {
        // this method will be used only at start, before any annitial connection to the srever has been made
        ConnectToPhotonServer();
    }
    public void CreateRoom()
    {
        // handle the time limit toggle
        string roomCode = CodeGenaretor.GenareteCode();
        string roomName = uiLobbyControllerInstance.roomNameInputField.text;
        roomName = (roomName.Equals(string.Empty)) ? "Room " + Random.Range(1000, 10000) : roomName;
        custonRoomProperties[customNameKey] =roomName;
        custonRoomProperties[customToggleKey] =uiLobbyControllerInstance.timeLimitToggle.isOn;
        RoomOptions roomOptions = new RoomOptions() { IsVisible = true, IsOpen = true, MaxPlayers = 6  ,CustomRoomProperties = custonRoomProperties};
        PhotonNetwork.CreateRoom(roomCode, roomOptions, TypedLobby.Default);
    }
    public void JoinRoom()
    {
        if (CodeGenaretor.CheckIfCodeExists(uiLobbyControllerInstance.codeInputField.text))
        {
            uiLobbyControllerInstance.OpenLoadingPanal();
            PhotonNetwork.JoinRoom(uiLobbyControllerInstance.codeInputField.text);
        }
    }

    public void LeaveRoom()
    {
        Player.localPlayer.playerIndex = -1;
        PhotonNetwork.LeaveRoom();
        // when you leave a room, you connect to the lobby automatically
    }
    public void BackButtonClicked()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
    }
    public void DisconnectPlayerFromPhotonServer()
    {

    }
    IEnumerator DisconnectAndLoadStart()
    {
        PhotonNetwork.Disconnect();
        // after disconnected, open start\ menu scene
        while (PhotonNetwork.IsConnected)
            yield return null;
        //LoadSceneLocally("Start");
    }

    #region OVERRIDEN METHODS
    public override void OnConnectedToMaster()
    {
        uiLobbyControllerInstance.connectionErrorMessage.gameObject.SetActive(false);
        uiLobbyControllerInstance.tryAgainBtn.gameObject.SetActive(false);
        Debug.Log("Connecting to master");
        uiLobbyControllerInstance.OpenJoinOrCreatePanal();
        PhotonNetwork.JoinLobby();
    }
    public override void OnCreatedRoom()
    {
        InstatiatePlayer();
        string name = PhotonNetwork.CurrentRoom.CustomProperties[customNameKey].ToString(); 
        string code = PhotonNetwork.CurrentRoom.Name;
        Debug.Log("Created new room with code "+ code + " and name " + name);
        uiLobbyControllerInstance.UpdateWaitRoomUI();
    }
    // happens when the host creates a room or when any played joins it 
    public override void OnJoinedRoom()
    {
        Debug.Log("succesfuly joined a room");
        uiLobbyControllerInstance.CloseLoadingPanal();
        uiLobbyControllerInstance.OpenWaitPanal();
        uiLobbyControllerInstance.UpdateWaitRoomUI();
        uiLobbyControllerInstance.SpwanUIPlayerLobby();
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount; // num of players in current room
        if (Player.localPlayer == null)
        // meaning we are yet to spawn a player
        {
            // spawn it 
            InstatiatePlayer();
        }
        Player.localPlayer.playerIndex = playerCount;
        if (playerCount < minPlayersPerRoom)
        {
            uiLobbyControllerInstance.messageTxt.gameObject.SetActive(true);
            uiLobbyControllerInstance.messageTxt.text = "Not enough players in room. Waiting for " + (minPlayersPerRoom - playerCount) + " more players";
        }
        else if (playerCount <= maxPlayersPerRoom)
        {
            Debug.Log("Match is ready to begin");
            uiLobbyControllerInstance.messageTxt.gameObject.SetActive(false);
            uiLobbyControllerInstance.startGameBtn.interactable = true;
        }
    }

    // Called only when a remote player entered the room. called on ALL OTHER PLAYERS EXCEPT LOCAL
    public override void OnPlayerEnteredRoom(Photon.Realtime.DemoPlayer newPlayer)
    {
        // activate the start game button on the waiting panal
        //only for the host!!
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount >= minPlayersPerRoom)
        {
            uiLobbyControllerInstance.startGameBtn.interactable = true;
            uiLobbyControllerInstance.messageTxt.gameObject.SetActive(false);
        }
    }
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        uiLobbyControllerInstance.unvalidCodeMsg.gameObject.SetActive(true);
        Debug.LogFormat("OnJoinRoomFailed bc {0}", message);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogFormat("OnCreateRoomFailed bc {0}", message);
    }
    public override void OnLeftLobby()
    {
        Debug.LogFormat("OnLeftLobby()"); 
    }
    public override void OnLeftRoom()
    {
        Player.localPlayer.playerIndex = -1;
        uiLobbyControllerInstance.OpenJoinOrCreatePanal();
    }
    public override void OnPlayerLeftRoom(Photon.Realtime.DemoPlayer otherPlayer)
    {
        StartCoroutine(uiLobbyControllerInstance.DisplayPlayerLeftRoomMsg(otherPlayer.NickName + " has left the game"));
        // open a panal with a message that this player has left the room
        // if one player only is left in the room, declare it as the winner
       
        Debug.LogFormat("OnPlayerLeftRoom() {0}", otherPlayer.NickName); // seen when other disconnects
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.LogFormat("OnPlayerLeftRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom
        }
    }

    /*private IEnumerator MoveToGameScene()
    {
        // call this when you get the rpc to load level
        // Temporary disable processing of futher network messages
        PhotonNetwork.IsMessageQueueRunning = false;
        LoadNewScene(newSceneName); // custom method to load the new scene by name
        while (newSceneDidNotFinishLoading)
        {
            yield return null;
        }
        PhotonNetwork.IsMessageQueueRunning = true;
    }
    */
    void OnConnectionFail(DisconnectCause cause)
    {
        Debug.LogWarningFormat(this, "OnConnectionFail, cause {0}", cause);
    }
    #endregion
}