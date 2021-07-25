using System.Diagnostics;
using System.Numerics;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Rendering;


namespace com.RogerioLima.ArOpneCV
{
    public class GrabCameraImage : MonoBehaviour
    {
        Texture2D m_Texture;
        XRCpuImage m_image;
        [SerializeField] ARCameraManager arCameraManager;
        [SerializeField] RawImage rawImage, hs_rawImage, hd_rawImage;
        [SerializeField] UnityEngine.Matrix4x4 m_displayMatrix;
        [SerializeField] AROcclusionManager occlusionManager;
        [SerializeField] ARCameraBackground m_ArCameraBackground;
        [SerializeField] GameObject o_Camera;

        void OnEnable()
        {
            arCameraManager.frameReceived += OnCameraFrameReceived;
        }

        void OnDisable()
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }


        //updates on camera frame received
        unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
        {
            // GrabImagePlanes();
            GrabHumanStencil();
            GrabHumanDepth();
            // GrabDisplayMatrix();
            DisplayCameraOnRawImage();
            // GrabCameraBuffer();
        }   



        //Acquire CPU Image planes for image processing
        //A XRCpuImage.Plane provides direct access to a native memory buffer via a NativeArray<byte>. 
        unsafe void GrabImagePlanes()
        {
            if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                return;

            XRCpuImage m_image = image;
            
            var conversionParams = new XRCpuImage.ConversionParams
            {
                // Get the entire image.
                inputRect = new RectInt(0, 0, m_image.width, m_image.height),

                // Downsample by 2.
                outputDimensions = new Vector2Int(m_image.width / 8, m_image.height / 8),

                // Choose RGBA format.
                outputFormat = TextureFormat.RGBA32,

                // Flip across the vertical axis (mirror image).
                transformation = XRCpuImage.Transformation.MirrorY
            };

            // See how many bytes you need to store the final image.
            int size = m_image.GetConvertedDataSize(conversionParams);

            // Allocate a buffer to store the image.
            var buffer = new NativeArray<byte>(size, Allocator.Temp);

            // Extract the image data
            m_image.Convert(conversionParams, new IntPtr(buffer.GetUnsafePtr()), buffer.Length);
            
            var luminancePlane = m_image.GetPlane(0);
            var colorPlane = m_image.GetPlane(1);

            if(luminancePlane != null)
            {
                ProcessLuminancePlane(luminancePlane.data);

            }

            if(colorPlane != null)
            {
                ProcessColorPlane(colorPlane.data);
            }


            // Consider each image plane.
            for (int planeIndex = 0; planeIndex < m_image.planeCount; ++planeIndex)
            {
                // Log information about the image plane.
                var plane = m_image.GetPlane(planeIndex);
                UnityEngine.Debug.LogFormat("Plane {0}:\n\tsize: {1}\n\trowStride: {2}\n\tpixelStride: {3}\n\timagewidth: {4}\n\timageHeight: {5}",
                planeIndex, plane.data.Length, plane.rowStride, plane.pixelStride, image.width, image.height);

                // Do something with the data.
                //MyComputerVisionAlgorithm(plane.data);
            }

            // Dispose the XRCpuImage to avoid resource leaks.
            image.Dispose();
        }
        

        void ProcessLuminancePlane(NativeArray<byte> plane)
        {
            UnityEngine.Debug.Log("Pronto para processar os dados de luminancia");
        }

        void ProcessColorPlane(NativeArray<byte> plane)
        {
            UnityEngine.Debug.Log("Pronto para processar os dados de cor");
        }

        //Acquire Camera Display Matrix information
        void GrabDisplayMatrix()
        {
            XRCameraParams cameraParams = new XRCameraParams 
            {
                // zNear = Camera.nearClipPlane,
                // zFar = Camera.farClipPlane,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                screenOrientation = Screen.orientation
            };

            XRCameraFrame cameraFrame;
            if (arCameraManager.subsystem.TryGetLatestFrame(cameraParams, out cameraFrame)) 
            {
                if(cameraFrame.hasDisplayMatrix)
                {
                    m_displayMatrix = cameraFrame.displayMatrix;
                    UnityEngine.Debug.Log("Display Matrix acquired");
                    UnityEngine.Debug.Log(m_displayMatrix.ToString());
                }

            }
            else 
            {
                UnityEngine.Debug.Log("No display Matrix for you, babe!");
            }

        }


        //Grab and display human Stencil Image
        void GrabHumanStencil()
        {
            if(occlusionManager.humanStencilTexture != null)
            {
                Texture2D h_segmentation = occlusionManager.humanStencilTexture;
                hs_rawImage.texture = h_segmentation;
            }
            else
            {
                UnityEngine.Debug.Log("No Human segmentation Texture");
            }  

        }

        void GrabHumanDepth()
        {
            if(occlusionManager.humanDepthTexture != null)
            {
                Texture2D h_depth = occlusionManager.humanDepthTexture;
                hd_rawImage.texture = h_depth;
            }
            else
            {
                UnityEngine.Debug.Log("No Human Depth Texture");
            }
        }

        //Display image using Command Buffers
        void GrabCameraBuffer()
        {
            var commandBuffer = new CommandBuffer();
            commandBuffer.name = "AR Camera Background Blit Pass";

            var texture = !m_ArCameraBackground.material.HasProperty("_MainTex") ? null : m_ArCameraBackground.material.GetTexture("_MainTex");
            Camera m_Camera = o_Camera.GetComponent<Camera>();
            
            RenderTexture renderTexture = m_Camera.targetTexture;
            Graphics.SetRenderTarget(renderTexture.colorBuffer, renderTexture.depthBuffer);
            commandBuffer.ClearRenderTarget(true, false, Color.clear);
            commandBuffer.Blit(texture, BuiltinRenderTextureType.CurrentActive, m_ArCameraBackground.material);
            Graphics.ExecuteCommandBuffer(commandBuffer);
        }

        //Acquire camera image and display it on a raw image
        unsafe void DisplayCameraOnRawImage()
        {
            
            if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                return;

            XRCpuImage m_image = image;

                var conversionParams = new XRCpuImage.ConversionParams
            {
                // Get the entire image.
                inputRect = new RectInt(0, 0, m_image.width, m_image.height),

                // Downsample by 2.
                outputDimensions = new Vector2Int(m_image.width / 8, m_image.height / 8),

                // Choose RGBA format.
                outputFormat = TextureFormat.RGBA32,

                // Flip across the vertical axis (mirror image).
                transformation = XRCpuImage.Transformation.MirrorY
            };

            

            // See how many bytes you need to store the final image.
            int size = m_image.GetConvertedDataSize(conversionParams);

            // Allocate a buffer to store the image.
            var buffer = new NativeArray<byte>(size, Allocator.Temp);

            // Extract the image data
            m_image.Convert(conversionParams, new IntPtr(buffer.GetUnsafePtr()), buffer.Length);

            // The image was converted to RGBA32 format and written into the provided buffer
            // so you can dispose of the XRCpuImage. You must do this or it will leak resources.
            m_image.Dispose();

            // At this point, you can process the image, pass it to a computer vision algorithm, etc.
            // In this example, you apply it to a texture to visualize it.

            // You've got the data; let's put it into a texture so you can visualize it.
            m_Texture = new Texture2D(
                conversionParams.outputDimensions.x,
                conversionParams.outputDimensions.y,
                conversionParams.outputFormat,
                false);

            m_Texture.LoadRawTextureData(buffer);
            m_Texture.Apply();

            rawImage.texture = m_Texture;

            // Done with your temporary data, so you can dispose it.
            buffer.Dispose();

        }

    }
}


