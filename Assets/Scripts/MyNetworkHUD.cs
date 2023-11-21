using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MyNetworkHUD : MonoBehaviour
{
    [SerializeField] GameObject startCanvas;
    [SerializeField] public InputField inputField;

    MyNetworkManager myNetMan;


    private void Start()
    {
        myNetMan = GetComponent<MyNetworkManager>();
    }

    public async void ClickHost()
    {
        //startCanvas.SetActive(false);
        //myNetMan.StartHost();

        var pJoinCode = await MatchMaker.Inst.HostGame();
        startCanvas.transform.GetChild(0).GetComponentInChildren<Text>().text = pJoinCode;
    }

    public void ClickJoin()
    {
        //startCanvas.SetActive(false);
        MatchMaker.Inst.JoinGame(inputField.text);

    }

}
