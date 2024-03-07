using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class RaytracedLight : MonoBehaviour
{
    public static List<RaytracedLight> lights = new List<RaytracedLight>();
    public LightType type;
    public float intensity = 1;
    void OnEnable(){
        lights.Add(this);
    }

    void OnDisable(){
        lights.Remove(this);
    }

    void Update(){
        if(type == LightType.directional){
            Debug.DrawRay(transform.position, transform.forward, Color.yellow);
        }
    }


    public enum LightType{
        ambient,
        point,
        directional
    }
}
