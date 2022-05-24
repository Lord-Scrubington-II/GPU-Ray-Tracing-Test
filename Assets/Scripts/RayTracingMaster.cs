using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    private RenderTexture renderTarget;
    private Camera masterCam;
    private readonly int DEFAULT_THREAD_GROUP_SIZE = 8;
    public readonly int MASTER_KERNEL_INDEX = 0;

    public Texture SkyboxTexture; // we can pass any texture to the compute shader, so let's do a skybox
    public Light Sun; // set in inspector, should be the scene's directional light. could upgrade to a list of lights

    // For AA
    private uint currentSample = 0;
    private Material AAMat;

    // For scene generation.
    // Perhaps we can define UI for controlling things like number of ray bounces,
    // use of the skybox, gizmos & unity transforms for manually placing spheres, etc. in real time...
    [SerializeField] Vector2 sphereRadiusRange = new Vector2(2.0f, 10.0f);
    [SerializeField] uint numSpheres = 100;
    [SerializeField] float spherePlacementRadius = 100.0f; // how large of a circle do we want to fill with spheres?
    [SerializeField] Vector2 sphereShininessRange = new Vector2(20.0f, 100.0f);
    private ComputeBuffer sphereBuffer;
    private const int FLOATS_IN_SHADER_SPHERE = 11; // used for calculating the buffer stride

    internal struct SimpleSphere
    {
        Vector3 centre;
        float radius;

        public SimpleMaterial material;

        public Vector3 Centre { get => centre; set => centre = value; }
        public float Radius { get => radius; set => radius = value; }
    }

    internal struct SimpleMaterial
    {
        Vector3 diffuse;
        Vector3 specular;
        float shininess;

        public Vector3 Diffuse { get => diffuse; set => diffuse = value; }
        public Vector3 Specular { get => specular; set => specular = value; }
        public float Shininess { get => shininess; set => shininess = value; }
    }

    private void Awake()
    {
        masterCam = gameObject.GetComponent<Camera>();
    }

    private void Update()
    {
        // For the sake of maintaining a good camera control framerate, 
        // we will only perform AA if the camera is currently not moving.
        // may remove later
        if (gameObject.transform.hasChanged) {
            currentSample = 0;
            gameObject.transform.hasChanged = false;
        }
    }

    private void OnEnable()
    {
        currentSample = 0;
        GenerateRandomSceneConfig();
    }

    private void OnDisable()
    {
        if (sphereBuffer != null) sphereBuffer.Release();
    }

    /// <summary>
    /// Generates a random scene of spheres with the inspector params.
    /// </summary>
    private void GenerateRandomSceneConfig()
    {
        List<SimpleSphere> spheres = new List<SimpleSphere>();

        // routine structure is adapted directly from `SetUpScene()` in 
        // http://three-eyed-games.com/2018/05/03/gpu-ray-tracing-in-unity-part-1/
        for (int i = 0; i < numSpheres; i++) {
            SimpleSphere sphere = new SimpleSphere();

            // set the radius and centre point of this sphere inside the defined placement circle
            sphere.Radius = sphereRadiusRange.x + Random.value * (sphereRadiusRange.y - sphereRadiusRange.x);
            Vector2 randomPos = Random.insideUnitCircle * spherePlacementRadius;
            sphere.Centre = new Vector3(randomPos.x, sphere.Radius, randomPos.y);

            // to deal with spheres that intersect each other, just reject them
            foreach (var otherSphere in spheres) {
                float minDistThresh = sphere.Radius + otherSphere.Radius; // must be dist apart equal to sum of their radii
                if (Vector3.Distance(sphere.Centre, otherSphere.Centre) < minDistThresh) {
                    goto NextSphere; // the only time you will ever see me use 'goto'
                }
            }

            // define a somewhat-random diffuse, specular, and shininess
            Color color = Random.ColorHSV();
            bool isMetal = Random.value < 0.5f; // half the spheres will be metals (highly specular), the other half matte (highly diffuse)

            if (isMetal) {
                sphere.material.Diffuse = Vector3.zero;
                sphere.material.Specular = new Vector3(color.r, color.g, color.b);
            } else {
                sphere.material.Diffuse = new Vector3(color.r, color.g, color.b);
                sphere.material.Specular = Vector3.one * 0.05f; // add 5% specularity, fully arbitrary
            }
            sphere.material.Shininess = sphereShininessRange.x + Random.value * (sphereShininessRange.y - sphereShininessRange.x);

            spheres.Add(sphere);

            NextSphere: continue;
        }

        // now, we must push the list of spheres to the compute buffer.
        sphereBuffer = new ComputeBuffer(spheres.Count, sizeof(float) * FLOATS_IN_SHADER_SPHERE);
        sphereBuffer.SetData(spheres);
    }


    /// <summary>
    /// This function is called by the Unity Runtime at the end of the standard rendering pipeline. 
    /// It allows me to effectively extend the graphics pipline by one extra post-processing step.
    /// </summary>
    /// <param name="source">The screen texture produced by the rendering pipeline.</param>
    /// <param name="destination">A RenderTexture that contains the final post-processed image.</param>
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        PassShaderParams();
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        InitRenderTexture();

        // Pass the cached texture to the compute shader and dispatch the GPU threads
        RayTracingShader.SetTexture(MASTER_KERNEL_INDEX, "Result", renderTarget);
        int threadGroupsinX = Mathf.CeilToInt(Screen.width / (float)DEFAULT_THREAD_GROUP_SIZE); 
        int threadGroupsinY = Mathf.CeilToInt(Screen.width / (float)DEFAULT_THREAD_GROUP_SIZE);
        RayTracingShader.Dispatch(MASTER_KERNEL_INDEX, threadGroupsinX, threadGroupsinY, 1); // we are using the default thread group size, one per 8x8 pixel grid.

        // AA routine
        if (AAMat == null) {
            AAMat = new Material(Shader.Find("Hidden/AddShader")); // "Hidden/AddShader" is the name of the image effect shader used for AA
        }
        AAMat.SetFloat("_Sample", (float)currentSample);
        Graphics.Blit(renderTarget, destination, AAMat); // blit over the screen with alpha blending to accomplish AA
        currentSample++;

        // Graphics.Blit() will copy the source texture into the destination. 
        // in this case, we want to simply write the cached RenderTexture into the destination buffer.
        // Graphics.Blit(renderTarget, destination);
    }

    private void InitRenderTexture()
    {
        if (renderTarget == null || renderTarget.width != Screen.width || renderTarget.height != Screen.height) {

            // if we already have a render texture, release it
            if (renderTarget != null) renderTarget.Release();

            // this will get a target render texture for ray tracing
            renderTarget = new RenderTexture(
                Screen.width,
                Screen.height,
                0,
                RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear
            ) {
                enableRandomWrite = true // enable random-access write to this texture
            };
            renderTarget.Create();
        }
    }

    private void PassShaderParams()
    {
        // pass the camera's model-view and projection matrix inverses to the compute shader
        RayTracingShader.SetMatrix("_CameraToWorld", masterCam.cameraToWorldMatrix); // string reference bad, but it will do for now
        RayTracingShader.SetMatrix("_CameraInverseProjection", masterCam.projectionMatrix.inverse);

        // spherical skybox texture
        RayTracingShader.SetTexture(MASTER_KERNEL_INDEX, "_SkyboxTexture", SkyboxTexture);

        // light(s)
        Vector3 sunlight_dir = Sun.transform.forward;
        RayTracingShader.SetVector("_Sun", new Vector4(sunlight_dir.x, sunlight_dir.y, sunlight_dir.z, Sun.intensity));

        // random vector for AA jittering
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

        // pass the scene data to the shader!
        RayTracingShader.SetBuffer(0, "_Spheres", sphereBuffer);
    }
}
