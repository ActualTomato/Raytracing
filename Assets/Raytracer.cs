using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering.UI;

public class Raytracer : MonoBehaviour
{
    public Color backgroundColor = new Color(183,222,232);
    public Color missingColor = new Color(216,47,228);

    public bool optimize = true;
    public int maxRecursionDepth;
    public Material cameraBlitMaterial;
    public int verticalResolution = 100;
    public int horizontalResolution = 100;
    public bool isEnabled;
    int counter = 0;
    float bias = 0.001f;
    
    [SerializeField]
    bool showRaycasts = false;
    float colorLerpSpeed = 0.01f;
    Texture2D render;
    Color currentColor = Color.black;
    Color targetColor = Color.black;
    new Camera camera;

    // Start is called before the first frame update
    void Start()
    {
        render = new Texture2D(horizontalResolution, verticalResolution);
        cameraBlitMaterial.mainTexture = render;

        camera = Camera.main;
        isEnabled = cameraBlitMaterial.GetInt("_isEnabled") == 1 ? true : false;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space)){
            int current = cameraBlitMaterial.GetInt("_isEnabled");
            isEnabled = !isEnabled;
            cameraBlitMaterial.SetInt("_isEnabled", current == 1 ? 0 : 1);

        }

        if(Input.GetKeyDown(KeyCode.O)){
            optimize = !optimize;
        }

        if(isEnabled){

            // update render resolution
            if(horizontalResolution != render.width || verticalResolution != render.height){
                render.Reinitialize(horizontalResolution,verticalResolution);
            }

            Render();
        }
    }

    Color Trace(Ray ray, int depth){
        // exit if depth is greater than max depth
        if(depth >= maxRecursionDepth){
            return backgroundColor;
        }

        // show camera rays if enabled
        if(showRaycasts){
            Debug.DrawRay(camera.transform.position, ray.direction * 1000, Color.cyan);
        }

        RaycastHit obj;

        // return background color if nothing is hit
        if(!Physics.Raycast(ray:ray, hitInfo: out obj, maxDistance:Mathf.Infinity)){
            return backgroundColor;
        }

        // get properties of raytraced material
        RaytracedMaterialProperties properties = obj.transform.GetComponent<RaytracedMaterialProperties>();

        // render with properties
        if(properties != null){

            

            Color surfaceColor = (properties.surfaceColor * ComputeLighting(obj.point, obj.normal, -ray.direction, properties.specular)) * (1 - properties.transparency);

            if (properties.transparency > 0 || properties.smoothness > 0) {
                float facingratio = -Vector3.Dot(ray.direction, obj.normal);

                // change the mix value to tweak the effect
                float fresneleffect = Mathf.Lerp(Mathf.Pow(1-facingratio, 3), properties.smoothness, 0.1f);

                // compute reflection direction
                Vector3 reflectionDir = ReflectRay(-ray.direction, obj.normal);
                Ray reflectionRay = new Ray(obj.point + (obj.normal * bias), reflectionDir);
                Color reflection = Trace(reflectionRay, depth + 1);

                Color refraction = Color.black;
                // if the sphere is also transparent compute refraction ray (transmission)
                if (properties.transparency > 0) {
                    Vector3 refractionDir = RefractRay(properties.IOR, obj.normal, -ray.direction);
                    Ray refractionRay = new Ray(obj.point + (-obj.normal * bias), refractionDir);
                    refraction = Trace(refractionRay, depth + 1);
            }
            // the result is a mix of reflection and refraction (if the sphere is transparent)
            surfaceColor += (
                reflection * fresneleffect +
                refraction * (1 - fresneleffect) * (properties.transparency));
    }

            
            surfaceColor.a = 1;
            return surfaceColor;

        }
        else{
            // renders a blank color if material properties are missing
            return missingColor;
        }

    }

    float ComputeLighting(Vector3 P, Vector3 N, Vector3 V, float specular){
        float intensity = 0;
        foreach(RaytracedLight light in RaytracedLight.lights){
            if(light.type == RaytracedLight.LightType.ambient){
                intensity += light.intensity;
            }
            else{
                Vector3 L;
                if(light.type == RaytracedLight.LightType.point){
                     L = light.transform.position - P;
                }
                else{
                    L = -light.transform.forward;
                }

                // Shadows
                RaycastHit shadow;
                Ray shadowRay = new Ray(P + (N * 0.01f), L);
                if(Physics.Raycast(ray:shadowRay, hitInfo: out shadow, maxDistance:Mathf.Infinity)){
                    continue;
                }

                // Diffuse
                float denom = N.magnitude * L.magnitude;
                float nDotL = Vector3.Dot(N,L);
                if(nDotL > 0){
                    intensity += light.intensity * (nDotL/denom);
                }

                // Specular
                if(specular != -1){
                    Vector3 R = ReflectRay(L, N);
                    float rDotV = Vector3.Dot(R,V);
                    if(rDotV > 0){
                        intensity += light.intensity * Mathf.Pow(rDotV/(R.magnitude * V.magnitude), specular);
                    }
                }
            }
        }

        return intensity;
    }

    Vector3 ReflectRay(Vector3 R, Vector3 N){
        return (2 * N * Vector3.Dot(R, N) - R).normalized;
    }

    Vector3 RefractRay(float ior, Vector3 I, Vector3 N){
        I.Normalize();
        N.Normalize();
        ior = 1/ior;
        float k = 1.0f - ior * ior * (1.0f - Vector3.Dot(N, I) * Vector3.Dot(N, I));
        if(k < 0){
            return Vector3.zero;
        }
        return (ior * I - (ior * Vector3.Dot(N, I) + Mathf.Sqrt(k)) * N).normalized;
    }

    void Render(){
        for(int w = 0; w < render.width; w++){
            for(int h = 0; h < render.height; h++){
                if(optimize){
                    if(counter == 0){
                        if(w%2==0){
                            continue;
                        }
                    }
                    else{
                        if(w%2==1){
                            continue;
                        }
                    }
                }
                Ray ray = camera.ViewportPointToRay(new Vector3((w+0.5f)/render.width,(h+0.5f)/render.height,0));
                render.SetPixel(w,h,Trace(ray,0));
                
            }
        }
        render.Apply();
        counter = (counter == 1 ? 0 : 1);
    }



    void DrawRays(){
        //Ray ray = camera.ViewportPointToRay(new Vector3(0.5f,0.5f,0));
        //Debug.DrawRay(camera.transform.position, ray.direction, Color.red);

        for(int w = 0; w < render.width; w++){
            for(int h = 0; h < render.height; h++){
                Debug.DrawRay(camera.transform.position, camera.ViewportPointToRay(new Vector3((w+0.5f)/render.width,(h+0.5f)/render.height,0) ).direction, Color.red);
            }
        }
    }

    void DrawSillhouette(){
        for(int w = 0; w < render.width; w++){
            for(int h = 0; h < render.height; h++){
                RaycastHit initialHit;
                if(Physics.Raycast(camera.ViewportPointToRay(new Vector3((w+0.5f)/render.width,(h+0.5f)/render.height,0)), out initialHit, Mathf.Infinity)){
                    
                    if(showRaycasts){
                        //Debug.DrawRay(initialHit.point, -sun.transform.forward, Color.cyan);
                    }

                    //if(Physics.Raycast(initialHit.point, -sun.transform.forward, out RaycastHit sunHit)){
                        //if(sunHit.transform != initialHit.transform){
                            //render.SetPixel(w,h,Color.black);
                        //}
                        //else{
                        //    render.SetPixel(w,h,Color.white);
                        //}
                        
                    //}
                    //else{
                    //    render.SetPixel(w,h,Color.white);
                    //}

                }
                else{
                    render.SetPixel(w,h,Color.blue);
                }
            }
        }
        render.Apply();
    }

    void DrawColors(){
        if(RoundingCompare(currentColor, targetColor, 0.05f)){
            targetColor = Random.ColorHSV();
        }
        currentColor = Color.Lerp(currentColor,targetColor,colorLerpSpeed);
        for(int w = 0; w < render.width; w++){
            for(int h = 0; h < render.height; h++){
                render.SetPixel(w,h,currentColor/((float)h/10));
                cameraBlitMaterial.mainTexture = render;
            }
        }
        render.Apply();
    }

    public static bool RoundingCompare(Color color, Color otherColor, float tolerance){
        if(Mathf.Abs(color.r - otherColor.r) < tolerance){
            if(Mathf.Abs(color.g - otherColor.g) < tolerance){
                if(Mathf.Abs(color.b - otherColor.b) < tolerance){
                    return true;
                }
            }
        }

        return false;
    }

}
