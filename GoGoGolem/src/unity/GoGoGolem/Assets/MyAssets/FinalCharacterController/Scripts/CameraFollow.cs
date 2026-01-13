using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    public Transform target;
    public float smoothTime = 0.1f;
    public Vector3 offset = new Vector3(0f, 1f, -3f);
    public Vector3 rotation = new Vector3(0f, 0f, 0f);
    private Vector3 velocity = Vector3.zero;
    
    private void Start()
    {
        // Set initial rotation
        transform.rotation = Quaternion.Euler(rotation);
    }
    
    // Update is called once per frame
    void Update()
    {
        if (target != null) {
            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        }
    }
}
