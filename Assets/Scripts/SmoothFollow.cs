using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
public class SmoothFollow : MonoBehaviour {
    public static Vector3 target;
    public Vector3 offset;
    public float zoom = 10;
    void Update() {
        //nothing interesting here
        if (Input.GetAxis("Mouse ScrollWheel") > 0f) {
            zoom = zoom <= 2000 ? zoom * 1.5f : 2000;
        } else if (Input.GetAxis("Mouse ScrollWheel") < 0f) {
            zoom = zoom >= 5 ? zoom * -1.5f : 5;
        }
        if (zoom < 0) zoom = 5;
        offset = new Vector3(offset.x, zoom, offset.z);
        transform.position = Vector3.Lerp(transform.position, target + offset, Time.deltaTime * 5);
        transform.forward = Vector3.Lerp(transform.forward, target - transform.position, Time.deltaTime);
    }
}
