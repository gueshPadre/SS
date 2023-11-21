using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using NetworkBehaviour = Mirror.NetworkBehaviour;
using Unity.Netcode;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay.Models;
using Unity.Services.Relay;
using Unity.Collections;
using Unity.Networking.Transport.Relay;
using Rewired.UI.ControlMapper;
using Unity.Networking.Transport;
using NetworkConnection = Unity.Networking.Transport.NetworkConnection;
using Type = Unity.Networking.Transport.NetworkEvent.Type;
using Utp;
using UnityEngine.Assertions;
using System.Net;
using System.Linq;
using System;

public class MatchMaker : MonoBehaviour
{
    public static MatchMaker Inst;


    Allocation hostAlloc;
    JoinAllocation clientAlloc;
    string joinCode;
    NetworkDriver hostDriver;
    NetworkDriver playerDriver;
    NativeList<NetworkConnection> serverConnections;
    NetworkConnection clientConnection;


    async void Awake()
    {
        await Authenticate();
    }

    static async Task Authenticate()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log($"my player ID: {AuthenticationService.Instance.PlayerId}");
    }


    private void Start()
    {
        Inst = this;
    }

    private void Update()
    {
        UpdateHost();
        ClientUpdate();
    }

    void UpdateHost()
    {
        // Skip update logic if the Host isn't yet bound.
        if (!hostDriver.IsCreated || !hostDriver.Bound)
        {
            return;
        }

        // This keeps the binding to the Relay server alive,
        // preventing it from timing out due to inactivity.
        hostDriver.ScheduleUpdate().Complete();

        // Clean up stale connections.
        for (int i = 0; i < serverConnections.Length; i++)
        {
            if (!serverConnections[i].IsCreated)
            {
                Debug.Log("Stale connection removed");
                serverConnections.RemoveAt(i);
                --i;
            }
        }

        // Accept incoming client connections.
        Debug.Log($"In Matchmaking, Am I Listening?: {hostDriver.Listening}");
        NetworkConnection incomingConnection;
        while ((incomingConnection = hostDriver.Accept()) != default(NetworkConnection))
        {
            // Adds the requesting Player to the serverConnections list.
            // This also sends a Connect event back the requesting Player,
            // as a means of acknowledging acceptance.
            Debug.Log("Accepted an incoming connection.");
            serverConnections.Add(incomingConnection);
        }




        // Process events from all connections.
        for (int i = 0; i < serverConnections.Length; i++)
        {
            Debug.Log($"get size: {hostDriver.GetEventQueueSizeForConnection(serverConnections[i])}" +
                $" there are: {serverConnections.Length} and #{i}'s connection state: {hostDriver.GetConnectionState(serverConnections[i])}");
            Assert.IsTrue(serverConnections[i].IsCreated);

            // Resolve event queue.
            Debug.Log($"The server connection ID: {serverConnections[i].InternalId}");
            Type eventType;
            while ((eventType = hostDriver.PopEventForConnection(serverConnections[i], out var stream)) != Type.Empty)
            {
                switch (eventType)
                {
                    // Handle Relay events.
                    case Type.Data:
                        FixedString32Bytes msg = stream.ReadFixedString32();
                        Debug.Log($"Server received msg: {msg}");
                        //hostLatestMessageReceived = msg.ToString();
                        break;

                    // Handle Disconnect events.
                    case Type.Disconnect:
                        Debug.Log("Server received disconnect from client");
                        serverConnections[i] = default(NetworkConnection);
                        break;
                }
            }
        }
    }

    public async Task<string> HostGame()
    {
        //var pRegion = await Relay.Instance.ListRegionsAsync();
        //foreach (var region in pRegion)
        //{
        //    Debug.Log($"Region ID: {region.Id} and Description");
        //}
        hostAlloc = await RelayService.Instance.CreateAllocationAsync(4, "europe-central2");

        serverConnections = new NativeList<NetworkConnection>(4, Allocator.Persistent);
        OnBindHost();

        joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAlloc.AllocationId);

        Mirror.NetworkManager.singleton.StartHost();
        //MyNetworkManager.instance.OnStartHost();
        //Mirror.NetworkManager.singleton.OnClientConnect();

        return joinCode;
    }

    public void OnHostSendMessage()
    {
        if (serverConnections.Length == 0)
        {
            Debug.LogWarning($"No players connected");
            return;
        }

        // Get message from the input field, or default to the placeholder text.
        //var msg = !String.IsNullOrEmpty(HostMessageInput.text) ? HostMessageInput.text : HostMessageInput.placeholder.GetComponent<Text>().text;
        var msg = "Host - Sending to Client";

        // In this sample, we will simply broadcast a message to all connected clients.
        for (int i = 0; i < serverConnections.Length; i++)
        {
            Debug.Log($"HostSendMessage sent: {msg}");
            if (hostDriver.BeginSend(serverConnections[i], out var writer) == 0)
            {
                // Send the message. Aside from FixedString32, many different types can be used.
                writer.WriteFixedString32(msg);
                hostDriver.EndSend(writer);
            }
        }

    }

    public void OnBindHost()
    {
        var relayServerData = new RelayServerData(hostAlloc, "udp");

        var pSettings = new NetworkSettings();
        pSettings.WithRelayParameters(ref relayServerData);

        hostDriver = NetworkDriver.Create(pSettings);

        hostDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

        if (hostDriver.Bind(NetworkEndPoint.AnyIpv4) != 0)
        {
            Debug.LogError($"host client failed to bind");
        }
        else
        {
            if (hostDriver.Listen() != 0)
            {
                Debug.Log($"failed to listen this time");
            }
            else
            {
                Debug.Log($"Bound to relay server");
            }
        }
    }



    public async void JoinGame(string _joinCode)
    {
        clientAlloc = await RelayService.Instance.JoinAllocationAsync(_joinCode);

        Debug.Log($"JOIN CODE SHOULD BE: {_joinCode} and client allocation ID: {clientAlloc.AllocationId}");

        OnBindPlayer();
    }

    void OnBindPlayer()
    {
        var pRelayServerData = new RelayServerData(clientAlloc, "udp");

        var pSettings = new NetworkSettings();
        pSettings.WithRelayParameters(ref pRelayServerData);


        playerDriver = NetworkDriver.Create(pSettings);

        if (playerDriver.Bind(NetworkEndPoint.AnyIpv4) != 0)
        {
            Debug.LogError($"Client failed to bind");
        }
        else
        {
            Debug.Log($"Client bound to relay server");
        }
    }

    public void OnConnect()
    {
        Debug.Log("Player - Connecting to Host's client.");

        Mirror.NetworkManager.singleton.StartClient();
        clientConnection = playerDriver.Connect();

    }

    void ClientUpdate()
    {
        // Skip update logic if the Player isn't yet bound.
        if (!playerDriver.IsCreated || !playerDriver.Bound)
        {
            return;
        }

        // This keeps the binding to the Relay server alive,
        // preventing it from timing out due to inactivity.
        playerDriver.ScheduleUpdate().Complete();

        // Resolve event queue.
        //Type eventType = clientConnection.PopEvent(playerDriver, out var stream);
        Type eventType;
        while ((eventType = clientConnection.PopEvent(playerDriver, out var stream)) != Type.Empty)
        {
            switch (eventType)
            {
                // Handle Relay events.
                case Type.Data:
                    FixedString32Bytes msg = stream.ReadFixedString32();
                    Debug.Log($"Player received msg: {msg}");
                    //playerLatestMessageReceived = msg.ToString();
                    break;

                // Handle Connect events.
                case Type.Connect:
                    Debug.Log("Player connected to the Host");

                    Mirror.NetworkClient.ready = true;
                    Mirror.NetworkClient.ConnectHost();


                    // server scene was loaded. now spawn all the objects
                    NetworkServer.SpawnObjects();

                    // connect client and call OnStartClient AFTER server scene was
                    // loaded and all objects were spawned.
                    // DO NOT do this earlier. it would cause race conditions where a
                    // client will do things before the server is even fully started.
                    //Debug.Log("StartHostClient called");
                    //Mirror.NetworkManager.singleton.SetupClient();
                    if (Mirror.NetworkManager.singleton.runInBackground)
                        Application.runInBackground = true;

                    //if (Mirror.NetworkManager.singleton.authenticator != null)
                    //{
                    //    Debug.Log($"Authenticatinggggg");
                    //    Mirror.NetworkManager.singleton.authenticator.OnStartClient();
                    //    Mirror.NetworkManager.singleton.authenticator.OnClientAuthenticated.AddListener(Mirror.NetworkManager.singleton.OnClientAuthenticated);
                    //}
                    Mirror.NetworkManager.singleton.RegisterClientMessages();
                    

                    //HostMode.InvokeOnConnected();


                    break;

                // Handle Disconnect events.
                case Type.Disconnect:
                    Debug.Log("Player got disconnected from the Host");
                    clientConnection = default(NetworkConnection);
                    break;
            }
        }
    }

    public void OnPlayerSendMessage()
    {
        if (!clientConnection.IsCreated)
        {
            Debug.LogError("Player isn't connected. No Host client to send message to.");
            return;
        }

        // Get message from the input field, or default to the placeholder text.
        var msg = "Client - Sending this to host";
        if (playerDriver.BeginSend(clientConnection, out var writer) == 0)
        {
            Debug.Log($"PlayerSendMessage Sent: {msg}");
            // Send the message. Aside from FixedString32, many different types can be used.
            writer.WriteFixedString32(msg);
            playerDriver.EndSend(writer);
        }
    }

    //private void OnDestroy()
    //{
    //    if (hostDriver.IsCreated)
    //    {
    //        hostDriver.Dispose();
    //        serverConnections.Dispose();
    //    }
    //    if (playerDriver.IsCreated)
    //    {
    //        playerDriver.Dispose();
    //        playerDriver.Disconnect(clientConnection);
    //        clientConnection = default(NetworkConnection);
    //    }
    //}


}


