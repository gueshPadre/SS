using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Utp;

public class MyNetworkManager : RelayNetworkManager
{
    List<PlayerMovement> players = new List<PlayerMovement>();          // List held on server
    Dictionary<NetworkConnectionToClient,GameObject> connPlayerDict = new Dictionary<NetworkConnectionToClient,GameObject>();       // List of players and their connections held on server

    public List<PlayerMovement> Players { get { return players; } }

    public static MyNetworkManager instance;

    public override void Awake()
    {
        base.Awake();

        instance = this;
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);

        players.Add(conn.identity.gameObject.GetComponent<PlayerMovement>());
        connPlayerDict.Add(conn, conn.identity.gameObject);
        Debug.Log($"Player: {playerPrefab.name} and {conn.identity.gameObject.name} and id: {conn.connectionId}");
    }

    public void AddPlayer()
    {
        Debug.Log($"Should be adding a player");
        var o = Instantiate(playerPrefab);

        NetworkServer.Spawn(o);
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);


    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        Debug.Log($"Connecting a client");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();

        Debug.Log($"OnClientConnect?");
        //var pPlayer = Instantiate(playerPrefab);

        //   NetworkServer.AddPlayerForConnection(conn, pPlayer);
    }

}
