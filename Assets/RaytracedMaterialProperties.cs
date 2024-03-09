using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class RaytracedMaterialProperties : MonoBehaviour
{
    [Header("Texture Properties")]
    public Texture2D texture;
    public Color surfaceColor = Color.gray;
    [Header("Surface Properties")]
    [SerializeField, Range(0,1)]
    public float transparency = 0;
    [SerializeField, Range(1,1500)]
    public float specular = 100;

    [SerializeField, Range(0,1)]
    public float smoothness = 0;
    [Tooltip("Metallic disables smoothness properties")]
    public bool isMetallic = false;
    
    [SerializeField, Range(0,1)]
    public float metallicity = 0;
    
    [Space]

    [Header("Glass Settings")]
    [Tooltip("Disables regular transparency, reflectivity, and specular settings.")]
    public bool isGlass = false;
    public float IOR = 1.375f;
    [SerializeField, Range(0,1)]
    public float glassTintAmount = 0;
}
