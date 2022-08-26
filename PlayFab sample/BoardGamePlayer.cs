using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class BoardGamePlayer : MonoBehaviour
{
    /* This snippet of code from an Andriod board game. The game is meant to be played by two players on the same phone (think "fire girl and water boy")
    This script gets and sets user data using PlayFab and Cloudscript for saving progress purposes. I will also be attaching the cloudscript for you to view*/
    const int MPStartAmount = 2000;
    [SerializeField]
    private CurrencyController currencyController;
    [SerializeField]
    private GameController gameController;
    [SerializeField]
    private UIController uiController;
    [SerializeField]
    private int playerIndex;
    private string characterId; // character id
    private string playFabID; // user id
    private int currentPositionOnArry = 0;
    private int mpAmount = 0; // magic points amount
    private int nextPositionOnArry;
    private int currentPlayerPosInArry;
    private int jail = 0;
    private Dictionary<string, string> data;
    public string CharacterId { get { return this.characterId; } }
    public int MpAmount { get { return this.mpAmount; }set { this.mpAmount = value; } }
    public int PlayerIndex { get { return this.playerIndex; } }
    public int CurrentPositionOnArry { get { return this.currentPositionOnArry; } set { this.currentPositionOnArry = value; } }
    public int CurrentPlayerPosInArry { get { return this.currentPlayerPosInArry; } set { this.currentPlayerPosInArry = value; } }
    public int NextPositionOnArry { get { return this.nextPositionOnArry; } set { this.nextPositionOnArry = value; } }
    public int Jail { get { return this.jail; } set { this.Jail = value; } }
    private void Start()
    {
        // giving the players a constant amount of money to begin the game
        mpAmount = MPStartAmount;
        // adding 2 character to this user in PlayFab, so each opponnent will have its own inventory since they are running on the same instance
        GetUserCharacters();
    }
    #region CHARACTERS CONTROLLERS

    // this metod Checks that there are no more than 2 characters per user
    void GetUserCharacters()
    {
        string input;
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest()
        {
            FunctionName = "GetUsersCharacters", // Arbitrary function name (must exist in your uploaded cloud.js file)
            GeneratePlayStreamEvent = true,
        },
        result =>
        {
            Debug.Log("GetCharacterId CloudScript call excuted");
            input = PlayFabSimpleJson.SerializeObject(result.FunctionResult);
            // making sure we have no more than 2 characters by indedifying the ids in the input
            // if there is 0 or 1 characters, add until there are 2
            int count = Regex.Matches(input, "CharacterId").Count;
            Debug.Log("Cound " + count);
            if (count < 2)
            {
                AddCharacter("Player " + count.ToString());
                Debug.Log("Adding character with name Player "+ count);
                if (playerIndex == 0) // making sure this part excuted only once since there are 2 player entity instances
                {
                    // doing a few different set request since the max dictionary size is 10
                    data = new Dictionary<string, string> { { "firstCharPos", "0" }, { "secondCharPos", "0" }, { "currentPlayerPosInArry", "0" }, { "rounds", "1" }, { "jail", "0" } };
                    SetReadOnlyUserData(data); // setting initial data
                    // setting the initial values to each tile ownwership
                    data.Clear();
                    string key = "";
                    for (int i = 0; i < 10; i++)
                    {
                        key = "TileOwnership"+i.ToString();
                        data.Add(key, "-1");
                    }
                    SetReadOnlyUserData(data); // setting initial data
                    data.Clear();
                    for (int i = 10; i < 20; i++)
                    {
                        key = "TileOwnership" + i.ToString();
                        data.Add(key, "-1");
                    }
                    SetReadOnlyUserData(data); // setting initial data
                    data.Clear();
                    for (int i = 20; i < gameController.tilesArry.Length; i++)
                    {
                        key = "TileOwnership" + i.ToString();
                        data.Add(key, "-1");
                    }
                    SetReadOnlyUserData(data); // setting initial data
                    uiController.fadeScreenAnime.SetTrigger("FadeOut");
                    // drop the cube 
                    gameController.diceControllerScript.DropDice();
                }
            }
            else
            {
                // change to load character id from save file
                // remove all previous mp
                Debug.Log("Loading previous character id"); 
                GetReadOnlyUserData(AndroidLogin.playFabId);
                // adding innitiale soft currency value
            }
        },
        error => { Debug.Log("GetCharacterId CloudScript call failed"); });
    }

    // this method creates a character whitin the user\ player, so we can have a different currency amount to each character in the game 
    void AddCharacter(string charName)
    {
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest()
        {
            FunctionName = "AddCharacterToUser", 
            GeneratePlayStreamEvent = true,
            FunctionParameter = new { characterName = charName }, // The parameter provided to your function
        },
        result =>
        {
            // retriving just the string id from the result string
            //string input = (JsonWrapper.SerializeObject(result.FunctionResult));
            string input = (PlayFabSimpleJson.SerializeObject(result.FunctionResult));
            int found = input.IndexOf(":");
            string output = input.Substring(found + 2);
            char[] charsToTrim = { '"', '}' };
            output = output.Trim(charsToTrim);
            Debug.Log("AddCharacter CloudScript call excuted, new character id is " + output);
            this.characterId = output;
            // save all of the users character id on Playfab so we could access their inventory easily later
            data = new Dictionary<string, string>();
            if (this.playerIndex == 0) // meaning we are player 1
                data = new Dictionary<string, string>() { { "firstCharacterId", this.characterId } };
            else //meaning we are player 2
                data = new Dictionary<string, string>() { { "secondCharacterId", this.characterId } };
            SetReadOnlyUserData(data); // setting initial data;
        },
        error => {
            Debug.Log("AddCharacter CloudScript call failed");
            Debug.Log(error.GenerateErrorReport());
        });
    }
    #endregion 

    #region DATA CONTROLLERS   
    internal void SetReadOnlyUserData(Dictionary<string, string> data)
    {
        PlayFabServerAPI.UpdateUserReadOnlyData(new PlayFab.ServerModels.UpdateUserDataRequest()
        {
            PlayFabId = AndroidLogin.playFabId,
            Data = data,
        },

    result => {
        Debug.Log("Set read-only user data successful");
    },
    error => {
        Debug.Log("Got error setting read-only user data:");
        Debug.Log(error.GenerateErrorReport());
    });
    }
    internal void UpdateReadOnlyUserData(Dictionary<string, string> data)
    {
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName = "UpdateReadOnlyData",
            FunctionParameter = new { newData = data }, // The parameter provided to your function
            GeneratePlayStreamEvent = true
        },
        result =>
        {
            Debug.Log("CloudScript call successful, result: "+PlayFabSimpleJson.SerializeObject(result.FunctionResult));
        },
        error =>
        {
            Debug.Log("CloudScript call failed");
            Debug.Log(error.GenerateErrorReport());
        }) ;
    }
    void GetReadOnlyUserData(string myPlayFabeId)
    {
        PlayFabServerAPI.GetUserReadOnlyData(new PlayFab.ServerModels.GetUserDataRequest()
        {         
            PlayFabId = myPlayFabeId,
        },
        result =>
        {
            this.playFabID = AndroidLogin.playFabId;
            if (result.Data != null)
            {
                if (playerIndex == 0 && result.Data.ContainsKey("firstCharacterId"))
                    this.characterId = result.Data["firstCharacterId"].Value;
                if (playerIndex == 1 && result.Data.ContainsKey("secondCharacterId"))
                    this.characterId = result.Data["secondCharacterId"].Value;
                // positions of the characters
                if (playerIndex == 0 && result.Data.ContainsKey("firstCharPos"))
                    this.currentPositionOnArry = Int32.Parse(result.Data["firstCharPos"].Value);
                if (playerIndex == 1 && result.Data.ContainsKey("secondCharPos"))
                    this.currentPositionOnArry = Int32.Parse(result.Data["secondCharPos"].Value);
                if (result.Data.ContainsKey("currentPlayerPosInArry"))
                    this.currentPlayerPosInArry = Int32.Parse(result.Data["currentPlayerPosInArry"].Value);
                if (result.Data.ContainsKey("rounds"))
                    uiController.ChangeRoundCountText(Int32.Parse(result.Data["rounds"].Value));
                if (result.Data.ContainsKey("jail") && this.playerIndex == 0)
                {
                    this.jail = Int32.Parse(result.Data["jail"].Value);
                }
                // getting each tile ownwership
                string key = "";
                for (int i = 0; i < gameController.tilesArry.Length; i++)
                {
                    key = "TileOwnership" + i.ToString();
                    gameController.tilesArry[i].OwnedBy = Int32.Parse(result.Data[key].Value);
                }
                // positioning the player at the same place as he was before 
                // setting the MP amount from before
                currencyController.GetSoftCurrency(this.characterId, this.playerIndex);
                if (playerIndex == 0) // to call the method only once
                    gameController.GameReopened(Int32.Parse(result.Data["firstCharPos"].Value) , Int32.Parse(result.Data["secondCharPos"].Value));
            }
        }, (error) =>
        {
            Debug.Log("Got error retrieving user data:");
            Debug.Log(error.GenerateErrorReport());
        });
    }

    internal void ResetCharacterValues()
    {
        UpdateReadOnlyUserData(new Dictionary<string, string>  { { "firstCharPos", "0" }, { "secondCharPos", "0" } , { "currentPlayerPosInArry" ,"0"}, { "rounds","0" } });
        // reset local variables 
        this.currentPlayerPosInArry = 0;
        this.currentPlayerPosInArry = 0;
        this.mpAmount = MPStartAmount;
        // resetting soft amount to 2000
        PlayerSubtractSoftCurrency("10000");
        PlayerAddSoftCurrency(MPStartAmount.ToString());
        // reset display 
        uiController.ChangeNowPlayingText("Player 1");
        uiController.ChangeRoundCountText(1);
        uiController.UpdateAmountDisplay(this.playerIndex, MPStartAmount);
    }
    #endregion
    #region CURRENCY CONTROLLERS
    internal void PlayerAddSoftCurrency(string amount)
    {
        int amountInt = Int32.Parse(amount);
        this.mpAmount += amountInt;
        currencyController.AddSoftCurrency(this.characterId, amount);
        uiController.UpdateAmountDisplay(playerIndex, this.mpAmount);
    }
    internal void PlayerSubtractSoftCurrency(string amount)
    {
        int amountInt = Int32.Parse(amount);
        if (this.mpAmount - amountInt >= 0)
        {
            this.mpAmount -= amountInt;
            currencyController.SubtractSoftCurrency(this.characterId, amount);
            uiController.UpdateAmountDisplay(playerIndex, this.mpAmount);
        }
        else
            gameController.GameIsOver();
    }
    #endregion
}
