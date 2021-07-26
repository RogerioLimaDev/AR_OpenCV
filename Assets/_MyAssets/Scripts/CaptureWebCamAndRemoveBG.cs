#if !(PLATFORM_LUMIN && !UNITY_EDITOR)
#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnityExample;
using UnityEngine;
using OpenCVForUnity.VideoModule;
using OpenCVForUnity.BgsegmModule;
using OpenCVForUnity.ImgprocModule;
using System.Collections.Generic;

namespace Com.RogerioLima.OpenCV
{
    [RequireComponent(typeof(WebCamTextureToMatHelper))]
    public class CaptureWebCamAndRemoveBG : MonoBehaviour
    {
     #region fields

        /// The background subtractor algorithm.
        
        // /// The requested resolution.
        public ResolutionPreset requestedResolution = ResolutionPreset._640x480;

        // /// The requestedFPS.
        public FPSPreset requestedFPS = FPSPreset._30;

        /// The texture.
        public static Texture2D texture;

        /// The webcam texture to mat helper.
        WebCamTextureToMatHelper webCamTextureToMatHelper;

        /// The FPS monitor.
        FpsMonitor fpsMonitor;
        Mat webCamTextureMat;

        Mat fgmaskMat;
        public BackgroundSubtractorAlgorithmPreset backgroundSubtractorAlgorithm = BackgroundSubtractorAlgorithmPreset.KNN;
        BackgroundSubtractor backgroundSubstractor;
        Mat kernel;
        System.Diagnostics.Stopwatch watch;
    #endregion

        // Use this for initialization
        void Start()
        {
            fpsMonitor = GetComponent<FpsMonitor>();
           
            webCamTextureToMatHelper = gameObject.GetComponent<WebCamTextureToMatHelper>();
            webCamTextureToMatHelper.outputColorFormat = WebCamTextureToMatHelper.ColorFormat.RGB;
            int width, height;
            Dimensions(requestedResolution, out width, out height);
            webCamTextureToMatHelper.requestedWidth = width;
            webCamTextureToMatHelper.requestedHeight = height;
            webCamTextureToMatHelper.requestedFPS = (int)requestedFPS;
            webCamTextureToMatHelper.Initialize();
            CreateBackgroundSubstractor();
            kernel = Imgproc.getStructuringElement(Imgproc.MORPH_ELLIPSE, new Size(3, 3));
            watch = new System.Diagnostics.Stopwatch();
        }


        /// Raises the webcam texture to mat helper initialized event.
        public void OnWebCamTextureToMatHelperInitialized()
        {
            Debug.Log("OnWebCamTextureToMatHelperInitialized");
            ConvertWebCamMatToTexture2D();
            ApplyTexture2DToRendererAndRedimensioneIt();
            SetupOrthographicCanera();
            CreateForegroundMat();
        }

        void ConvertWebCamMatToTexture2D()
        {
            webCamTextureMat = webCamTextureToMatHelper.GetMat();
            texture = new Texture2D(webCamTextureMat.cols(), webCamTextureMat.rows(), TextureFormat.RGB24, false);
        }

