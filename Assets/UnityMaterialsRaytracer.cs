using System.Collections;
using UnityEngine;


public class UnityMaterialsRaytracer : MonoBehaviour
{
    [Header("Config")]
    public Texture2D skybox;
    [Tooltip("Only updates alternating rows every other frame, kind of like interlacing?")]
    public bool optimize = true;
    public Vector2Int resolution = new Vector2Int(160,90);
    public int maxTraceRecursionDepth = 5;
    
    public float bias = 0.01f;
    public Material cameraBlitMaterial;

    // misc variables
    Texture2D render;
    int optimizeCounter = 0;
    Camera mainCamera;

    //  MeshCollider data
    Hashtable meshDatas;

    // Start is called before the first frame update
    void OnEnable()
    {
        render = new Texture2D(resolution.x, resolution.y);
        cameraBlitMaterial.mainTexture = render;
        Physics.queriesHitBackfaces = true;
        mainCamera = Camera.main;

        // Add all MeshCollider data for every object in scene to HashTable at the start to prevent continuous access in GetInterpolatedNormals()
        meshDatas = new Hashtable();
        foreach(MeshCollider collider in GameObject.FindObjectsOfType(typeof(MeshCollider))){
            MeshData data = new MeshData(collider);
            GameObject obj = collider.gameObject;
            meshDatas.Add(obj, data);
        }
    }

    // Update is called once per frame
    void Update()
    {
        /*

        SKYBOX ANGLE TEST, DISREGARD

        Vector3 Va = mainCamera.transform.forward;
        Vector3 Vb = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);
        Vector3 Vn = mainCamera.transform.right;
        print(Mathf.Atan2(Vector3.Dot(Vector3.Cross(Va, Vb), Vn), Vector3.Dot(Va, Vb)));
        */


        // toggle if raytracer is enabled
        if(Input.GetKeyDown(KeyCode.Space)){
            int current = cameraBlitMaterial.GetInt("_isEnabled");
            cameraBlitMaterial.SetInt("_isEnabled", current == 1 ? 0 : 1);
        }

        // toggle optimize
        if(Input.GetKeyDown(KeyCode.O)){
            optimize = !optimize;
        }

        if(cameraBlitMaterial.GetInt("_isEnabled") == 1){
            // update render resolution
            if(resolution.x != render.width || resolution.y != render.height){
                render.Reinitialize(resolution.x,resolution.y);
            }

            Render();
        }
        
    }

