using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Mirror;

public class GameManager : NetworkBehaviour
{
    public static GameManager Inst;
    public Action OnUpdatePosition;

    float updateTimer = 3f;         // Time interval in which it should update players' positions

    // Start is called before the first frame update
    void Start()
    {
        #region Singleton
        if (Inst == null)
            Inst = this;
        else
            Destroy(this.gameObject);
        #endregion

    }

    private void OnEnable()
    {
        Debug.Log($"GameManager just became active");
    }

    void Update()
    {
        UpdatePlayersPositions();

        if (Input.GetKeyDown(KeyCode.R) && isServer)
        {
            ResetPlayers();
        }
    }

    void UpdatePlayersPositions()
    {
        if (updateTimer > 0)
        {
            updateTimer -= Time.deltaTime;

            if (updateTimer <= 0)
            {
                OnUpdatePosition?.Invoke();

                updateTimer = 3f;       // reset
            }
        }
    }

    [Server]
    void ResetPlayers()
    {
        foreach (var player in MyNetworkManager.instance.Players)
        {
            player.GetComponent<Rigidbody>().isKinematic = true;
            player.GetComponent<Rigidbody>().velocity = Vector3.zero;
            player.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, player.transform.rotation.z));

            player.GetComponent<Rigidbody>().isKinematic = false;
            ResetRpc(player.gameObject, player.transform.rotation.z);
        }
    }

    [ClientRpc]
    void ResetRpc(GameObject _player, float _z)
    {
        _player.GetComponent<Rigidbody>().isKinematic = true;
        _player.GetComponent<Rigidbody>().velocity = Vector3.zero;
        _player.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, _z));
        _player.GetComponent<Rigidbody>().isKinematic = false;
    }


}
