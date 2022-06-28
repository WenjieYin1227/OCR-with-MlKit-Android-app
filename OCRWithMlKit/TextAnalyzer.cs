using System;
using System.IO;
using System.Threading.Tasks;
using Android.Gms.Tasks;
using Android.Graphics;
using Android.Media;
using Android.Util;
using AndroidX.Camera.Core;
using Java.Nio;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;

namespace OCRWithMlKit
{
    public class TextAnalyzer : Java.Lang.Object, ImageAnalysis.IAnalyzer
    {
        private const string TAG = "OCR Log: ";
        private readonly Action<string> TextListerner;

        public TextAnalyzer(Action<string> callback) //LumaListener listener)
        {
            this.TextListerner = callback;
        }

        public async void Analyze(IImageProxy image)
        {
            //var buffer = image.GetPlanes()[0].Buffer;
            //var data = ToByteArray(buffer);

            //var pixels = data.ToList();
            //pixels.ForEach(x => x = (byte)((int)x & 0xFF));
            //var luma = pixels.Average(x => x);
            ////Log.Debug(TAG, $"Average luminosity: {luma}");

            //

            var Rotation = image.ImageInfo.RotationDegrees;
            Log.Error("OCR Log", "Rotation : " + Rotation);
            var bitmap = yuv420ToBitmap(image.Image);


            Matrix matrix = new Matrix();
            matrix.PostRotate(Rotation);
            bitmap = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);

            string output = await ExtractText(bitmap);

            image.Close();
            TextListerner.Invoke(output);

        }

        public static Task<Java.Lang.Object> ToAwaitableTask(Android.Gms.Tasks.Task task)
        {
            var taskCompletionSource = new TaskCompletionSource<Java.Lang.Object>();
            var taskCompleteListener = new TaskCompleteListener(taskCompletionSource);
            task.AddOnCompleteListener(taskCompleteListener);

            return taskCompletionSource.Task;
        }

        public async Task<string> ExtractText(Bitmap bitmap)
        {
            //System.IO.Stream stream = new MemoryStream(data);
            //var bitmap = BitmapFactory.DecodeStream(stream);
            //var base64 = Convert.ToBase64String(data);
            var inputImage = InputImage.FromBitmap(bitmap, 0);

            var b = new TextRecognizerOptions.Builder();
            var opts = b.Build();
            var recognizer = TextRecognition.GetClient(opts);
            var t = (Text)await ToAwaitableTask(recognizer.Process(inputImage));

            var output = "";

            foreach (var item in t.TextBlocks)
            {
                output += item.Text + " \n";
            }

            return output;
        }

        Bitmap yuv420ToBitmap(Image image)
        {
            int imageWidth = image.Width;
            int imageHeight = image.Height;
            // sRGB array needed by Bitmap static factory method I use below.
            int[] argbArray = new int[imageWidth * imageHeight];
            ByteBuffer yBuffer = image.GetPlanes()[0].Buffer;
            yBuffer.Position(0);

            // This is specific to YUV420SP format where U & V planes are interleaved
            // so you can access them directly from one ByteBuffer. The data is saved as
            // UVUVUVUVU... for NV12 format and VUVUVUVUV... for NV21 format.
            //
            // The alternative way to handle this would be refer U & V as separate
            // `ByteBuffer`s and then use PixelStride and RowStride to find the right
            // index of the U or V value per pixel.
            ByteBuffer uvBuffer = image.GetPlanes()[1].Buffer;
            uvBuffer.Position(0);
            int r, g, b;
            int yValue, uValue, vValue;

            for (int y = 0; y < imageHeight - 2; y++)
            {
                for (int x = 0; x < imageWidth - 2; x++)
                {
                    int yIndex = y * imageWidth + x;
                    // Y plane should have positive values belonging to [0...255]
                    yValue = (yBuffer.Get(yIndex) & 0xff);

                    int uvx = x / 2;
                    int uvy = y / 2;
                    // Remember UV values are common for four pixel values.
                    // So the actual formula if U & V were in separate plane would be:
                    // `pos (for u or v) = (y / 2) * (width / 2) + (x / 2)`
                    // But since they are in single plane interleaved the position becomes:
                    // `u = 2 * pos`
                    // `v = 2 * pos + 1`, if the image is in NV12 format, else reverse.
                    int uIndex = uvy * imageWidth + 2 * uvx;
                    // ^ Note that here `uvy = y / 2` and `uvx = x / 2`
                    int vIndex = uIndex + 1;

                    uValue = (uvBuffer.Get(uIndex) & 0xff) - 128;
                    vValue = (uvBuffer.Get(vIndex) & 0xff) - 128;
                    r = (int)(yValue + 1.370705f * vValue);
                    g = (int)(yValue - (0.698001f * vValue) - (0.337633f * uValue));
                    b = (int)(yValue + 1.732446f * uValue);
                    r = clamp(r, 0, 255);
                    g = clamp(g, 0, 255);
                    b = clamp(b, 0, 255);
                    // Use 255 for alpha value, no transparency. ARGB values are
                    // positioned in each byte of a single 4 byte integer
                    // [AAAAAAAARRRRRRRRGGGGGGGGBBBBBBBB]
                    argbArray[yIndex] = (255 << 24) | (r & 255) << 16 | (g & 255) << 8 | (b & 255);
                }
            }

            return Bitmap.CreateBitmap(argbArray, imageWidth, imageHeight, Bitmap.Config.Argb8888);
        }

        public int clamp(int val, int min, int max)
        {
            return Math.Max(min, Math.Min(max, val));
        }

        class TaskCompleteListener : Java.Lang.Object, IOnCompleteListener
        {
            private readonly TaskCompletionSource<Java.Lang.Object> taskCompletionSource;

            public TaskCompleteListener(TaskCompletionSource<Java.Lang.Object> tcs)
            {
                this.taskCompletionSource = tcs;
            }

            public void OnComplete(Android.Gms.Tasks.Task task)
            {
                if (task.IsCanceled)
                {
                    this.taskCompletionSource.SetCanceled();
                }
                else if (task.IsSuccessful)
                {
                    this.taskCompletionSource.SetResult(task.Result);
                }
                else
                {
                    this.taskCompletionSource.SetException(task.Exception);
                }
            }
        }

    }
}