        void ApplyTexture2DToRendererAndRedimensioneIt()
        {
            gameObject.GetComponent<Renderer>().material.mainTexture = texture;
            gameObject.transform.localScale = new Vector3(webCamTextureMat.cols(), webCamTextureMat.rows(), 1);
            Debug.Log("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);
        }

        void SetupOrthographicCanera()
        {
            float width = webCamTextureMat.width();
            float height = webCamTextureMat.height();

            float widthScale = (float)Screen.width / width;
            float heightScale = (float)Screen.height / height;
            if (widthScale < heightScale)
            {
                Camera.main.orthographicSize = (width * (float)Screen.height / (float)Screen.width) / 2;
            }
            else
            {
                Camera.main.orthographicSize = height / 2;
            }
        }

        void CreateForegroundMat()
        {
            fgmaskMat = new Mat(webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC1);
        }


        /// Raises the webcam texture to mat helper disposed event.
        public void OnWebCamTextureToMatHelperDisposed()
        {
            Debug.Log("OnWebCamTextureToMatHelperDisposed");
            
            if (backgroundSubstractor != null)
                backgroundSubstractor.clear();

            if (fgmaskMat != null)
                fgmaskMat.Dispose();

            if (texture != null)
            {
                Texture2D.Destroy(texture);
                texture = null;
            }
        }


        /// Raises the webcam texture to mat helper error occurred event.
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode)
        {
            Debug.Log("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);

            if (fpsMonitor != null)
            {
                fpsMonitor.consoleText = "ErrorCode: " + errorCode;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
            {
                Mat rgbMat = webCamTextureToMatHelper.GetMat();
                watch.Reset();
                watch.Start();

                //ExtractBackground(rgbMat);
                ApplyDrawContours(rgbMat);
                
                //ProcessImage(rgbMat,true,false,false);

                Utils.fastMatToTexture2D(rgbMat, texture);

                if (fpsMonitor != null)
                {
                    fpsMonitor.Add("time: ", watch.ElapsedMilliseconds + " ms");
                }
            }        
        }

        void ProcessImage(Mat rgbMat, bool floodFill, bool findContours, bool invert)
        {
            //Cria uma matrix vazia para efetuar o processamento
            Mat greyMat = new Mat( webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC3);
            //Copia a matrix da camera para a matrix recem criada
            rgbMat.copyTo(greyMat);

            
            //Converte para Greyscale para efeito de processamento
            Imgproc.cvtColor(greyMat,greyMat,Imgproc.COLOR_RGB2GRAY);
            ApplyFloodFill(greyMat,findContours,invert);

            watch.Stop();
            //Converte a imagem para RGB depois de processada
            Imgproc.cvtColor(greyMat, greyMat, Imgproc.COLOR_GRAY2RGB);

            //Copia a Matrix criada para a Matrix original
            greyMat.copyTo(rgbMat);
            
        }

        void ApplyFindContours(Mat greyMat)
        {
            Mat im_findContours = greyMat.clone();
            //Aplica um gaussiam blur antes de extrair o background
            Imgproc.GaussianBlur (im_findContours,im_findContours,new Size(5,5),0);
            //Extrai os contornos da imagem e salva em uma lista os contornos encontrados
            Imgproc.Canny(im_findContours,im_findContours,150,150);
            //Cria lista para salvar contornos encontrados
            List<MatOfPoint> contours = new List<MatOfPoint>();
            Imgproc.findContours(im_findContours,contours,new Mat(),Imgproc.RETR_TREE, Imgproc.CHAIN_APPROX_SIMPLE);
            im_findContours.copyTo(greyMat);
        }

        void ApplyDrawContours(Mat greyMat)
        {
            Imgproc.cvtColor(greyMat,greyMat,Imgproc.COLOR_RGB2GRAY);
            ApplyThreshold(greyMat);
            Mat im_drawContours = greyMat.clone();
            Imgproc.GaussianBlur(im_drawContours,im_drawContours,new Size(8,8),0);
            List<MatOfPoint> contours = new List<MatOfPoint>();
            Imgproc.findContours(im_drawContours,contours,new Mat(),Imgproc.RETR_TREE, Imgproc.CHAIN_APPROX_SIMPLE);
            Imgproc.cvtColor(im_drawContours, im_drawContours, Imgproc.COLOR_GRAY2RGB);
            Imgproc.drawContours(im_drawContours,contours,-1,new Scalar(255,255,255),Imgproc.FILLED, 16, new Mat(), 2);
            im_drawContours.copyTo(greyMat);
        }

        void ApplyThreshold(Mat greyMat)
        {        
            Mat im_Threshold = greyMat.clone();
            //Aplica um gaussiam blur e em seguida um Trheshold
            Imgproc.GaussianBlur(im_Threshold,im_Threshold,new Size(5,5),0);
            Imgproc.threshold(im_Threshold,im_Threshold,220,255,Imgproc.THRESH_OTSU);
            Mat im_threshold_inv = im_Threshold.clone();
            //Inverte o Threshold e copia para a Matrix original
            Core.bitwise_not(im_Threshold,im_threshold_inv);
            im_threshold_inv.copyTo(greyMat); 
        }

        void ApplyFloodFill(Mat greyMat,bool findContours, bool invert)
        {
            ApplyThreshold(greyMat);

            if(findContours)
            {
                ApplyFindContours(greyMat);
            }

            Mat im_floodFill = greyMat.clone();
            Imgproc.GaussianBlur(im_floodFill,im_floodFill,new Size(5,5),0);
            Imgproc.floodFill(im_floodFill,im_floodFill, new Point(0,0), new Scalar(0));

            //A Matrix já chegou invertida, inverte para o original?
            if(invert)
            {
                Mat im_floodfillInvert = im_floodFill.clone();
                Core.bitwise_not(im_floodFill,im_floodfillInvert);
                im_floodfillInvert.copyTo(greyMat);
            }
            else
            im_floodFill.copyTo(greyMat);
        }

        void ExtractBackground(Mat rgbMat)
        {
            //Cria uma matrix vazia para efetuar o processamento
            Mat greyMat = new Mat( webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC3);
            //Copia a matrix da camera para a matrix recem criada
            rgbMat.copyTo(greyMat);

            //Extrai o background
            backgroundSubstractor.apply(greyMat, fgmaskMat);
            Imgproc.morphologyEx(fgmaskMat, fgmaskMat, Imgproc.MORPH_OPEN, kernel);
            //O greymat é a máscara
            backgroundSubstractor.getBackgroundImage(greyMat);
            //O fgmaskNat é o fundo sem foreground
           //backgroundSubstractor.getBackgroundImage(fgmaskMat);
            Imgproc.cvtColor(fgmaskMat,greyMat,Imgproc.COLOR_GRAY2RGB);
            greyMat.copyTo(rgbMat);
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy()
        {
            // if (backgroundSubstractor != null)
            //     backgroundSubstractor.Dispose();

            if (webCamTextureToMatHelper != null)    
                webCamTextureToMatHelper.Dispose();
        }

        public enum FPSPreset : int
        {
            _0 = 0,
            _1 = 1,
            _5 = 5,
            _10 = 10,
            _15 = 15,
            _30 = 30,
            _60 = 60,
        }

        public enum ResolutionPreset : byte
        {
            _50x50 = 0,
            _640x480,
            _1280x720,
            _1920x1080,
            _9999x9999,
        }

        private void Dimensions(ResolutionPreset preset, out int width, out int height)
        {
            switch (preset)
            {
                case ResolutionPreset._50x50:
                    width = 50;
                    height = 50;
                    break;
                case ResolutionPreset._640x480:
                    width = 640;
                    height = 480;
                    break;
                case ResolutionPreset._1280x720:
                    width = 1280;
                    height = 720;
                    break;
                case ResolutionPreset._1920x1080:
                    width = 1920;
                    height = 1080;
                    break;
                case ResolutionPreset._9999x9999:
                    width = 9999;
                    height = 9999;
                    break;
                default:
                    width = height = 0;
                    break;
            }
        }


        public enum BackgroundSubtractorAlgorithmPreset : byte
        {
            KNN = 0,
            MOG2,
            CNT,
            GMG,
            GSOC,
            LSBP,
            MOG,
        }
 
        protected void CreateBackgroundSubstractor()
        {
            //Caso já exista uma segmentação, descarta a existente
            if (backgroundSubstractor != null)
            {
                backgroundSubstractor.Dispose();
            }

            //// ALGORITMOS DE EXTRAÇÃO DE BACKGROUND DIFERENTES  ////

            //backgroundSubstractor = Video.createBackgroundSubtractorKNN();
            backgroundSubstractor = Video.createBackgroundSubtractorMOG2();
            //backgroundSubstractor = Bgsegm.createBackgroundSubtractorCNT();
            //backgroundSubstractor = Bgsegm.createBackgroundSubtractorMOG();
            //Algoritmo mais lentos abaixo
            //backgroundSubstractor = Bgsegm.createBackgroundSubtractorGMG();
            //backgroundSubstractor = Bgsegm.createBackgroundSubtractorLSBP();
            //backgroundSubstractor = Bgsegm.createBackgroundSubtractorGSOC();
            Debug.Log("<color=green><b>BACKGROUND SUBTRACTOR CREATED </b></color>");
        }

    }

}
#endif
#endif

