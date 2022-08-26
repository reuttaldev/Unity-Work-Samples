using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using System;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    [Header("Values")]
    public static Player localPlayer;
    public string playerName;
    public int playerIndex = 0;
    // change this to sync over the network using OnPhotonSerializeView
    [SerializeField]
    internal int numOfFourth = 0;
    internal List<CardEntity> hand = new List<CardEntity>();
    public Dictionary<string, int> mySubCatagories = new Dictionary<string, int>(); // catagories I have in my hand
    string lastAskedCatagory = "";
    // change to set each player score text at start
    private const string PlayerPrefsNameKey = "PlayerName";
    internal PhotonView photonView;
    PhotonMessageInfo nullPhotonMessage = new PhotonMessageInfo();

    // player = local player
    // ai = actually AI or any other player that is not local
    // RPC is acceuted only on that player who is the target
    // CALL THE RPC ON THE TARGET THEN CALL IT ON EVERYONE ELSE TO OD THE FAKE STUFF

    private void Awake()
    {
        if (PhotonNetwork.OfflineMode == false)
        {
            DontDestroyOnLoad(this);
            photonView = gameObject.GetComponent<PhotonView>();
            playerIndex = UILobbyController.index -1;
            playerName = "Player " + playerIndex.ToString(); 
            gameObject.name = "Player " + playerIndex.ToString();
            if (photonView.IsMine)
            {
                localPlayer = this;
            }
        }
    }
    private void OnEnable()
    {
        lastAskedCatagory = "";
        numOfFourth = 0;
    }
    internal void SetPlayerUI()
    {
        Debug.Log("setting player ui");
        PlayerScore score;
        if (PhotonNetwork.OfflineMode)
        {
            // AI
            if (playerName == "Computer")
            {
                PlayerUIController controller = UIGameController.instance.PlayerUIInScene[1];
                controller.nickName = "Computer";
                UIGameController.instance.PlayerUIInScene[1].gameObject.SetActive(true);
                score = UIGameController.instance.PlayerScoreInScene[0];
                controller.playerScoreInstance = score;
            }
            //Local
            else
            {
                PlayerUIController controller = UIGameController.instance.PlayerUIInScene[3];
                controller.nickName = playerName;
                controller.bubble = GameObject.Find("Local Bubble");
                controller.bubble.SetActive(true);
                controller.bubbleText = GameObject.Find("Local Bubble Txt").GetComponent<TMP_Text>();
                score = UIGameController.instance.PlayerScoreInScene[1];
                controller.playerScoreInstance = score;
                StartCoroutine("StartGame");
                Debug.LogError(localPlayer == this);
            }
            score.nameTxt.text = playerName;
            score.gameObject.SetActive(true);
        }
        else
        {
            PlayerUIController controller = UIGameController.instance.PlayerUIInScene[3];
            controller.nickName = playerName;
            controller.bubble = GameObject.Find("Local Bubble");
            controller.bubble.SetActive(true);
            controller.bubbleText = GameObject.Find("Local Bubble Txt").GetComponent<TMP_Text>();

            int length = PhotonNetwork.PlayerListOthers.Length;
            Debug.LogError(length);
            if (length == 1)
            {
                controller = UIGameController.instance.PlayerUIInScene[1];
                controller.nickName = PhotonNetwork.PlayerListOthers[0].NickName;
                Debug.LogError(PhotonNetwork.PlayerListOthers[0].NickName);
                UIGameController.instance.PlayerUIInScene[1].gameObject.SetActive(true);
            }
            else if (length == 2)
            {
                 controller = UIGameController.instance.PlayerUIInScene[0];
                controller.nickName = PhotonNetwork.PlayerListOthers[0].NickName;
                UIGameController.instance.PlayerUIInScene[0].gameObject.SetActive(true);
                controller = UIGameController.instance.PlayerUIInScene[2];
                controller.nickName = PhotonNetwork.PlayerListOthers[1].NickName;
                UIGameController.instance.PlayerUIInScene[2].gameObject.SetActive(true);
            }
            else
            {
                for (int i = 0; i < PhotonNetwork.PlayerListOthers.Length; i++)
                {
                     controller = UIGameController.instance.PlayerUIInScene[i];
                    controller.nickName = PhotonNetwork.PlayerListOthers[i].NickName;
                    UIGameController.instance.PlayerUIInScene[i].gameObject.SetActive(true);
                }
            }

        }
    }
    internal void SetPlayerName()
    {
        if (String.IsNullOrEmpty(playerName))
            playerName = "Player " + playerIndex.ToString();
        PhotonNetwork.NickName = playerName;
        if (playerName != "Computer")
        {
            localPlayer = this;
        }
        else
            localPlayer = GameObject.Find("Local Player").GetComponent<Player>();
    }
    void SetUpPlayerScore()
    {
        for (int i = 0; i < UIGameController.instance.PlayerUIInScene.Length; i++)
        {
            if (UIGameController.instance.PlayerUIInScene[i].gameObject.activeSelf)
            {
                UIGameController.instance.PlayerUIInScene[i].playerScoreInstance.nameTxt.text = UIGameController.instance.PlayerUIInScene[i].nickName;
                UIGameController.instance.PlayerUIInScene[i].playerScoreInstance.gameObject.SetActive(true);
            }
        }
    }
    void ChangeBubbleTxt(string nickName,string newTxt, bool changeActivity,bool active)
    {
        if(String.IsNullOrEmpty(newTxt) == false)
        {
            GetPlayerUI(nickName).bubble.SetActive(true);
            Debug.Log("change bubble text to " + newTxt);
            GetPlayerUI(nickName).bubbleText.text = newTxt;
        }
        if(changeActivity)
        {
            GetPlayerUI(nickName).bubble.SetActive(active);
        }
    }
    void ChangeScoreTxt(string nickName, int score)
    {
        GetPlayerUI(nickName).playerScoreInstance.scoreTxt.text = score.ToString();
    }
    internal PlayerUIController GetPlayerUI(string nickName)
    {
        // this will return the player score that is only on my computer
        PlayerUIController[] temp = UIGameController.instance.PlayerUIInScene;
        for (int i = 0; i < temp.Length; i++)
        {
            if (temp[i].nickName == nickName)
            {
                return temp[i];
            }
        }
        return null;
    }
    internal void ControlAllbubbleTexts(bool active)
    {
        for (int i = 0; i < TurnManager.instance.players.Count; i++)
        {
            ChangeBubbleTxt(TurnManager.instance.players[i].playerName, "", true, active);
        }
    }
    #region IPunObservable implementation
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(playerName);
            stream.SendNext(playerIndex);
            stream.SendNext(numOfFourth);
            stream.SendNext(hand);
            stream.SendNext(mySubCatagories);
            stream.SendNext(lastAskedCatagory);
        }
        else
        {
            // Network player, receive data
            this.playerName = (string)stream.ReceiveNext();
            this.playerIndex = (int)stream.ReceiveNext();
            this.numOfFourth = (int)stream.ReceiveNext();
            this.hand = (List<CardEntity>)stream.ReceiveNext();
            this.mySubCatagories = (Dictionary<string, int>)stream.ReceiveNext();
            this.lastAskedCatagory = (string)stream.ReceiveNext();
        }
    }

    internal void SetUpInfo()
    {
        photonView.RPC("StartCo", RpcTarget.All);
    }
    [PunRPC]
    void StartCo()
    {
        StartCoroutine(SetUpPlayer());
    }
    IEnumerator SetUpPlayer()
    {
        while (SceneManager.GetActiveScene().name != "AIBoard")
        {
            yield return new WaitForEndOfFrame();
        }
        SetPlayerName();
        yield return new WaitForSeconds(2f);
        SetPlayerUI();
        yield return new WaitForSeconds(2f);
        SetUpPlayerScore();
        if (localPlayer == this && PhotonNetwork.OfflineMode || PhotonNetwork.OfflineMode == false && PhotonNetwork.IsMasterClient)       
            StartCoroutine("StartGame");
    }
    IEnumerator StartGame()
    {
        while (CardController.instance == null)
        {
            yield return new WaitForEndOfFrame();
        }
        Debug.LogError(playerName);
        Debug.LogError(localPlayer == this);

        CardController.instance.HandStartingCards();
    }
    #endregion
    // split each rpc to what to do when it is master (local player on it's owm computer) and when it's a network player on my computer (fake)
    #region CATEGORY
    // game starts with this method
    [PunRPC]
    internal void PlayerChoosePlayer()
    {
        UIGameController.instance.StartTimer("player");
        Debug.Log("PlayerChoosePlayer()");
        UIGameController.instance.instructionText.text = "Choose a player by clicking on it's icon";
        for (int i = 0; i < TurnManager.instance.players.Count; i++)
        {
            var currentIndex = i;
            if (TurnManager.instance.players[i] != this)
                GetPlayerUI(TurnManager.instance.players[currentIndex].playerName).iconButton.onClick.AddListener(delegate { this.PlayerChoosePlayerResult(TurnManager.instance.players[currentIndex]); });
        }
    }
    internal void PlayerChoosePlayerResult(Player playerToAsk)
    {
        UIGameController.instance.StopAllLocalTimers();
        Debug.Log("PlayerChoosePlayerResult() called on player " + this.playerName);
        for (int i = 0; i < TurnManager.instance.UIplayers.Count; i++)
        {
            if (TurnManager.instance.players[i] != this)
                TurnManager.instance.UIplayers[i].iconButton.onClick.RemoveAllListeners();
        }
        ChangeBubbleTxt(playerName,"I choose to ask "+playerToAsk.playerName, false, false);
        LocalChooseSubCatFromCards(playerToAsk);
    }
    internal void LocalChooseSubCatFromCards(Player playerToAsk)
    {
        Debug.Log("ChooseSubCatagoryFromCards()");
        UIGameController.instance.instructionText.text = "Ask which Card Catagory to request by clicking on the catagory in your cards";
        UIGameController.instance.StartTimer("choose cat");
        ControlAllbubbleTexts(false);
        // do this only localy
        // adding buttons on click
        for (int i = 0; i < hand.Count; i++)
        {
            var currentIndex = i;
            hand[i].gameObject.GetComponent<Button>().onClick.RemoveAllListeners(); ;
            hand[i].gameObject.GetComponent<Button>().onClick.AddListener(delegate { hand[currentIndex].GetCatagoryFromClick(playerToAsk); });
        }
    }
    internal void AIChooseSubCatagoryFromCards()
    {
        // randomly choose a catagory from my hand to ask the player
        int rnd = UnityEngine.Random.Range(0, hand.Count);
        PhotonMessageInfo nullPhotonMessage = new PhotonMessageInfo(); 
        TurnManager.instance.players[0].AskPlayerIfHasCatagory( hand[rnd].subCategoryName,this.lastAskedCatagory,this.playerName, nullPhotonMessage);
        // done
    }
    internal void ChooseSubCatagoryFromCardsResult(string subCategoryName, Player playerToAsk)
    {
        // turning off the halg time pop up
        UIGameController.instance.StopAllLocalTimers();
        Debug.Log("ChooseSubCatagoryFromCardsResult()");
        // when a card is clicked remove all listeners from other cards and choose the catagory
        for (int i = 0; i < hand.Count; i++)
        {
            hand[i].GetComponent<Button>().onClick.RemoveAllListeners();
        }
        // the chosen catagory
        Debug.Log("The player chose catagory: " + subCategoryName);
        // ask AI if it has catagory
        ChangeBubbleTxt(playerName, "Do you have " + subCategoryName,true,true);
        if (PhotonNetwork.OfflineMode)
            TurnManager.instance.players[1].AskAIIfHasCatagory(subCategoryName);
        else
            photonView.RPC("AskPlayerIfHasCatagory", playerToAsk.GetComponent<PhotonView>().Owner, subCategoryName, this.lastAskedCatagory, this.playerName);
    }

    [PunRPC]
    public void AskPlayerIfHasCatagory(string subCategoryName, string lastAskedCat, string askedName, PhotonMessageInfo info)
    {
        // this method is called from result of choosing which catagory to ask another player by clicking on cards
        // this is called on the player who is asked by info
        Debug.Log("AskPlayerIfHasCatagory()");
        UIGameController.instance.instructionText.text = "Choose your answer by clicking yes or no";
        if(lastAskedCat != subCategoryName)
            ChangeBubbleTxt(askedName,"Do you have " + subCategoryName+" ?",true,true);
        else
            ChangeBubbleTxt(askedName, "Do you have any more " + subCategoryName + " ?", true, true);
        UIGameController.instance.StartTimer("answer cat");
        Debug.Log("waiting for answer if player " + this.playerIndex + " has catagory");
        // setting the right methods for the buttons to show on the player who got asked
        if(PhotonNetwork.OfflineMode)
        {
            UIGameController.instance.yesCatButton.onClick.AddListener(delegate { PlayerAnswerCatagory( "yes", subCategoryName, nullPhotonMessage); });
            UIGameController.instance.noCatButton.onClick.AddListener(delegate { PlayerAnswerCatagory("no", subCategoryName,nullPhotonMessage); });
        }
        else
        {
            Debug.Log(info.Sender.NickName);
            UIGameController.instance.yesCatButton.onClick.AddListener(delegate { PlayerAnswerCatagory( "yes", subCategoryName,info); });
            UIGameController.instance.noCatButton.onClick.AddListener(delegate { PlayerAnswerCatagory("no", subCategoryName,info); });
        }
    }
    internal void AskAIIfHasCatagory(string subCategoryName)
    {
        // do this localy= no need for network
        bool has = CheckIfPlayerHasCatagory(this, subCategoryName);
        Debug.Log("AskAIIfHasCatagory() answer for: " + subCategoryName);
        AIAnswerCatagory(has, subCategoryName);
        // done
    }
    internal bool CheckIfPlayerHasCatagory(Player player, string catName)
    {
        Debug.Log("CheckIfPlayerHasCatagory()");
        // check the UI hand to check if there is the catagory
        if (player.mySubCatagories.ContainsKey(catName))
            return true;
        return false;
        // done
    }
    [PunRPC]
    public void PlayerAnswerCatagory(string answer, string subCategoryName , PhotonMessageInfo info)
    {
        // this is called on the player who is asked by info after they clicked yes or no
        if (UIGameController.timerRunning)
        {
            bool has = CheckIfPlayerHasCatagory(this,subCategoryName);
            Debug.Log("PlayerAnswerCatagory()");
            // this method will happen on the player who got asked
            UIGameController.instance.StopAllLocalTimers();
            if (answer == "yes")
            {
                if (has)
                {
                    //playerWhoAsked.lastAskedCatagory = subCategoryName;
                    ChangeBubbleTxt(playerName, "I do", true, true);
                    if(PhotonNetwork.OfflineMode)
                        TurnManager.instance.players[1].AIChooseCardFromCatagory(subCategoryName);
                    else
                        photonView.RPC("PlayerChooseCardFromCatagory", info.Sender, subCategoryName);
                }
                else
                    UIGameController.instance.instructionText.text = "Try again by choosing yes or no";
            }
            else
            {
                if (!has)
                {
                    // change add rcp
                    // change the player who asked to go fish and not AI
                    ChangeBubbleTxt(playerName, "I don't", true, true);
                    if (PhotonNetwork.OfflineMode)
                        TurnManager.instance.players[1].AIGoFish();
                    else
                        photonView.RPC("PlayerGoFish", info.Sender);
                    Debug.Log("Removing listener to yes or no");
                    UIGameController.instance.yesCatButton.onClick.RemoveAllListeners();
                    UIGameController.instance.noCatButton.onClick.RemoveAllListeners();
                }
                else
                    UIGameController.instance.instructionText.text = "Try again by choosing yes or no";
            }
        }
    }
    void AIAnswerCatagory(bool has, string subCategoryName)
    {
        // this method is called on AI
        Debug.Log("AIAnswerCatagory()");
        // AI has this catagory 
        ChangeBubbleTxt(localPlayer.playerName, "", true, false);
        if (has)
        {
            TurnManager.instance.players[0].lastAskedCatagory = subCategoryName;
            TurnManager.instance.players[0].PlayerChooseCardFromCatagory(subCategoryName, nullPhotonMessage);
            ChangeBubbleTxt(playerName, "I do! Which card do you want?", false, false);
        }
        else
        {
            ChangeBubbleTxt(playerName, "I don't have it", false, true);
            TurnManager.instance.players[0].PlayerGoFish();
        }
        // done
    }

    #endregion
    #region CARD
    List<CardEntity> WhichCardsAreMissingInCatagory(Player player, string subCategoryName)
    {
        List<CardEntity> notFount = new List<CardEntity>();
        // look through that catagory, check whice cards you need to get a 
        SubCategoryEntity catagory = CardController.instance.allSubCatogories[subCategoryName];
        for (int i = 0; i < catagory.cards.Count; i++)
        {
            if (!player.hand.Contains(catagory.cards[i]))
            {
                // if you dont have that card, add it to the new list
                notFount.Add(catagory.cards[i]);
            }
        }
        Debug.Log("Player " + player.playerIndex + "doesnt have num of cards in that catagory " +catagory.subCategoryName );
        return notFount;
        // done
    }
    [PunRPC]
    void PlayerChooseCardFromCatagory(string subCategoryName, PhotonMessageInfo info)
    {
        // called on all players but AI
        // this metod will be called on the player who asked the catagry
        Debug.Log("PlayerChooseCardFromCatagory()");
        // check which cards in the catagory the player does not have
        UIGameController.instance.instructionText.text = "Choose which card you want by clicking on the button with it's name";
        ChangeBubbleTxt(playerName, "I do! Which card do you want?", true, true);
        UIGameController.instance.StartTimer("choose card");
        List<CardEntity> notFount = WhichCardsAreMissingInCatagory(this, subCategoryName);
        //UIGameController.instance.playerCards.SetActive(false);
        // making only the catagory UI I chose to appear
        foreach (KeyValuePair<string, GameObject> entry in CardController.instance.catagoryUI)
        {
            if(entry.Key !=subCategoryName)
            entry.Value.SetActive(false);
        }
        for (int i = 0; i < notFount.Count; i++)
        {
            UIGameController.instance.options[i].gameObject.SetActive(true);
            UIGameController.instance.options[i].gameObject.name = notFount[i].cardName;
            UIGameController.instance.options[i].GetComponentInChildren<Text>().text = notFount[i].cardName;
            // change to UIGameController.instance.options[i].onClick.AddListener(delegate { playerWhoAsked.PlayerAnswerCard(notFount[i]); });
            CardEntity card = notFount[i];
            UIGameController.instance.options[i].onClick.RemoveAllListeners();
            Debug.LogError("Coroutine ai anwer card: "+card.cardName);
            if(PhotonNetwork.OfflineMode)
                UIGameController.instance.options[i].onClick.AddListener(delegate {StartCoroutine( TurnManager.instance.players[1].AIAnswerCard(card)); });
            else
                UIGameController.instance.options[i].onClick.AddListener(delegate {photonView.RPC("AskPlayerIfHasCard", info.Sender, playerName, card.cardName); });
        }
    }
    void AIChooseCardFromCatagory(string subCategoryName)
    {
        // called on AI
        // this metod will be called after the AI was asked and answered about the catagory
        Debug.Log("AIChooseCardFromCatagory()");
        List<CardEntity> notFount = WhichCardsAreMissingInCatagory(this, subCategoryName);
        int rnd = UnityEngine.Random.Range(0, notFount.Count);
        TurnManager.instance.players[0].AskPlayerIfHasCard(this.playerName, notFount[rnd].cardName, nullPhotonMessage);
        // done
    }

    [PunRPC]
    void AskPlayerIfHasCard(string askingPlayerName, string cardName, PhotonMessageInfo info)
    {
        UIGameController.instance.StopAllLocalTimers();
        UIGameController.instance.StartTimer("answer card");
        ChangeBubbleTxt(askingPlayerName, "Do you have the card: " + cardName + " ?", true, true);
        Debug.Log("waiting for answer if player " + this.playerIndex + " has card"+cardName);
        // setting the right methods for the buttons to show on the player who got asked
        Debug.Log("Adding listener to yes or no");
        UIGameController.instance.yesCatButton.onClick.RemoveAllListeners();
        UIGameController.instance.yesCatButton.onClick.RemoveAllListeners();
        ChangeBubbleTxt(playerName, "" + cardName + " ?", true, false);
        if(PhotonNetwork.OfflineMode)
        {
            UIGameController.instance.yesCatButton.onClick.AddListener(delegate { PlayerAnswerCard( true, cardName, nullPhotonMessage); });
            UIGameController.instance.noCatButton.onClick.AddListener(delegate { PlayerAnswerCard( false, cardName, nullPhotonMessage); });
        }
        else
        {
            UIGameController.instance.yesCatButton.onClick.AddListener(delegate { PlayerAnswerCard(true, cardName, info); });
            UIGameController.instance.noCatButton.onClick.AddListener(delegate { PlayerAnswerCard(false, cardName, info); });
        }
    }

    [PunRPC]
    void PlayerAnswerCard(bool answer, string cardName, PhotonMessageInfo info)
    {
        if (UIGameController.timerRunning)
        {
            // turning off the halg time pop up
            Debug.Log("PlayerAnswerCard" + answer);
            UIGameController.instance.StopAllLocalTimers();
            bool has = CheckIfPlayerHasCard(this, cardName);
            if (answer)
            {
                if (has)
                {
                    // give the card to the AI
                    // remove yes or no listeners here
                    ChangeBubbleTxt(playerName, "I do!", true, true);
                    UIGameController.instance.yesCatButton.onClick.RemoveAllListeners();
                    UIGameController.instance.noCatButton.onClick.RemoveAllListeners();
                    CardEntity card = GetCardFromCardName(this, cardName);
                    if (PhotonNetwork.OfflineMode)
                        PlayerGiveCardToPlayer(this, TurnManager.instance.players[1], card);
                    else
                        PlayerGiveCardToPlayer(this, info.photonView.gameObject.GetComponent<Player>(), card);
                }
                else
                    UIGameController.instance.instructionText.text = "Try again by choosing yes or no";
            }
            else
            {
                if (!has)
                {
                    // change to this playerWhoAsked.PlayerGoFish();
                    // remove yes or no listeners here
                    ChangeBubbleTxt(playerName, "I don't!", true, true);
                    if (PhotonNetwork.OfflineMode)
                        TurnManager.instance.players[1].AIGoFish();
                    else
                        photonView.RPC("PlayerGoFish", info.Sender);
                    Debug.Log("Removing listener to yes or no");
                    UIGameController.instance.yesCatButton.onClick.RemoveAllListeners();
                    UIGameController.instance.noCatButton.onClick.RemoveAllListeners();
                }
                else
                    UIGameController.instance.instructionText.text = "Try again by choosing yes or no";
            }
        }
    }
    IEnumerator AIAnswerCard(CardEntity card)
    {
        Debug.Log("AI answer card: "+card.cardName);
        if (UIGameController.timerRunning)
        {
            UIGameController.instance.StopAllLocalTimers();
            UIGameController.instance.playerCards.SetActive(true);
            foreach (KeyValuePair<string, GameObject> entry in CardController.instance.catagoryUI)
            {
                entry.Value.SetActive(true);
            }
            TurnManager.instance.players[0].CancelOptions();
            yield return new WaitForSeconds(1);
            if (CheckIfPlayerHasCard(this, card.cardName))
            {
                // give the card to the player
                AIGiveCardToPlayer(card);
                ChangeBubbleTxt(playerName, "", true, false);
            }
            else
            {
                ChangeBubbleTxt(playerName, "I don't have it", false, false);
                TurnManager.instance.players[0].PlayerGoFish();
            }
        }
    }
    [PunRPC]
    void CancelOptions()
    {
        for (int i = 0; i < 4; i++)
        {
            UIGameController.instance.options[i].gameObject.name = i.ToString();
            UIGameController.instance.options[i].gameObject.SetActive(false);
        }
    }
    bool CheckIfPlayerHasCard(Player player, string cardName)
    {
        for (int i = 0; i < player.hand.Count; i++)
        {
            if (player.hand[i].cardName == cardName)
                return true;
        }
        return false;
    }

    CardEntity GetCardFromCardName(Player player,string cardName)
    {
        // call this only if you definatly have the card
        for (int i = 0; i < player.hand.Count; i++)
        {
            if (player.hand[i].cardName == cardName)
                return player.hand[i];
        }
        return null;
    }
    [PunRPC]
    void PlayerGiveCardToPlayer(Player whoIsGiving, Player whoToGive, CardEntity card)
    {
        ChangeBubbleTxt(whoIsGiving.playerName, "" + card.cardName + " ?", true, false);
        ChangeBubbleTxt(whoToGive.playerName, "" + card.cardName + " ?", true, false);
        Debug.Log("Player is giving card to player index "+whoIsGiving.playerIndex+". The card name is: " + card.name);
        CardController.instance.GiveCardToPlayer(TurnManager.instance.players[1],this, card);
        TurnManager.instance.GiveTurnToCurrentPlayerInList();
    }
    void AIGiveCardToPlayer(CardEntity card)
    {
        // called on AI
        // this method will be called if a player asked me for a card and I have it
        // the turn stays to this player 
        Debug.Log("AI is giving card to player. The card name is: " + card.name);
        CardController.instance.GiveCardToPlayer(TurnManager.instance.players[0], this, card);
        //TurnManager.instance.players[0].PlayerChooseSubCatagoryFromCards();
    }
    #endregion

    #region HAND
    public void PrintMyCatagories()
    {
        foreach (KeyValuePair<string, int> pair in mySubCatagories)
        {
            Debug.Log(pair.Key + "," + pair.Value);
        }
    }
    public void PrintMyCards()
    {
        foreach (CardEntity card in hand)
        {
            Debug.Log(card.cardName);
        }
    }
    #endregion

    #region GENERAL METHODS
    [PunRPC]
    void PlayerGoFish()
    {
        // this is called on local player only
        Debug.Log("PlayerGoFish()");
        if (CardController.instance.deckOver == false)
        {
            // this method is to take a pile from the pile
            // this metod will be called on the player who asked if the player who got asked said no
            // add a go fish text
            UIGameController.instance.instructionText.text = " Take a card from the pile by clicking on it";
            UIGameController.instance.StartTimer("deck");
            Debug.LogError("Adding listener to topdeck");
            UIGameController.instance.deckTopCard.onClick.AddListener(delegate { StartCoroutine(CardController.instance.PullFromDeck(1,true)); });
            //Debug.LogError("Num of listeners on deck = " + UIGameController.instance.deckTopCard.onClick.GetPersistentEventCount());
            // add a listener for the pile to give the player who clicked it a card
        }
    }
    void AIGoFish()
    {
            // will be called on AI and will make it take a card from the pile
        if (CardController.instance.deckOver == false)
        {
            Debug.Log("AIGoFish()");
            CardController.instance.FakePullFromDeck(this, 1);
        }
        TurnManager.instance.NextTurn();
    }
    internal void AddForth()
    {
        numOfFourth++;
        // change scores via rpc when needed
        ChangeScoreTxt(this.playerName, numOfFourth);
        CheckIfMaxForths();
    }
    void CheckIfMaxForths()
    {
        if (numOfFourth == CardController.instance.maxNumOfForth)
            GameController.instance.GameOver();
    }
    #endregion

}