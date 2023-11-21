using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionManager : MonoBehaviour
{
    [SerializeField] float forceMult = 3f;

    PlayerMovement myPm;
    Rigidbody myRigidbody;

    float initMass;

    private void Start()
    {
        myPm = GetComponent<PlayerMovement>();
        initMass = GetComponent<Rigidbody>().mass;
        myRigidbody = GetComponent<Rigidbody>();
    }


    private void OnCollisionEnter(Collision _collision)
    {
        if(_collision.gameObject.GetComponent<CollisionManager>() && _collision.gameObject != this.gameObject)
        {
            var pEnemy = _collision.gameObject.GetComponent<PlayerMovement>();
            if(myPm.Speed == 0 || myPm.Speed < pEnemy.Speed) { return; }         // Only one should handle collision
            Debug.LogWarning($"I've impacted: {_collision.transform.parent.gameObject.name} when I have a speed of: {myPm.Speed} and they have a speed of {pEnemy.Speed}");


            HandleCollision(myPm, pEnemy);
        }
    }

    void HandleCollision(PlayerMovement _mySpaceship, PlayerMovement _enemySpaceship)
    {
        if(_mySpaceship.Speed > _enemySpaceship.Speed)
        {
            if(_enemySpaceship.Speed == 0)      // Only bounce the enemy one and keep ours intact
            {
                myRigidbody.velocity = new Vector3(myRigidbody.velocity.x, 0, myRigidbody.velocity.z);

                _enemySpaceship.GetComponent<Rigidbody>().velocity += myRigidbody.velocity;
            }
        }
    }

}
