using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    private RenderTexture _target;
    private Camera _masterCam;
    private readonly int DEFAULT_THREAD_GROUP_SIZE = 8;

    private void Awake()
    {
        _masterCam = gameObject.GetComponent<Camera>();
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
        RayTracingShader.SetTexture(0, "RTResult", _target);
        int threadGroupsinX = Mathf.CeilToInt(Screen.width / (float)DEFAULT_THREAD_GROUP_SIZE); 
        int threadGroupsinY = Mathf.CeilToInt(Screen.width / (float)DEFAULT_THREAD_GROUP_SIZE);
        RayTracingShader.Dispatch(0, threadGroupsinX, threadGroupsinY, 1); // we are using the default thread group size, one per 8x8 pixel grid.

        // Graphics.Blit() will copy the source texture into the destination. 
        // in this case, we want to simply write the cached RenderTexture into the destination buffer.
        Graphics.Blit(_target, destination);
    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height) {

            // if we already have a render texture, release it
            if (_target != null) _target.Release();

            // this will get a target render texture for ray tracing
            _target = new RenderTexture(
                Screen.width,
                Screen.height,
                0,
                RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear
            ) {
                enableRandomWrite = true // enable random-access write to this texture
            };
            _target.Create();
        }
    }

    private void PassShaderParams()
    {
        // pass the camera's model-view and projection matrix inverses to the compute shader
        RayTracingShader.SetMatrix("_CameraToWorld", _masterCam.cameraToWorldMatrix); // string reference bad, but it will do for now
        RayTracingShader.SetMatrix("_CameraInverseProjection", _masterCam.projectionMatrix.inverse);
    }
}
