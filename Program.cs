using UIKit;
using SceneKit;
using ARKit;
using Foundation;
using System;
using CoreGraphics;
using System.Linq;
using OpenTK;
using Xamarin.Essentials;
using ARKit_Csharp;

namespace ARKitDemo
{
public class ARDelegate : ARSCNViewDelegate
{
        public event EventHandler<ImageDetection> ImageDetected;

	public override void DidAddNode(ISCNSceneRenderer renderer, SCNNode node, ARAnchor anchor)
	{
   
		if (anchor != null)
		{
                if (anchor is ARPlaneAnchor)
                    PlaceAnchorNode(node, anchor as ARPlaneAnchor);
                else if (anchor is ARImageAnchor)
                    PlaceMarkerCube(node, anchor as ARImageAnchor);
		}
	}

        private void PlaceMarkerCube(SCNNode node, ARImageAnchor aRImageAnchor)
        {
            ImageDetected?.Invoke(this, new ImageDetection
            {
                Anchor = aRImageAnchor,
            });

            //var plane = SCNPlane.Create(aRImageAnchor.ReferenceImage.PhysicalSize.Width, aRImageAnchor.ReferenceImage.PhysicalSize.Height);
            //var planeNode = SCNNode.FromGeometry(plane);
            //planeNode.Position = new SCNVector3(aRImageAnchor.Transform.Column3.X, aRImageAnchor.Transform.Column3.Y,aRImageAnchor.Transform.Column3.Z);
            //planeNode.Transform = SCNMatrix4.CreateRotationX((float)(Math.PI / 2.0));
            //node.AddChildNode(planeNode);

            ////Mark the anchor with a small red box
            //var box = new SCNBox { Height = 0.5f, Width = 0.5f, Length =0.5f };
            //box.FirstMaterial.Diffuse.ContentColor = UIColor.Green;
            //var anchorNode = new SCNNode { Position = new SCNVector3(0, 0, 0), Geometry = box };
            //planeNode.AddChildNode(anchorNode);
        }



        void PlaceAnchorNode(SCNNode node, ARPlaneAnchor anchor)
	{
		var plane = SCNPlane.Create(anchor.Extent.X, anchor.Extent.Z);
		plane.FirstMaterial.Diffuse.Contents = UIColor.LightGray;
		var planeNode = SCNNode.FromGeometry(plane);

		//Locate the plane at the position of the anchor
		planeNode.Position = new SCNVector3(anchor.Extent.X, 0.0f, anchor.Extent.Z);
		//Rotate it to lie flat
		planeNode.Transform = SCNMatrix4.CreateRotationX((float) (Math.PI / 2.0));
		node.AddChildNode(planeNode);

		//Mark the anchor with a small red box
		var box = new SCNBox { Height = 0.18f, Width = 0.18f, Length = 0.18f };
		box.FirstMaterial.Diffuse.ContentColor = UIColor.Red;
		var anchorNode = new SCNNode { Position = new SCNVector3(0, 0, 0), Geometry = box };
		planeNode.AddChildNode(anchorNode);
	}

	public override void DidUpdateNode(ISCNSceneRenderer renderer, SCNNode node, ARAnchor anchor)
	{

		if (anchor is ARPlaneAnchor)
		{
			var planeAnchor = anchor as ARPlaneAnchor;
			//BUG: Extent.Z should be at least a few dozen centimeters
			System.Console.WriteLine($"The (updated) extent of the anchor is [{planeAnchor.Extent.X}, {planeAnchor.Extent.Y}, {planeAnchor.Extent.Z}]");
		}
	}
}

	public class ARKitController : UIViewController
	{
		ARSCNView scnView; 

		public ARKitController() : base()
		{
			
		}

		public override bool ShouldAutorotate() => true;

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();

            var arDel = new ARDelegate();
            arDel.ImageDetected += ArDel_ImageDetected;

			scnView = new ARSCNView()
			{
				Frame = this.View.Frame,
				Delegate = arDel,
				DebugOptions = ARSCNDebugOptions.ShowFeaturePoints | ARSCNDebugOptions.ShowWorldOrigin,
				UserInteractionEnabled = true
			};

			this.View.AddSubview(scnView);
		}

