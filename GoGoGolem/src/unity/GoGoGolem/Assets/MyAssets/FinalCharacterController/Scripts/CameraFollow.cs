using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    public Transform target;
    public float smoothTime = 0.1f;
    public Vector3 offset;
    private Vector3 velocity = Vector3.zero;
    
    // Update is called once per frame
    void Update()
    {
        if (target != null) {
            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        }
    }
}
