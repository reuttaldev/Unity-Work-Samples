using Photon.Realtime;
using UnityEngine;


namespace Photon.Pun.UtilityScripts
{
    /// <summary>
    /// Unexpected disconnects recovery
    /// </summary>
    /// 
    public class DisconnectsRecovery : MonoBehaviourPunCallbacks
    {
        #region CONST MESSAGES
        const string neverConneced = "You are offline. Please connect to internet or play locally.";
        const string disconnected = "You disconnected from the internet. Please re-connct or play localy";
        #endregion
        private bool rejoinCalled;

        private bool reconnectCalled;

        private bool inRoom;

        private DisconnectCause previousDisconnectCause;
        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogFormat("OnDisconnected(cause={0}) ClientState={1} PeerState={2}",
                            cause,
                            PhotonNetwork.NetworkingClient.State,
                            PhotonNetwork.NetworkingClient.LoadBalancingPeer.PeerState);

            // display massages on screen
            if (DisconnectCause.ExceptionOnConnect == cause)
            {
                UILobbyController.instance.DisplayConnectionError(neverConneced);
                UILobbyController.instance.tryAgainBtn.gameObject.SetActive(true);

            }

            if (this.rejoinCalled)
            {
                Debug.LogErrorFormat("Rejoin failed, client disconnected, causes; prev.:{0} current:{1}", this.previousDisconnectCause, cause);
                this.rejoinCalled = false;
            }
            else if (this.reconnectCalled)
            {
                Debug.LogErrorFormat("Reconnect failed, client disconnected, causes; prev.:{0} current:{1}", this.previousDisconnectCause, cause);
                this.reconnectCalled = false;
            }
            this.HandleDisconnect(cause); // add attempts counter? to avoid infinite retries?
            this.inRoom = false;
            this.previousDisconnectCause = cause;
        }

        private void HandleDisconnect(DisconnectCause cause)
        {
            switch (cause)
            {
                // cases that we can recover from
                case DisconnectCause.ServerTimeout:
                case DisconnectCause.Exception:
                case DisconnectCause.ClientTimeout:
                case DisconnectCause.DisconnectByServerLogic:
                case DisconnectCause.AuthenticationTicketExpired:
                case DisconnectCause.DisconnectByServerReasonUnknown:
                    if (this.inRoom)
                    {
                        Debug.Log("calling PhotonNetwork.ReconnectAndRejoin()");
                        this.rejoinCalled = PhotonNetwork.ReconnectAndRejoin();
                        if (!this.rejoinCalled)
                        {
                            Debug.LogWarning("PhotonNetwork.ReconnectAndRejoin returned false, PhotonNetwork.Reconnect is called instead.");
                            this.reconnectCalled = PhotonNetwork.Reconnect();
                        }
                    }
                    else
                    {
                        Debug.Log("calling PhotonNetwork.Reconnect()");
                        UILobbyController.instance.DisplayConnectionError(disconnected);
                        this.reconnectCalled = PhotonNetwork.Reconnect();
                    }
                    if (!this.rejoinCalled && !this.reconnectCalled)
                    {
                        Debug.LogError("PhotonNetwork.ReconnectAndRejoin() or PhotonNetwork.Reconnect() returned false, client stays disconnected.");
                    }
                    break;
                case DisconnectCause.None:
                case DisconnectCause.OperationNotAllowedInCurrentState:
                case DisconnectCause.CustomAuthenticationFailed:
                case DisconnectCause.DisconnectByClientLogic:
                case DisconnectCause.InvalidAuthentication:
                case DisconnectCause.ExceptionOnConnect:
                case DisconnectCause.MaxCcuReached:
                case DisconnectCause.InvalidRegion:
                    Debug.LogErrorFormat("Disconnection we cannot automatically recover from, cause: {0}, report it if you think auto recovery is still possible", cause);
                    break;
            }
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            if (this.rejoinCalled)
            {
                Debug.LogErrorFormat("Quick rejoin failed with error code: {0} & error message: {1}", returnCode, message);
                this.rejoinCalled = false;
            }
        }

        public override void OnJoinedRoom()
        {
            this.inRoom = true;
            if (this.rejoinCalled)
            {
                Debug.Log("Rejoin successful");
                this.rejoinCalled = false;
            }
        }

        public override void OnLeftRoom()
        {
            this.inRoom = false;
        }

        public override void OnConnectedToMaster()
        {
            if (this.reconnectCalled)
            {
                Debug.Log("Reconnect successful");
                this.reconnectCalled = false;
            }
        }
    }
}