using UnityEngine;
using Mirror;
using System;
using UnityEngine.SceneManagement;

public class NetworkPlayer : NetworkBehaviour
/* This is a snippet of code from a multiplayer game that has digitilized a US tv game show.
This is the code from the lobby - controlling hosting a game, joining a game that already exists by clicking on its container(UI object), and more. 
Multiplayer features were achieved by me only using Mirror*/
{
    public static NetworkPlayer localPlayer = null;

    [Header("Lobby")]
    public string playerName = "";
    [SyncVar] public int playerAmount = 0;
    [SyncVar] public bool isHost = false;
    [Header("Mirror")]
    [SyncVar] public string matchID;
    [SyncVar] public string playerID;
    [SyncVar] public bool hasAnswered;
    int playerIndex;
    private UILobbyController uiLobby;

    [Header("Game")]
    UIPlayerController uiPlayer;
    NetworkMatchChecker matchChecker;
    internal UIGameController uiGame;
    internal bool isSumbiting, isBuzzing, canDecide, canContinue;

    void Start()
    {
        if (isLocalPlayer)
            localPlayer = this;
        DontDestroyOnLoad(this.gameObject);
        if (isLocalPlayer)
        {
            this.playerID = CreateRandomID();
            CmdUpdatePlayerId(this.playerID);
            this.uiLobby = GameObject.Find("UI Lobby Controller").GetComponent<UILobbyController>();
            this.playerAmount = 0;
            if (isClient && this.uiLobby != null && this.uiLobby.lobbyPanal.activeSelf)
                CmdUpdateGameRoomList();
            // for testing purpeses, if we are startubg from the game and not the lobby
            else
            {
                this.uiGame = GameObject.Find("UI Game Controller").GetComponent<UIGameController>();
                StartUIIfConnected.instance.ActivateUI();
            }
        }
    }
    void OnEnable()
    {
        // change after demo to check who is the local player
        this.playerAmount = 0;
        this.isHost = false;
        this.hasAnswered = false;
    }
    string CreateRandomID()
    {
        string id = "";
        for (int i = 0; i < 10; i++)
        {
            int rnd = UnityEngine.Random.Range(0, 36);
            if (rnd < 26)
            // meaning it's a letter
            {
                id += (char)(rnd + 65);
            }
            else
            // its a numer
            {
                id += (rnd - 26).ToString(); // subtracting to make it into a number 
            }
        }
        Debug.Log("Random ID is " + id);
        return id;
    }

    #region PASSING VALUES TO SERVER THEN THE SYNC VAR TRANSFERS THE DATA TO ALL CLIENTS METHODS
    // syncvar are  synchronized from the server to clients
    [Command]
    void CmdUpdatePlayerId(string newId)
    {
        this.playerID = newId;
    }
    [Command]
    void CmdUpdateMatchID(string newId)
    {
        this.matchID = newId;
    }
    [Command]
    void CmdUpdateIsHost(bool host, string matchId, string playerId)
    {
        this.isHost = host;
        SyncListGameObject players = MatchMaker.instance.FindMatchById(matchId).playersInThisMatch;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].GetComponent<NetworkPlayer>().playerID == playerId)
            {
                MatchMaker.instance.FindMatchById(matchId).playersInThisMatch[i].GetComponent<NetworkPlayer>().isHost = host;
                Debug.LogError("Sucsesfully changed player has answered in matchmaker for id " + matchID);
            }
        }
    }

    #endregion
    #region HOST
    public void PlayerHostGame()
    {
        string id = MatchMaker.CreateRandomID();
        string gameName = uiLobby.gameNameIP.text;
        if (string.IsNullOrEmpty(gameName))
        {
            gameName = "Game #" + (uiLobby.CountGameContainers() + 1).ToString();
            //Debug.LogError("No game name", this);
        }
        CmdHostGame(id, int.Parse(uiLobby.gameSizeTxt.text), gameName, localPlayer.gameObject);
    }
    [Command] // call this from a client to run it on the server
    // the problem was that we were passing in the player clone that was on the server and not the local player that called the method
    void CmdHostGame(string id, int gameSize, string gameName, GameObject player)
    {
        // tell the server we got an id, please register a new game and add this player to the list 
        if (MatchMaker.instance.AddAndApproveHostingGame(id, gameName, gameSize, player.gameObject))
        // if valadation passed
        {
            CountParticipentContainers(id);
            TargetHostGame(true, id, gameSize, gameName, MatchMaker.RegularIDToGUI(id));
            Debug.Log("Server - Sucssesfly hosted a game");
            // add a game container
        }
        else
        {
            TargetHostGame(false, id, gameSize, gameName, MatchMaker.RegularIDToGUI(id));
            Debug.LogError("Server- Couldn't host game", this);
        }
    }

    [TargetRpc] // the server will run this on a specific client
    void TargetHostGame(bool success, string id, int gameSize, string gameName, Guid guid)
    {
        if (success)
        {
            this.playerIndex = 1;
            localPlayer.isHost = true;
            CmdUpdateIsHost(true, id, localPlayer.matchID);
            CmdAddGameContainer(id, gameName, gameSize, guid);
            localPlayer.matchID = id;
            CmdUpdateMatchID(localPlayer.matchID);
            GameObject transferData = Instantiate(localPlayer.uiLobby.transferDataToGamePrefab);
            transferData.name = "Transfer Data";
            localPlayer.uiLobby.NewGameStartGameButton();
            Debug.Log("Client - Sucssesfly hosted a game with name " + TransferDataToGame.instance.gameName + " and id " + id + " and participent number " + TransferDataToGame.instance.gameSize.ToString());
        }
        else
        {
            localPlayer.matchID = null;
            CmdUpdateMatchID(localPlayer.matchID);
            Debug.LogError("Client- Player couldnt host game");
        }
    }
    public void PlayerCancelHosting(string id)
    {
        CmdCancelHost(id);
    }
    [Command] // call this from a client to run it on the server
    void CmdCancelHost(string id)
    {
        Debug.Log("CmdCancelhost");
        // tell the server to tell all clients to update 
        RpcDeleteGameContainer(id);
        SyncListGameObject players = MatchMaker.instance.FindMatchById(id).playersInThisMatch;
        for (int i = 0; i < players.Count; i++)
        {
            TargetPlayerLeaveGameFromLobby(players[i].GetComponent<NetworkIdentity>().connectionToClient);
        }
        // tell the server we got an id, please register a new game and add this player to the list 

        if (MatchMaker.instance.DeleteMatch(id))
        // if valadation passed
        {
            TargetCancelHost(true, id);
            RcpCountGameContainers();
            Debug.Log("Server - Sucssesfly canceled a hosting");
        }
        else
        {
            TargetCancelHost(false, id);
            Debug.LogError("Server- Couldn't host game", this);
        }
    }

    [TargetRpc] // the server will run this on a specific client
    void TargetCancelHost(bool success, string id)
    {
        if (success)
        {

            this.playerIndex = -1;
            localPlayer.matchID = null;
            CmdUpdateMatchID(localPlayer.matchID);
            localPlayer.uiLobby.hostGamePanal.SetActive(false);
            localPlayer.uiLobby.lobbyPanal.SetActive(true);
            localPlayer.uiLobby.CountGameContainers();
        }
        else
        {
            Debug.LogError("Client- Player couldnt  delete host");
        }
    }

    [ClientRpc]
    void RcpCountGameContainers()
    {
        localPlayer.uiLobby.CountGameContainers();

    }
    #endregion

    #region JOIN
    public void PlayerJoinGame(string idToJoin)
    {
        CmdJoinGame(idToJoin);
    }

    [Command] // call this from a client to run it on the server
    void CmdJoinGame(string id)
    // tell the server we got an id, please register a new game and add this player to the list 
    {
        if (MatchMaker.instance.AddAndApproveJoiningGame(id, this.gameObject))
        // if valadation passed
        {
            Debug.LogError(MatchMaker.instance.FindMatchById(id).playersInThisMatch.Count);
            Debug.Log("Server- Sucssesfly joined game");
            TargetJoinGame(true, id, MatchMaker.instance.FindMatchById(id), MatchMaker.RegularIDToGUI(id), MatchMaker.instance.FindMatchById(id).playersInThisMatch.Count - 1);
            CountParticipentContainers(id);
        }
        else
        {
            Debug.LogError("Server- Couldn't join game", this);
            TargetJoinGame(false, id, MatchMaker.instance.FindMatchById(id), MatchMaker.RegularIDToGUI(id), MatchMaker.instance.FindMatchById(id).playersInThisMatch.Count - 1);
        }
    }

    [TargetRpc] // the server will run this on a specific client
    void TargetJoinGame(bool success, string id, Match match, Guid GuiId, int index)
    {
        if (success)
        {
            this.playerIndex = index;
            Debug.LogError("player index is " + index);
            localPlayer.matchID = id;
            CmdUpdateMatchID(localPlayer.matchID);
            Debug.Log("Client- Game joining was sucsesfull with id " + id.ToString());
            localPlayer.uiLobby.OpenJoinPanalWithId(index, match.maxGameSize);
            CmdAddJoinContainerLocally(id, localPlayer.playerID);

            // add my contaniner to the host 
            CmdAddHostContainer(id, localPlayer.playerID);
            // add my container to the other players who have joined
            CmdAddJoinContainerToPlayersWhoAlreadyJoined(id, localPlayer.playerID);
            uiLobby.ChangeGamecontainerParticipentNumTxt(id, match.playersInThisMatch.Count, match.maxGameSize);
        }
        else
        {
            Debug.Log("Client- Game joining was not sucsesful");
        }
    }
    [Command]
    void CmdAddJoinContainerLocally(string id, string playerId)
    {
        SyncListGameObject players = MatchMaker.instance.FindMatchById(id).playersInThisMatch;
        int numOfPlayers = players.Count;
        // calling the target rpc to instatiate the needed join containers for ON MY SCREEN ONLY 
        for (int i = 0; i < numOfPlayers; i++)
        {
            if (players[i].GetComponent<NetworkPlayer>().isHost == true)
            {
                string hostName = players[i].GetComponent<NetworkPlayer>().playerName;
                if (string.IsNullOrEmpty(hostName))
                    hostName = "Host";
                TargetAddJoinContainerLocally(id, true, hostName, players[i].GetComponent<NetworkPlayer>().playerID);
                break;
            }
        }
        TargetAddJoinContainerLocallyWithIF(id);
        for (int i = 0; i < numOfPlayers; i++)
        {
            if (players[i].GetComponent<NetworkPlayer>().isHost == false && players[i].GetComponent<NetworkPlayer>().playerID != playerId)
            {
                string playerName = players[i].GetComponent<NetworkPlayer>().playerName;
                if (string.IsNullOrEmpty(playerName))
                    playerName = "Player";
                TargetAddJoinContainerLocally(id, false, playerName, players[i].GetComponent<NetworkPlayer>().playerID);
            }
        }
    }
    [Command]
    void CmdAddJoinContainerToPlayersWhoAlreadyJoined(string id, string playerId)
    {
        SyncListGameObject players = MatchMaker.instance.FindMatchById(id).playersInThisMatch;

        for (int i = 0; i < players.Count; i++)
        {
            if (playerId != players[i].GetComponent<NetworkPlayer>().playerID) // if it is not me 
                TargetCmdAddJoinContainerToPlayersWhoAlreadyJoined(players[i].GetComponent<NetworkIdentity>().connectionToClient, id, playerId);
        }
    }
    [TargetRpc]
    void TargetCmdAddJoinContainerToPlayersWhoAlreadyJoined(NetworkConnection target, string id, string playerId)
    {
        localPlayer.uiLobby.AddJoinParticipentConteiner(id, false, "Player", playerId);

    }

    [TargetRpc]
    void TargetAddJoinContainerLocally(string id, bool host, string playerName, string playerId)
    {
        localPlayer.uiLobby.AddJoinParticipentConteiner(id, host, playerName, playerId);
    }

    [TargetRpc]
    void TargetAddJoinContainerLocallyWithIF(string id)
    {
        localPlayer.uiLobby.AddJoinParticipentConteinerWithInput(id, localPlayer.playerID);

    }
    public void PlayerCancelJoin(string id)
    {
        CmdCancelJoin(id, this.playerID);
    }
    [Command] // call this from a client to run it on the server
    void CmdCancelJoin(string id, string playerId)
    {
        // tell the server we got an id, please register a new game and add this player to the list 
        if (MatchMaker.instance.RemovePlayerFromMatch(id, this))
        // if valadation passed
        {
            DeleteHostContainer(id, playerId);
            CountParticipentContainers(id);
            this.playerIndex = -1;
            TargetCancelJoin(true, id, MatchMaker.instance.FindMatchById(id));
            Debug.Log("Server - Sucssesfly canceled a joining");
        }
        else
        {
            TargetCancelJoin(false, id, MatchMaker.instance.FindMatchById(id));
            Debug.LogError("Server- Couldn't cancel join game", this);
        }
    }

    [TargetRpc] // the server will run this on a specific client
    void TargetCancelJoin(bool success, string id, Match match)
    {
        if (success)
        {
            //localPlayer.gameObject.GetComponent<NetworkMatchChecker>().matchId = new Guid();
            //CmdUpdateMatchChecker(new Guid());
            Debug.Log("Client - Sucssesfly cancaled joining");
            localPlayer.uiLobby.hostGamePanal.SetActive(false);
            localPlayer.uiLobby.joinGamePanal.SetActive(false);
            localPlayer.uiLobby.lobbyPanal.SetActive(true);
            localPlayer.matchID = null;
            localPlayer.uiLobby.ClearJoinContainers();
            CmdUpdateMatchID(localPlayer.matchID);
            uiLobby.ChangeGamecontainerParticipentNumTxt(id, match.playersInThisMatch.Count, match.maxGameSize);
            //Player.localPlayer.CmdUpdateGameRoomList();

            /*// if we were supposed to be the last player and the match maker script has deleted the game container, add it back in
            if (match.playersInThisMatch.Count == match.maxGameSize)
            {
                CmdAddGameContainer(id, match.gameName, match.playersInThisMatch.Count, match.maxGameSize);
            }*/
        }
        else
        {
            Debug.LogError("Client- Player couldnt  delete host");
        }
    }


    #endregion

    #region BEGIN GAME
    public void PlayerBeginGame()
    {
        Debug.Log("Begine");
        CmdBeginGame(localPlayer.matchID);
    }
    [Command] // call this from a client to run it on the server
    void CmdBeginGame(string id)
    {
        //MatchMaker.instance.BegineGame(matchID);
        Match thisMatch = MatchMaker.instance.FindMatchById(id);
        for (int i = 0; i < thisMatch.playersInThisMatch.Count; i++)
        {
            TargetBeginGame(thisMatch.playersInThisMatch[i].GetComponent<NetworkIdentity>().connectionToClient, MatchMaker.RegularIDToGUI(id));
        }
    }

    [TargetRpc] // the server will run this on a specific client
    void TargetBeginGame(NetworkConnection target, Guid guid)
    {
        // instatiate a match chcker with the correct id
        this.matchChecker = localPlayer.gameObject.AddComponent(typeof(NetworkMatchChecker)) as NetworkMatchChecker;
        this.matchChecker.matchId = guid;
        // spawn a turn manager
        //GameObject turnManager = Instantiate(localPlayer.uiLobby.turnMangerPrefab);
        //TurnManager turnManagerScript = turnManager.GetComponent<TurnManager>();
        //turnManagerScript.AddPlayer(localPlayer);
        //turnManager.GetComponent<NetworkMatchChecker>().matchId = RegularIDToGUI(id);
        // take all players to the game
        SceneManager.LoadScene("PlayerBoard");
        if (localPlayer.isHost)
            CmdDistributeDataForFirstBoard();
    }


    #endregion

    #region CONTAINERS
    [Command]
    internal void CmdAddGameContainer(string id, string gameName, int maxPlayers, Guid guid)
    {
        Debug.Log("cmdCmdAddGameContainer");
        RpcAddGameContainer(id, gameName, maxPlayers);
        //TargetChangeMatchMakerMatchId(guid);
    }
    [ClientRpc]
    private void RpcAddGameContainer(string id, string gameName, int maxPlayers)
    {
        Debug.Log("RpcAddGameContainer");
        localPlayer.uiLobby.AddGameContainer(id, gameName, 1, maxPlayers);
        Debug.LogError(isServer + " Rpc AddGameContainer");
    }
    /*
    [TargetRpc]
    void TargetChangeMatchMakerMatchId(Guid guid)
    {
        Debug.LogError("Changing match checker id");
        localPlayer.gameObject.GetComponent<NetworkMatchChecker>().matchId = guid;
        CmdUpdateMatchChecker(guid);
    }
    [Command]
    void CmdUpdateMatchChecker(Guid GuiId)
    {
        this.gameObject.GetComponent<NetworkMatchChecker>().matchId = GuiId;

    }*/

    [ClientRpc]
    private void RpcDeleteGameContainer(string id)
    {
        localPlayer.uiLobby.DeleteGameContainer(id);
    }
    [TargetRpc]
    void TargetPlayerLeaveGameFromLobby(NetworkConnection target)
    {
        localPlayer.uiLobby.joinGamePanal.SetActive(false);
        localPlayer.uiLobby.lobbyPanal.SetActive(true);
        localPlayer.uiLobby.ClearHostContainers();
        localPlayer.uiLobby.ClearJoinContainers();
    }

    [Command]
    void CmdAddJoinContainer(string id, string playerId)
    {
        // what to do- 
        // each time a player has joined with this id
        // call a client rcp to each one of the game participent in this id, telling them a player has joined or left 
        Debug.Log(MatchMaker.instance.FindMatchById(id) == null);
        Debug.Log(id);
        SyncListGameObject playersInThisMatch = MatchMaker.instance.FindMatchById(id).playersInThisMatch;
        Debug.Log(playersInThisMatch.Count);
        for (int i = 0; i < playersInThisMatch.Count; i++)
        {
            // add my contaniner to everyone else
            Debug.Log(playersInThisMatch[i].GetComponent<NetworkIdentity>().connectionToClient);
            TargetAddJoinParticipentContainer(playersInThisMatch[i].GetComponent<NetworkIdentity>().connectionToClient, id, playerId);
        }
    }


    [TargetRpc]
    private void TargetAddJoinParticipentContainer(NetworkConnection target, string id, string playerId)
    {
        Debug.LogError(" target rpc adding host participent " + localPlayer.playerID);
        localPlayer.uiLobby.AddJoinParticipentConteiner(id, localPlayer.isHost, "Player", playerId);
    }
    void DeleteJoinContainer(string id, string playerId)
    {
        // tell the server to delete my game container to everyone in this match
        SyncListGameObject playersInThisMatch = MatchMaker.instance.FindMatchById(id).playersInThisMatch;
        for (int i = 0; i < playersInThisMatch.Count; i++)
        {
            // add my contaniner to everyone else
            if (playersInThisMatch[i].GetComponent<NetworkPlayer>() != this)
                TargetDeleteJoinContainer(playersInThisMatch[i].GetComponent<NetworkPlayer>().connectionToClient, id, playerId);
        }
    }
    [TargetRpc]
    void TargetDeleteJoinContainer(NetworkConnection target, string id, string playerId)
    {
        localPlayer.uiLobby.DeleteJoinParticipentContainer(id, playerID);
    }
    [Command]
    void CmdAddHostContainer(string id, string playerId)
    {
        SyncListGameObject playersInThisMatch = MatchMaker.instance.FindMatchById(id).playersInThisMatch;
        for (int i = 0; i < playersInThisMatch.Count; i++)
        {
            // add my contaniner to everyone else
            Debug.Log(playersInThisMatch[i].GetComponent<NetworkIdentity>().connectionToClient);
            TargetAddHostParticipentContainer(playersInThisMatch[i].GetComponent<NetworkIdentity>().connectionToClient, id, playerId); ;
        }
    }

    [TargetRpc]
    private void TargetAddHostParticipentContainer(NetworkConnection target, string id, string playerId)
    {
        if (localPlayer.isHost)
        {
            Debug.LogError(" target rpc adding host participent ");
            localPlayer.uiLobby.AddHostParticipentContainer(id, "Player", playerId);
        }
    }
    void DeleteHostContainer(string id, string playerId)
    {
        // tell the server to delete my game container to everyone in this match
        SyncListGameObject playersInThisMatch = MatchMaker.instance.FindMatchById(id).playersInThisMatch;
        for (int i = 0; i < playersInThisMatch.Count; i++)
        {
            // delete my contaniner to everyone else
            TargetDeleteHostContainer(playersInThisMatch[i].GetComponent<NetworkPlayer>().connectionToClient, id, playerId);
        }
    }

    [TargetRpc]
    void TargetDeleteHostContainer(NetworkConnection target, string id, string playerId)
    {
        localPlayer.uiLobby.DeleteHostParticipentContainer(id, playerId); ;
    }


    [Command]
    internal void CmdUpdateGameRoomList()
    {
        MatchMaker instance = MatchMaker.instance;

        for (int i = 0; i < instance.allGames.Count; i++)
        {
            TargetUpdateGameRoomList(instance.allGames[i].matchId, instance.allGames[i].gameName, instance.allGames[i].playersInThisMatch.Count, instance.allGames[i].maxGameSize);
        }
    }
    [TargetRpc]
    void TargetUpdateGameRoomList(string id, string gameName, int currentPlayers, int maxPlayers)
    {
        localPlayer.uiLobby.AddGameContainer(id, gameName, currentPlayers, maxPlayers);
    }

    internal void CountParticipentContainers(string id)
    {
        if (MatchMaker.instance.FindMatchById(id) == null)
            Debug.LogError("match id does not exist in match maker", this);
        SyncListGameObject playersInThisMatch = MatchMaker.instance.FindMatchById(id).playersInThisMatch;
        for (int i = 0; i < playersInThisMatch.Count; i++)
        {
            TargetCountParticipentContainers(playersInThisMatch[i].GetComponent<NetworkPlayer>().connectionToClient, MatchMaker.instance.FindMatchById(id).playersInThisMatch.Count, MatchMaker.instance.FindMatchById(id).maxGameSize);
        }
    }
    [TargetRpc]
    void TargetCountParticipentContainers(NetworkConnection target, int current, int max)
    {
        if (localPlayer.isHost == true)
            localPlayer.uiLobby.hostParticipentNumTxt.text = (current - 1) + "/" + max; // (current-1) -1 because we don't iclude the host as a participent
        else
            localPlayer.uiLobby.joinParticipentNumTxt.text = (current - 1) + "/" + max; //(current - 1) - 1 because we don't iclude the host as a participent

    }

    #endregion
    #region CHANGE NAME
    [Command]
    public void CmdUpdateMyJoinContainerName(string id, string thisplayerId, string playerName)
    {
        SyncListGameObject playersInThisMatch = MatchMaker.instance.FindMatchById(id).playersInThisMatch;
        for (int i = 0; i < playersInThisMatch.Count; i++)
        {
            if (playersInThisMatch[i].GetComponent<NetworkPlayer>().playerID != thisplayerId)
                TargetUpdateMyJoinContainerName(playersInThisMatch[i].GetComponent<NetworkIdentity>().connectionToClient, thisplayerId, playerName);
        }
    }
    [TargetRpc]
    void TargetUpdateMyJoinContainerName(NetworkConnection target, string thisplayerId, string playerName)
    {
        localPlayer.uiLobby.UpdateJoinContainerName(thisplayerId, playerName);
        Debug.LogError("TargetUpdateMyJoinContainerName");
    }
    [Command]
    public void CmdUpdateMyHostContainerName(string id, string thisplayerId, string playerName)
    {
        SyncListGameObject playersInThisMatch = MatchMaker.instance.FindMatchById(id).playersInThisMatch;
        for (int i = 0; i < playersInThisMatch.Count; i++)
        {
            if (playersInThisMatch[i].GetComponent<NetworkPlayer>().playerID != thisplayerId)
                TargetUpdateMyHostContainerName(playersInThisMatch[i].GetComponent<NetworkIdentity>().connectionToClient, thisplayerId, playerName);
        }
    }
    [TargetRpc]
    void TargetUpdateMyHostContainerName(NetworkConnection target, string playerId, string playerName)
    {
        if (localPlayer.isHost)
        {
            Debug.LogError("TargetUpdateMyHostContainerName");
            localPlayer.uiLobby.UpdateHostContainerName(playerId, playerName);

        }
    }
    /*
    internal void PlayerChangeName()
    {

    }

    [Command]
    void CmdChangePlayerName(string id)
    {
        SyncListGameObject playersInThisMatch = MatchMaker.instance.FindMatchById(id).playersInThisMatch;
        for (int i = 0; i < playersInThisMatch.Count; i++)
        {
            if (playersInThisMatch[i].GetComponent<Player>().playerID != playerID)
                TargetChangePlayerName(playersInThisMatch[i].GetComponent<NetworkIdentity>().connectionToClient, playerID, playerName);
        }
    }

    [TargetRpc]
    void TargetChangePlayerName(NetworkConnection target)
    {
        localPlayer.uiLobby.UpdatePlayerNameIP();
    }
    */
    #endregion
}
