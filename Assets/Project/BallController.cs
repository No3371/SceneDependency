using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallController : MonoBehaviour
{
    public new Rigidbody rigidbody;
    public float force = 10;

    void Update ()
    {
        Camera.main.transform.position = this.transform.position + Vector3.up * 10 + Vector3.back * 3;
        Camera.main.transform.LookAt(this.transform, Vector3.up);
    }

    void FixedUpdate ()
    {
        if (Input.GetKey(KeyCode.W)) rigidbody.AddForce(Vector3.forward * force, ForceMode.Acceleration);
        if (Input.GetKey(KeyCode.S)) rigidbody.AddForce(Vector3.back * force, ForceMode.Acceleration);
        if (Input.GetKey(KeyCode.A)) rigidbody.AddForce(Vector3.left * force, ForceMode.Acceleration);
        if (Input.GetKey(KeyCode.D)) rigidbody.AddForce(Vector3.right * force, ForceMode.Acceleration);
    }
}
