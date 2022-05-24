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

    protected struct SimpleSphere
    {
        Vector3 centre;
        Vector3 radius;

        SimpleMaterial material;
    }

    protected struct SimpleMaterial
    {
        Vector3 diffuse;
        Vector3 specular;
        float shininess;
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
    }
}
