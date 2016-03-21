
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Hardware;
using ZXing.Mobile;
using System.Threading;
using Android.Content.PM;
using System.Threading.Tasks;

namespace Sample.Android
{
	[Activity (Label = "JohanScanActivity", ScreenOrientation = ScreenOrientation.Landscape)]			
	public class JohanScanActivity : Activity, ISurfaceHolderCallback, Camera.IPreviewCallback
	{
		SurfaceView surfaceView;
		Camera camera;
		Task processingTask = null;
		ZXing.BarcodeReader barcodeReader = null;
		bool completed = false;

		private void Complete(string result)
		{
			if (completed)
				return;
			completed = true;
			RunOnUiThread (() => {
				Intent intent = new Intent();
				intent.PutExtra("ScanResult", result);
				SetResult(Result.Ok, intent);
				this.Finish();
			});
		}

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			RequestWindowFeature (WindowFeatures.NoTitle);
			SetContentView (Resource.Layout.johan_scan_layout);

			surfaceView = new SurfaceView (this);
			surfaceView.Holder.AddCallback (this);
			surfaceView.Holder.SetType (SurfaceType.PushBuffers);
			FindViewById<LinearLayout> (Resource.Id.layout1).AddView (surfaceView, getChildLayoutParams());
		}

		private LinearLayout.LayoutParams getChildLayoutParams()
		{
			var layoutParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
			layoutParams.Weight = 1;
			return layoutParams;
		}

		public void SurfaceChanged (ISurfaceHolder holder, global::Android.Graphics.Format format, int width, int height)
		{
			Console.WriteLine ("Surface changed");
			if (camera == null)
				return;

			camera.SetPreviewCallback (this);
			camera.SetPreviewDisplay (surfaceView.Holder);
			camera.StartPreview ();
		}

		public void SurfaceCreated (ISurfaceHolder holder)
		{
			Console.WriteLine ("Surface created");
			camera = Camera.Open ();
		}

		public void SurfaceDestroyed (ISurfaceHolder holder)
		{
			Console.WriteLine ("Surface destroyed");
			if (camera != null) {
				camera.SetPreviewCallback (null);
				camera.Release ();
				camera = null;
			}
		}

		public void OnPreviewFrame (byte[] bytes, Camera camera)
		{
			if (completed)
				return;
			
			//Check and see if we're still processing a previous frame
			if (processingTask != null && !processingTask.IsCompleted)
				return;

			var cameraParameters = camera.GetParameters ();
			var width = cameraParameters.PreviewSize.Width;
			var height = cameraParameters.PreviewSize.Height;
			//var img = new YuvImage(bytes, ImageFormatType.Nv21, cameraParameters.PreviewSize.Width, cameraParameters.PreviewSize.Height, null);	

			processingTask = Task.Factory.StartNew (() => {
				try {

					Task.Delay(500).Wait();

					if (barcodeReader == null) {
						barcodeReader = new ZXing.BarcodeReader (null, null, null, (p, w, h, f) => 
							new ZXing.PlanarYUVLuminanceSource (p, w, h, 0, 0, w, h, false));

						barcodeReader.Options.TryHarder = false;
						barcodeReader.Options.PureBarcode = false;
						//TODO: //barcodeReader.Options.CharacterSet
						barcodeReader.TryInverted = false;
						barcodeReader.Options.PossibleFormats = new List<ZXing.BarcodeFormat> ();
						barcodeReader.Options.PossibleFormats.Add(ZXing.BarcodeFormat.CODE_128);
					}

					bool rotate = false;
					int newWidth = width;
					int newHeight = height;

					var cDegrees = getCameraDisplayOrientation (this);

					if (cDegrees == 90 || cDegrees == 270) {
						rotate = true;
						newWidth = height;
						newHeight = width;
					}

					var start = PerformanceCounter.Start ();

					if (rotate)
						bytes = rotateCounterClockwise (bytes, width, height);

					var result = barcodeReader.Decode (bytes, newWidth, newHeight, ZXing.RGBLuminanceSource.BitmapFormat.Unknown);

					PerformanceCounter.Stop (start, "Decode Time: {0} ms (width: " + width + ", height: " + height + ", degrees: " + cDegrees + ", rotate: " + rotate + ")");

					if (result == null || string.IsNullOrEmpty (result.Text))
						return;

					//SetResult(Result.Ok, 
					//Android.Util.Log.Debug (MobileBarcodeScanner.TAG, "Barcode Found: " + result.Text);
					Complete(result.Text);

				} catch (ZXing.ReaderException) {
					//Android.Util.Log.Debug (MobileBarcodeScanner.TAG, "No barcode Found");
					// ignore this exception; it happens every time there is a failed scan
				} catch (Exception) {
					// TODO: this one is unexpected.. log or otherwise handle it
					Complete(null);
				}

			});
		}

		int getCameraDisplayOrientation (Activity context)
		{
			var degrees = 0;

			var display = context.WindowManager.DefaultDisplay;

			var rotation = display.Rotation;

			switch (rotation) {
			case SurfaceOrientation.Rotation0:
				degrees = 0;
				break;
			case SurfaceOrientation.Rotation90:
				degrees = 90;
				break;
			case SurfaceOrientation.Rotation180:
				degrees = 180;
				break;
			case SurfaceOrientation.Rotation270:
				degrees = 270;
				break;
			}


			Camera.CameraInfo info = new Camera.CameraInfo ();

			//TODO: correct id.
			int cameraId = 0;
			Camera.GetCameraInfo (cameraId, info);

			int correctedDegrees = (360 + info.Orientation - degrees) % 360;

			return correctedDegrees;
		}

		public byte[] rotateCounterClockwise (byte[] data, int width, int height)
		{
			var rotatedData = new byte[data.Length];
			for (int y = 0; y < height; y++) {
				for (int x = 0; x < width; x++)
					rotatedData [x * height + height - y - 1] = data [x + y * width];
			}
			return rotatedData;
		}
	}


}