    void Render(){
        for(int w = 0; w < render.width; w++){
            for(int h = 0; h < render.height; h++){

                // skip every other line if optimize is enabled
                if(optimize){
                    if(optimizeCounter == 0){
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

                Ray ray = mainCamera.ViewportPointToRay(new Vector3((w+0.5f)/render.width,(h+0.5f)/render.height,0));
                render.SetPixel(w,h,Trace(ray, 0));
            }
        }
        render.Apply();
        optimizeCounter = optimizeCounter == 1 ? 0 : 1;
    }

    Color Trace(Ray ray, int depth){
        // exit if recursion limit is reached (default is 5)
        if(depth >= maxTraceRecursionDepth){
            return Color.black;
        }

        bool isHit = Physics.Raycast(ray, out RaycastHit hit, maxDistance:Mathf.Infinity);

        if(isHit){

            Vector3 normal = hit.normal;
            MeshCollider meshCollider = hit.collider as MeshCollider;
            if (meshCollider != null && meshCollider.sharedMesh != null)
            {
                // mesh collider does not return interpolated normals, use function to get barycentric interpolated normals
                normal = GetInterpolatedNormal(hit);
            }

            Renderer renderer = hit.transform.GetComponent<Renderer>();
            if(renderer){
                // Get material properties
                RaytracedMaterialProperties properties = hit.collider.gameObject.GetComponent<RaytracedMaterialProperties>();
                if(!properties){
                    // Return unshaded magenta color (like in minecraft) if material properties are missing

                    //return new Color(216,47,228);
                    return (Color.blue + (Color.red*0.4f) + (Color.white * 0.5f)) - (Color.white * 0.05f);
                }

                Texture2D texture = properties.texture;
                Color baseColor = properties.surfaceColor;
                float specular = properties.specular;
                float smoothness = properties.smoothness;
                float metallic = properties.metallicity;
                float indexOfRefraction = properties.IOR;

                // default texel color is white so it doesnt affect render if there is no texture
                Color texelColor = Color.white;


                /* old skybox code (used with an inverted sphere object as the skybox)

                if(hit.collider.gameObject.tag.Equals("Skybox")){
                    texelColor = texture.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
                    return texelColor;
                }

                */

                
                float lighting = ComputeLighting(hit.point, normal, -ray.direction, specular);

                // raycast cannot get texture coordinates unless using a mesh collider
                if(meshCollider != null && texture != null){
                    texelColor = texture.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
                }

                Color coloredTexel = texelColor * baseColor;

                Vector3 shift = Vector3.Dot(ray.direction, normal) > 0 ? (normal * bias) : (-normal * bias);
                float kr = CalcFresnelEffect(ray.direction, normal, indexOfRefraction);
                float kt = 1 - kr;

                Color reflectionColor = Color.white;
                if(properties.isGlass || properties.isMetallic || smoothness > 0){
                    Ray reflectionRay = new Ray(hit.point - shift, Vector3.Reflect(ray.direction, normal).normalized);
                    reflectionColor = Trace(reflectionRay, depth + 1);
                }

                if(properties.isGlass){
                    Color refractionColor = Color.black;

                    if(kr < 1){
                        Ray refractionRay = new Ray(hit.point + shift, Refract(ray.direction, normal, indexOfRefraction).normalized);
                        refractionColor = Trace(refractionRay, depth + 1);
                    }

                    return ((Color.Lerp(Color.white, coloredTexel, properties.glassTintAmount) * refractionColor * kt) + reflectionColor * kr ) + (new Color(1,1,1) * computeSpecular(hit.point, normal, -ray.direction, 1500));
                }
                else if(properties.isMetallic){

                    return Color.Lerp(coloredTexel * lighting, coloredTexel * reflectionColor * lighting, metallic) + coloredTexel * computeSpecular(hit.point, normal, -ray.direction, specular);
                }
                return Color.Lerp(coloredTexel * lighting , (coloredTexel * kt) * lighting + (reflectionColor * kr) , smoothness);
                
            }
        }

        //
        // SKYBOX CODE its a mess i know (also it doesnt properly map the texture but it looks good enough so whatever)
        
        Vector3 Va = ray.direction;
        Vector3 Vbx = Vector3.ProjectOnPlane(ray.direction, Vector3.right);
        Vector3 Vnx = mainCamera.transform.up;
        float x = (Mathf.Atan2(Vector3.Dot(Vector3.Cross(Va, Vbx), Vnx), Vector3.Dot(Va, Vbx)))/(Mathf.PI/2);

        Vector3 Vby = Vector3.ProjectOnPlane(ray.direction, Vector3.up);
        Vector3 Vny = mainCamera.transform.right;
        float y = (Mathf.Atan2(Vector3.Dot(Vector3.Cross(Va, Vby), Vny), Vector3.Dot(Va, Vby)))/(Mathf.PI/2);
        
        return skybox.GetPixelBilinear(x+0f, y+0.5f);
        //return skybox.GetPixelBilinear(Vector3.Angle(ray.direction, Vector3.ProjectOnPlane(ray.direction, Vector3.right)) / 90f, Vector3.Angle(ray.direction, Vector3.ProjectOnPlane(ray.direction, Vector3.up)) / 90f);;
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
                    intensity += intensity * computeSpecular(P, N, V, specular);
                }
            }
        }

