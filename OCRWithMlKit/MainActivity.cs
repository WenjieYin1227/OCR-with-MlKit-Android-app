using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Snackbar;
using AndroidX.Camera.Core;
using Java.Util.Concurrent;
using AndroidX.Camera.View;
using Android;
using System.Linq;
using AndroidX.Core.Content;
using AndroidX.Core.App;
using Android.Widget;
using AndroidX.Camera.Lifecycle;
using Java.Lang;
using Android.Util;

namespace OCRWithMlKit
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {

        private const string TAG = "OCR Log: ";
        private const int REQUEST_CODE_PERMISSIONS = 10;

        ImageCapture imageCapture;
        IExecutorService cameraExecutor;

        PreviewView viewFinder;

        TextView OutputTextView;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            viewFinder = FindViewById<PreviewView>(Resource.Id.viewFinder);
            OutputTextView = FindViewById<TextView>(Resource.Id.textView1);

            // Request camera permissions   
            string[] permissions = new string[] { Manifest.Permission.Camera};
            if (permissions.FirstOrDefault(x => ContextCompat.CheckSelfPermission(this, x) != Android.Content.PM.Permission.Granted) != null) //   ContextCompat.CheckSelfPermission(this, Manifest.Permission.Camera) == Android.Content.PM.Permission.Granted)
                ActivityCompat.RequestPermissions(this, permissions, REQUEST_CODE_PERMISSIONS);
            else
                StartCamera();

            cameraExecutor = Executors.NewSingleThreadExecutor();

        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == REQUEST_CODE_PERMISSIONS)
            {
                if (permissions.FirstOrDefault(x => ContextCompat.CheckSelfPermission(this, x) != Android.Content.PM.Permission.Granted) == null)
                {
                    StartCamera();
                }
                else
                {
                    Toast.MakeText(this, "Permissions not granted by the user.", ToastLength.Short).Show();
                    this.Finish();
                    return;
                }
            }
        }

        private void StartCamera()
        {
            var cameraProviderFuture = ProcessCameraProvider.GetInstance(this);

            cameraProviderFuture.AddListener(new Runnable(() =>
            {
                // Used to bind the lifecycle of cameras to the lifecycle owner
                var cameraProvider = (ProcessCameraProvider)cameraProviderFuture.Get();

                // Preview
                var preview = new Preview.Builder().Build();
                preview.SetSurfaceProvider(viewFinder.CreateSurfaceProvider());

                // Take Photo
                this.imageCapture = new ImageCapture.Builder().Build();

                // Frame by frame analyze
                var imageAnalyzer = new ImageAnalysis.Builder().Build();
                imageAnalyzer.SetAnalyzer(cameraExecutor, new TextAnalyzer(SetNewText));

                // Select back camera as a default, or front camera otherwise
                CameraSelector cameraSelector = null;
                if (cameraProvider.HasCamera(CameraSelector.DefaultBackCamera) == true)
                    cameraSelector = CameraSelector.DefaultBackCamera;
                else if (cameraProvider.HasCamera(CameraSelector.DefaultFrontCamera) == true)
                    cameraSelector = CameraSelector.DefaultFrontCamera;
                else
                    throw new System.Exception("Camera not found");

                try
                {
                    // Unbind use cases before rebinding
                    cameraProvider.UnbindAll();

                    // Bind use cases to camera
                    cameraProvider.BindToLifecycle(this, cameraSelector, preview, imageCapture, imageAnalyzer);
                }
                catch (Java.Lang.Exception exc)
                {
                    Log.Debug(TAG, "Use case binding failed", exc);
                    Toast.MakeText(this, $"Use case binding failed: {exc.Message}", ToastLength.Short).Show();
                }

            }), ContextCompat.GetMainExecutor(this)); //GetMainExecutor: returns an Executor that runs on the main thread.
        }

        void SetNewText(string text)
        {
            RunOnUiThread(() =>
            {
                if (!string.IsNullOrEmpty(text))
                    OutputTextView.Text = text;
                else
                    OutputTextView.Text = "No Text Found";
            });
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            cameraExecutor.Shutdown();
        }
    }
}
