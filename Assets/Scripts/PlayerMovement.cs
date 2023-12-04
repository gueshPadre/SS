using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rewired;
using System.Threading.Tasks;
using Cinemachine;
using Mirror;
using System.Runtime.CompilerServices;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Tooltip("The unique speed of that particular vehicle ")]
    [SerializeField] float speedMultiplier = 1f;
    [SerializeField] float rotSpeed;
    [SerializeField] float pTiltSpeed;
    [SerializeField] float maxTiltAngle = 15f;
    [SerializeField, Tooltip("The time it takes to reach full speed")] float topSpeedTimer = 3f;
    [SerializeField] float topVertSpeed;

    [SerializeField] float slowDownMultiplier = 5f;
    [SerializeField] Transform parent;       // The parent transform of the spaceship. Keeps all alignment proper
    [SerializeField] GameObject camSetup;   // The camera setup to instantiate


    [Header("DEBUG")]
    [SerializeField] bool isInactive;


    private Player player;
    Rigidbody myRb;
    Camera mainCam;
    Transform parentInstance;
    GameManager gM;

    float floatingY = 4f;        // The y at which the spaceship should always float at

    float initialVertSpeed;
    float myVertSpeed;

    bool isRotated;
    private bool maxTilted;
    float currentSpeedTimer;
    CinemachineVirtualCamera myVirtCam;

    public float Speed { get { return myVertSpeed; } }

    // Start is called before the first frame update
    void Start()
    {
        myRb = GetComponent<Rigidbody>();
        gM = GameManager.Inst;

        if (!isInactive)
        {
            player = ReInput.players.GetPlayer(0);
            mainCam = Camera.main;
        }

        initialVertSpeed = myVertSpeed;
        currentSpeedTimer = topSpeedTimer;
        CreateParent();

        //GameManager.Inst.OnUpdatePosition += UpdateMyPos;
    }

    public override void OnStartClient()
    {
        Debug.Log($"OnStartClient on PlayerMovement");
        SetMyCam();
    }

    void CreateParent()
    {
        parentInstance = Instantiate(parent);
        transform.parent = parentInstance;
    }

    void SetMyCam()
    {
        if (!isOwned) { return; }
        var myCam = Instantiate(camSetup);
        myVirtCam = myCam.GetComponentInChildren<CinemachineVirtualCamera>();


        myVirtCam.Follow = this.transform;
        myVirtCam.LookAt = this.transform;
    }

    void UpdateMyPos()
    {
        if(isOwned)
        {
            CmdUpdatePos(this.gameObject, transform.position);
        }
    }

    [Command]
    void CmdUpdatePos(GameObject _player, Vector3 _pos)
    {
        _player.transform.position = _pos;

        RpcUpdatePos(this.gameObject, _pos);
    }

    [ClientRpc]
    void RpcUpdatePos(GameObject _player, Vector3 _pos)
    {
        _player.transform.position = _pos;
    }


    // Update is called once per frame
    void Update()
    {
        Float();
        if (!this.isOwned) { return; }
        if (isInactive) { return; }
        ReadInputs();
    }

    void Float()
    {
        var pDown = -parent.up;

        Ray pRay = new Ray(transform.position, pDown);


        if (Physics.Raycast(pRay, 20f, LayerMask.GetMask("Ground")))
        {
            var pMyPos = transform.position;
            pMyPos.y = floatingY;

            transform.position = pMyPos;
        }

        Debug.DrawRay(transform.position, pDown * 20f, Color.red, 3f);
    }


    private void FixedUpdate()
    {
        if (isInactive || !isOwned) { return; }
        // Calculate the local forward vector of the spaceship
        Vector3 localForward = transform.forward;

        // Convert the local forward vector to world space
        Debug.DrawRay(transform.position, localForward * 5f, Color.blue, 5f);

        // Set the velocity to move forward along the worldForward vector
        if (myVertSpeed == 0)
        {
            myRb.velocity = Vector3.zero;
        }
        else
        {
            myRb.velocity = new Vector3(localForward.x * myVertSpeed * speedMultiplier, myRb.velocity.y, localForward.z * myVertSpeed * speedMultiplier);
        }
        CmdUpdateVelocity(this.gameObject, myRb.velocity);
    }


    [Command]
    void CmdUpdateVelocity(GameObject _player, Vector3 _velo)
    {
        _player.GetComponent<Rigidbody>().velocity = _velo;
        RpcUpdateVelo(_player, _velo);
    }

    [ClientRpc]
    void RpcUpdateVelo(GameObject _player, Vector3 _velo)
    {
        _player.GetComponent<Rigidbody>().velocity = _velo;
    }


    void ReadInputs()
    {
        var pHorizRight = player.GetButton("MoveHorizButtonRight");
        var pHorizLeft = player.GetButton("MoveHorizButtonLeft");
        var pVertUp = player.GetButton("MoveVertButtonUp");
        var pVertDown = player.GetButton("MoveVertButtonDown");

        // Rotate ship
        if (pHorizRight)
        {
            TiltShip(true);
        }
        else if (pHorizLeft)
        {
            TiltShip(false);
        }
        else
        {
            TiltBack();
        }


        // going front and back
        if (pVertUp)
            GainSpeed(true);
        else if (pVertDown)
            GainSpeed(false);
        else
            DropSpeed();

        //Debug.Log($"speeeed {myVertSpeed}");
    }

    /// <summary>
    /// Gradually gain speed
    /// </summary>
    void GainSpeed(bool _isPositive)
    {
        currentSpeedTimer -= Time.deltaTime;
        if (currentSpeedTimer <= 0) { currentSpeedTimer = 0; }       // cap it at 0
        var pPercent = 1 - (currentSpeedTimer / topSpeedTimer);

        if (_isPositive)
        {
            myVertSpeed = pPercent * topVertSpeed;
        }
        else
        {
            myVertSpeed = pPercent * -topVertSpeed;
        }
    }

    /// <summary>
    /// Gradually lose speed
    /// </summary>
    void DropSpeed()
    {
        currentSpeedTimer = topSpeedTimer;
        if (Mathf.Abs(myVertSpeed) > 1f)        // reduce
        {
            myVertSpeed = myVertSpeed > 0 ? myVertSpeed - Time.deltaTime * slowDownMultiplier : myVertSpeed + Time.deltaTime * slowDownMultiplier;
        }
        else
        {
            myVertSpeed = 0f;
        }

    }


    void TiltShip(bool _isGoingRight)
    {
        if (_isGoingRight)
        {
            var pRot = transform.rotation.eulerAngles;
            pRot.y += (rotSpeed * Time.deltaTime);
            pRot.z -= Time.deltaTime * pTiltSpeed;
            var pNegZ = pRot.z - 360;
            var pNewZ = pRot.z;

            // Constraint the left side rotation

            if (pNegZ <= -maxTiltAngle)
            {
                if (pNegZ > -180f)
                {
                    pNewZ = -maxTiltAngle;
                }
            }

            transform.rotation = Quaternion.Euler(new Vector3(pRot.x, pRot.y, pNewZ));
        }
        else
        {
            var pRot = transform.rotation.eulerAngles;
            pRot.y -= (rotSpeed * Time.deltaTime);
            pRot.z += Time.deltaTime * pTiltSpeed;

            var pNewZ = Mathf.Clamp(pRot.z, 0f, maxTiltAngle);
            if (pNewZ == maxTiltAngle) { maxTilted = true; }
            transform.rotation = Quaternion.Euler(new Vector3(pRot.x, pRot.y, pNewZ));
        }

        CmdSendRotation(this.gameObject, transform.rotation);
    }


    [Command]
    void CmdSendRotation(GameObject _player ,Quaternion _rot)
    {
        _player.transform.rotation = _rot;

        RpcSendRotation(_player, _rot);
    }

    [ClientRpc]
    void RpcSendRotation(GameObject _player, Quaternion _rot)
    {
        _player.transform.rotation = _rot;
    }


    /// <summary>
    /// Tilt back the spaceship if it was tilted
    /// </summary>
    async void TiltBack()
    {
        var pRot = transform.rotation.eulerAngles;
        if (Mathf.Abs(pRot.z) - 2f >= 0)        // leeway of 2 degree angle
        {
            if (pRot.z > 0f && pRot.z < 180f)
            {
                while (pRot.z > 0)
                {
                    pRot.z -= Time.deltaTime * pTiltSpeed * 4f; // twice as fast

                    transform.rotation = Quaternion.Euler(pRot);

                    if (pRot.z <= 0f || pRot.z > 180f) { break; }

                    await Task.Yield();
                }
            }
            else if (pRot.z > 180f || pRot.z < 0f)
            {
                while (pRot.z < 0f || pRot.z > 180f)
                {
                    pRot.z += Time.deltaTime * pTiltSpeed * 4f; // twice as fast

                    transform.rotation = Quaternion.Euler(pRot);

                    if (pRot.z >= 0f || pRot.z < 180f) { break; }

                    await Task.Yield();
                }
            }
            CmdSendRotation(this.gameObject, transform.rotation);
        }
    }


    async void BounceBack(float _targetAngle)
    {
        var pRot = transform.rotation.eulerAngles;
        if (Mathf.Abs(_targetAngle) < 5f)   // base case, exit out of recursive method
        {
            Debug.Log($"Successfully exited");
            return;
        }

        // Positive rotation
        if (_targetAngle < 0f)
        {
            while (pRot.z > 0 && pRot.z < 180f)
            {
                pRot.z -= Time.deltaTime * pTiltSpeed * 4f;
                transform.rotation = Quaternion.Euler(pRot);
                if (pRot.z >= 180f && pRot.z - 360f <= _targetAngle)
                {
                    maxTilted = false;
                    _targetAngle /= -2;

                    BounceBack(_targetAngle);
                }

                await Task.Yield();
            }
        }          // Negative rotation
        else if (_targetAngle > 0f)
        {
            while (pRot.z < 0 || pRot.z > 180f)     // negative
            {
                pRot.z += Time.deltaTime * pTiltSpeed * 4f;
                transform.rotation = Quaternion.Euler(pRot);

                await Task.Yield();
            }

            while (pRot.z > 0 && pRot.z < 180f && pRot.z <= _targetAngle)
            {
                pRot.z += Time.deltaTime * pTiltSpeed * 4f;
                transform.rotation = Quaternion.Euler(pRot);

                if (pRot.z >= _targetAngle)
                {
                    _targetAngle /= -2;

                    BounceBack(_targetAngle);
                }
                await Task.Yield();
            }
        }

    }


    private void OnDisable()
    {
        //GameManager.Inst.OnUpdatePosition -= UpdateMyPos;
    }

}