        return intensity;
    }

    float computeSpecular(Vector3 P, Vector3 N, Vector3 V, float specular){
        float intensity = 0;
        foreach(RaytracedLight light in RaytracedLight.lights){
            if(light.type == RaytracedLight.LightType.ambient){
                continue;
            }
            Vector3 L;
            if(light.type == RaytracedLight.LightType.point){
                    L = light.transform.position - P;
            }
            else{
                L = -light.transform.forward;
            }

            Vector3 R = ReflectRay(L, N);
            float rDotV = Vector3.Dot(R,V);
            if(rDotV > 0){
                intensity += Mathf.Pow(rDotV/(R.magnitude * V.magnitude), specular) * light.intensity;
            }
            
        }
        return intensity;
    }

    Vector3 ReflectRay(Vector3 R, Vector3 N){
        return (2 * N * Vector3.Dot(R, N) - R).normalized;
    }

    float CalcFresnelEffect(Vector3 I, Vector3 N, float ior){
        // kr = amount of light reflected
        // kt = amount of light transmitted

        float kr;

        float cosi = Mathf.Clamp(Vector3.Dot(I, N), -1, 1);
        float etai = 1, etat = ior;
        if (cosi > 0) {
            etai = ior;
            etat = 1;
        }
        // Snell's law
        float sint = etai / etat * Mathf.Sqrt(Mathf.Max(0, 1 - cosi * cosi));

        // TIR
        if (sint >= 1) {
            kr = 1;
        }
        else {
            float cost = Mathf.Sqrt(Mathf.Max(0, 1 - sint * sint));
            cosi = Mathf.Abs(cosi);
            float Rs = ((etat * cosi) - (etai * cost)) / ((etat * cosi) + (etai * cost));
            float Rp = ((etai * cosi) - (etat * cost)) / ((etai * cosi) + (etat * cost));
            kr = (Rs * Rs + Rp * Rp) / 2;
        }

        // kt = 1 - kr
        return kr;
    }

    Vector3 Refract(Vector3 I, Vector3 N, float ior){
        Vector3 normal = N;
        float cosi = Mathf.Clamp(Vector3.Dot(normal, I), -1, 1);
        float etai = 1.0f;
        float etat = ior;
        if(cosi < 0){
            cosi = -cosi;
        }
        else{
            normal = -N;
            etai = ior;
            etat = 1;
        }

        float eta = etai / etat;

        float k = 1 - eta * eta * (1 - cosi * cosi);
        
        if(k < 0){
            return Vector3.zero;
        }

        return eta * I + (eta * cosi - Mathf.Sqrt(k)) * normal;
    }

    Vector3 GetInterpolatedNormal(RaycastHit hit){

        // Get Normals and Triangles array
        MeshData meshData = (MeshData)meshDatas[hit.collider.gameObject];
        Vector3[] normals = meshData.normals;
        int[] triangles = meshData.triangles;
        

        // Extract local space normals of the triangle we hit
        Vector3 n0 = normals[triangles[hit.triangleIndex * 3 + 0]];
        Vector3 n1 = normals[triangles[hit.triangleIndex * 3 + 1]];
        Vector3 n2 = normals[triangles[hit.triangleIndex * 3 + 2]];

        // interpolate using the barycentric coordinate of the hitpoint
        Vector3 baryCenter = hit.barycentricCoordinate;

        // Use barycentric coordinate to interpolate normal
        Vector3 interpolatedNormal = n0 * baryCenter.x + n1 * baryCenter.y + n2 * baryCenter.z;
        // normalize the interpolated normal
        interpolatedNormal = interpolatedNormal.normalized;

        // Transform local space normals to world space
        Transform hitTransform = hit.collider.transform;
        interpolatedNormal = hitTransform.TransformDirection(interpolatedNormal);
        return interpolatedNormal;
    }

    // kind of works but very grainy due to random nature, increasing index sort of works
    // need to make a depth buffer based effect for SSAO
    float ComputeAmbientOcclusion(Vector3 point, Vector3 normal){
            float occlusion = 1.0f;
            int index = 128;

            for(int i = 0; i < index; i++){
                Vector3 sampleDir = Random.onUnitSphere;
                float dot = Vector3.Dot(sampleDir, normal);

                if(dot > 0){
                    RaycastHit hit;
                    if(Physics.Raycast(point + normal * bias, sampleDir, out hit)){
                        occlusion -= 1.0f / index;
                        if(occlusion < 0.0f) {
                            occlusion = 0.0f;
                        }
                    }
                }
            }

        return occlusion;
    }

    struct MeshData{
        public MeshCollider mesh;
        public int[] triangles;
        public Vector3[] normals;

        public MeshData(MeshCollider collider){
            this.mesh = collider;
            triangles = mesh.sharedMesh.triangles;
            normals = mesh.sharedMesh.normals;
        }
    }
}