        private void ArDel_ImageDetected(object sender, ImageDetection e)
        {
            Vibration.Vibrate();

            var v = new SCNVector3(
                e.Anchor.Transform.Column3.X,
                e.Anchor.Transform.Column3.Y,
                e.Anchor.Transform.Column3.Z);

            PlaceCube(v);

            scnView.Session.RemoveAnchor(e.Anchor);


        }

        public override void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);

            var images = GetDetectionImages();

            // Configure ARKit 
            var config = new ARWorldTrackingConfiguration();
			config.PlaneDetection = ARPlaneDetection.Horizontal;
            config.DetectionImages = images;

			// This method is called subsequent to `ViewDidLoad` so we know `scnView` is instantiated
			scnView.Session.Run(config, ARSessionRunOptions.RemoveExistingAnchors);
		}

        /// <summary>
        /// Images that the app will detect
        /// </summary>
        /// <returns></returns>
        private NSSet<ARReferenceImage> GetDetectionImages()
        {
            var i = UIImage.FromBundle("marker_image").CGImage;
            var r = new ARReferenceImage(i, ImageIO.CGImagePropertyOrientation.Up, 0.07f);
            var set = new NSSet<ARReferenceImage>(r);
            return set;
        }

        public override void TouchesBegan(NSSet touches, UIEvent evt)
		{
			base.TouchesBegan(touches, evt);
			var touch = touches.AnyObject as UITouch;
			if (touch != null)
			{
				var loc = touch.LocationInView(scnView);
				var worldPos = WorldPositionFromHitTest(loc);
				if (worldPos.Item1.HasValue)
				{
					PlaceCube(worldPos.Item1.Value);
				}
			}
		}

		private SCNVector3 PositionFromTransform(NMatrix4 xform)
		{
			return new SCNVector3(xform.M14, xform.M24, xform.M34);
		}

		Tuple<SCNVector3?, ARAnchor> WorldPositionFromHitTest (CGPoint pt)
		{
			//Hit test against existing anchors
			var hits = scnView.HitTest(pt, ARHitTestResultType.ExistingPlaneUsingExtent);
			if (hits != null && hits.Length > 0)
			{
				var anchors = hits.Where(r => r.Anchor is ARPlaneAnchor);
				if (anchors.Count() > 0)
				{
					var first = anchors.First();
					var pos = PositionFromTransform(first.WorldTransform);
					return new Tuple<SCNVector3?, ARAnchor>(pos, (ARPlaneAnchor)first.Anchor);
				}
			}
			return new Tuple<SCNVector3?, ARAnchor>(null, null);
		}

		private SCNMaterial[] LoadMaterials()
		{
			Func<string, SCNMaterial> LoadMaterial = fname =>
			{
				var mat = new SCNMaterial();
                mat.Diffuse.Contents = UIImage.FromFile(fname);
                //mat.Diffuse.ContentColor = UIColor.Green;
                mat.LocksAmbientWithDiffuse = true;
                mat.Transparency = 0.6f;
                mat.TransparencyMode = SCNTransparencyMode.SingleLayer;
				return mat;
			};

			var a = LoadMaterial("msft_logo.png");
			var b = LoadMaterial("xamagon.png");
			var c = LoadMaterial("fsharp.png"); // This demo was originally in F# :-) 

			return new[] { a, b, a, b, c, c };
		}

		SCNNode PlaceCube(SCNVector3 pos)
		{
			var box = new SCNBox { Width = 0.25f, Height = 0.25f, Length = 0.25f };
            
            var cubeNode = new SCNNode { Position = pos, Geometry = box };
            cubeNode.Geometry.Materials = LoadMaterials();
            scnView.Scene.RootNode.AddChildNode(cubeNode);
            return cubeNode;
        }
    }

    [Register ("AppDelegate")]
    public class AppDelegate : UIApplicationDelegate
    {
        UIWindow window;

        public override bool FinishedLaunching (UIApplication app, NSDictionary options)
        {
			window = new UIWindow (UIScreen.MainScreen.Bounds);
            window.RootViewController = new ARKitController();

            window.MakeKeyAndVisible ();

            return true;
        }
    }

    public class Application
    {
        static void Main (string [] args)
        {
            UIApplication.Main (args, null, "AppDelegate");
        }
    }
}


