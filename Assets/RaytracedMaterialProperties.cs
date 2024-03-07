using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaytracedMaterialProperties : MonoBehaviour
{
    public Color surfaceColor = Color.white;
    [SerializeField, Range(0,1)]
    public float transparency = 0;
    public float indexOfRefraction = 1;
    [SerializeField, Range(0,1500)]
    public float specular = -1;
    [SerializeField, Range(0,1)]
    public float reflectivity = 0;
}
