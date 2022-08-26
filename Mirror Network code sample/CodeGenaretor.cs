using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Text.RegularExpressions;
using System;

public class CodeGenaretor : MonoBehaviourPunCallbacks, ILobbyCallbacks
{
    private static Dictionary<string, RoomInfo> RoomDic;
    void Start()
    {
        RoomDic = new Dictionary<string, RoomInfo>();
    }
    public static string GenareteCode()
    {
        string code = "";
        int num;
        for (int i = 0; i < 6; i++)
        {
            num = UnityEngine.Random.Range(0, 10);
            code += num.ToString();
            if (i == 5)
            // meaning we are about to exit the loop
            {
                // check that this code does not already exist in the list
                if (RoomDic.ContainsKey(code))
                {
                    // if it does, start over until we have a code that doesn't already exist
                    code = "";
                    i = -1;
                }
            }
        }
        return code;

    }
    public static bool CheckIfCodeExists(string code)
    {
        return RoomDic.ContainsKey(code);
        Regex.Replace(code, @"\s+", "");
        if (String.IsNullOrEmpty(code))
        {
            UILobbyController.instance.unvalidCodeMsg.gameObject.SetActive(true);
            return false;
        }
        else
        {
            return true;
        }
    }

    public void PrintAllRoomList()
    {
        foreach (KeyValuePair<string, RoomInfo> room in RoomDic)
        {
            Debug.Log("Room name: " + room.Key);
            Debug.Log("Number of players in room: " + room.Value.PlayerCount);
            Debug.Log("is open: " + room.Value.IsOpen);
        }
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            // Remove room from cached room list if it got closed, became invisible or was marked as removed
            if (!info.IsOpen || !info.IsVisible || info.RemovedFromList || info.PlayerCount > info.MaxPlayers)
            {
                if (RoomDic.ContainsKey(info.Name))
                {
                    RoomDic.Remove(info.Name);
                }

                continue;
            }

            // Update cached room info
            if (RoomDic.ContainsKey(info.Name))
            {
                RoomDic[info.Name] = info;
            }
            // Add new room info to cache
            else
            {
                RoomDic.Add(info.Name, info);
            }
        }
        Debug.Log(RoomDic.Count + " Active Rooms");
        if (RoomDic.Count > 0)
        {
            Debug.Log("printing new room dic: ");
            PrintAllRoomList();
        }
    }
    public override void OnJoinedLobby()
    {
        // whenever this joins a new lobby, clear any previous room lists
        RoomDic.Clear();
    }

    // note: when a client joins / creates a room, OnLeftLobby does not get called, even if the client was in a lobby before
    public override void OnLeftLobby()
    {
        RoomDic.Clear();
    }
    public override void OnJoinedRoom()
    {
        // joining (or entering) a room invalidates any cached lobby room list (even if LeaveLobby was not called due to just joining a room)
        RoomDic.Clear();
    }
}

