﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Microsoft.Samples.Kinect.SwipeGestureRecognizer;
using Coding4Fun.Kinect.Wpf;
using System.Windows.Controls;

namespace Microsoft.Samples.Kinect.Slideshow
{


    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// The recognizer being used.
        /// </summary>
        private readonly Recognizer activeRecognizer;

        /// <summary>
        /// The paths of the picture files.
        /// </summary>
        private readonly string[] picturePaths = CreatePicturePaths();

        /// <summary>
        /// Array of arrays of contiguous line segements that represent a skeleton.
        /// </summary>
        private static readonly JointType[][] SkeletonSegmentRuns = new JointType[][]
        {
            new JointType[] 
            { 
                JointType.Head, JointType.ShoulderCenter, JointType.HipCenter 
            },
            new JointType[] 
            { 
                JointType.HandLeft, JointType.WristLeft, JointType.ElbowLeft, JointType.ShoulderLeft,
                JointType.ShoulderCenter,
                JointType.ShoulderRight, JointType.ElbowRight, JointType.WristRight, JointType.HandRight
            },
            new JointType[]
            {
                JointType.FootLeft, JointType.AnkleLeft, JointType.KneeLeft, JointType.HipLeft,
                JointType.HipCenter,
                JointType.HipRight, JointType.KneeRight, JointType.AnkleRight, JointType.FootRight
            }
        };

        /// <summary>
        /// The sensors used
        /// </summary>
        private KinectSensor frontSensor, rearSensor;

        /// <summary>
        /// There is currently no connected sensor.
        /// </summary>
        private bool isDisconnectedField = true;

        /// <summary>
        /// Any message associated with a failure to connect.
        /// </summary>
        private string disconnectedReasonField;

        /// <summary>
        /// Array to receive skeletons from sensor, resize when needed.
        /// </summary>
        private Skeleton[] skeletons = new Skeleton[0];

        /// <summary>
        /// Time until skeleton ceases to be highlighted.
        /// </summary>
        private DateTime highlightTime = DateTime.MinValue;

        /// <summary>
        /// The ID of the skeleton to highlight.
        /// </summary>
        private int highlightId = -1;

        /// <summary>
        /// The ID if the skeleton to be tracked.
        /// </summary>
        private int nearestId = -1;

        /// <summary>
        /// The index of the current image.
        /// </summary>
        private int indexField = 1;

        /// <summary>
        /// Kinect Device ID
        /// </summary>
        private const string FrontSideKinectID = @"USB\VID_045E&PID_02C2\5&464F35A&0&2";
        private const string RearSideKinectID  = @"USB\VID_0409&PID_005A\5&464F35A&0&1";

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow" /> class.
        /// </summary>
        public MainWindow()
        {
            this.PreviousPicture = this.LoadPicture(this.Index - 1);
            this.Picture = this.LoadPicture(this.Index);
            this.NextPicture = this.LoadPicture(this.Index + 1);

            InitializeComponent();

            // Create the gesture recognizer.
            this.activeRecognizer = this.CreateRecognizer();

            // Wire-up window loaded event.
            Loaded += this.OnMainWindowLoaded;
        }

        /// <summary>
        /// Event implementing INotifyPropertyChanged interface.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets a value indicating whether no Kinect is currently connected.
        /// </summary>
        public bool IsDisconnected
        {
            get
            {
                return this.isDisconnectedField;
            }

            private set
            {
                if (this.isDisconnectedField != value)
                {
                    this.isDisconnectedField = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("IsDisconnected"));
                    }
                }
            }
        }

        /// <summary>
        /// Gets any message associated with a failure to connect.
        /// </summary>
        public string DisconnectedReason
        {
            get
            {
                return this.disconnectedReasonField;
            }

            private set
            {
                if (this.disconnectedReasonField != value)
                {
                    this.disconnectedReasonField = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("DisconnectedReason"));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the index number of the image to be shown.
        /// </summary>
        public int Index
        {
            get
            {
                return this.indexField;
            }

            set
            {
                if (this.indexField != value)
                {
                    this.indexField = value;

                    // Notify world of change to Index and Picture.
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("Index"));
                    }
                }
            }
        }

        /// <summary>
        /// Gets the previous image displayed.
        /// </summary>
        public BitmapImage PreviousPicture { get; private set; }

        /// <summary>
        /// Gets the current image to be displayed.
        /// </summary>
        public BitmapImage Picture { get; private set; }

        /// <summary>
        /// Gets teh next image displayed.
        /// </summary>
        public BitmapImage NextPicture { get; private set; }

        /// <summary>
        /// Get list of files to display as pictures.
        /// </summary>
        /// <returns>Paths to pictures.</returns>
        private static string[] CreatePicturePaths()
        {
            var list = new List<string>();

            var commonPicturesPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\SlideShowImages\";
            list.AddRange(Directory.GetFiles(commonPicturesPath, "*.jpg", SearchOption.AllDirectories));
            if (list.Count == 0)
            {
                list.AddRange(Directory.GetFiles(commonPicturesPath, "*.png", SearchOption.AllDirectories));
            }

            if (list.Count == 0)
            {
                var myPicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                list.AddRange(Directory.GetFiles(myPicturesPath, "*.jpg", SearchOption.AllDirectories));
                if (list.Count == 0)
                {
                    list.AddRange(Directory.GetFiles(commonPicturesPath, "*.png", SearchOption.AllDirectories));
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// Load the picture with the given index.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <returns>Corresponding image.</returns>
        private BitmapImage LoadPicture(int index)
        {
            BitmapImage value;

            if (this.picturePaths.Length != 0)
            {
                var actualIndex = index % this.picturePaths.Length;
                if (actualIndex < 0)
                {
                    actualIndex += this.picturePaths.Length;
                }

                Debug.Assert(0 <= actualIndex, "Index used will be non-negative");
                Debug.Assert(actualIndex < this.picturePaths.Length, "Index is within bounds of path array");

                try
                {
                    value = new BitmapImage(new Uri(this.picturePaths[actualIndex]));
                }
                catch (NotSupportedException)
                {
                    value = null;
                }
            }
            else
            {
                value = null;
            }

            return value;
        }

        /// <summary>
        /// Create a wired-up recognizer for running the slideshow.
        /// </summary>
        /// <returns>The wired-up recognizer.</returns>
        private Recognizer CreateRecognizer()
        {
            // Instantiate a recognizer.
            var recognizer = new Recognizer();

            // Wire-up swipe right to manually advance picture.
            recognizer.SwipeRightDetected += (s, e) =>
            {
                if (e.Skeleton.TrackingId == nearestId)
                {
                    Index++;

                    // Setup corresponding picture if pictures are available.
                    this.PreviousPicture = this.Picture;
                    this.Picture = this.NextPicture;
                    this.NextPicture = LoadPicture(Index + 1);

                    // Notify world of change to Index and Picture.
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("PreviousPicture"));
                        this.PropertyChanged(this, new PropertyChangedEventArgs("Picture"));
                        this.PropertyChanged(this, new PropertyChangedEventArgs("NextPicture"));
                    }

                    var storyboard = Resources["LeftAnimate"] as Storyboard;
                    if (storyboard != null)
                    {
                        storyboard.Begin();
                    }

                    HighlightSkeleton(e.Skeleton);
                }
            };

            // Wire-up swipe left to manually reverse picture.
            recognizer.SwipeLeftDetected += (s, e) =>
            {
                if (e.Skeleton.TrackingId == nearestId)
                {
                    Index--;

                    // Setup corresponding picture if pictures are available.
                    this.NextPicture = this.Picture;
                    this.Picture = this.PreviousPicture;
                    this.PreviousPicture = LoadPicture(Index - 1);

                    // Notify world of change to Index and Picture.
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("PreviousPicture"));
                        this.PropertyChanged(this, new PropertyChangedEventArgs("Picture"));
                        this.PropertyChanged(this, new PropertyChangedEventArgs("NextPicture"));
                    }

                    var storyboard = Resources["RightAnimate"] as Storyboard;
                    if (storyboard != null)
                    {
                        storyboard.Begin();
                    }

                    HighlightSkeleton(e.Skeleton);
                }
            };

            return recognizer;
        }

        /// <summary>
        /// Handle insertion of Kinect sensor.
        /// </summary>
        private void InitializeNui()
        {
            this.UninitializeNui();

            this.isDisconnectedField = false;
            var index = 0;
            while ( (this.frontSensor == null || this.rearSensor == null) && 
                     index < KinectSensor.KinectSensors.Count              )
            {

                try
                {
                    // Initialize the front camera
                    if (KinectSensor.KinectSensors[index].DeviceConnectionId == FrontSideKinectID)
                    {
                        this.frontSensor = KinectSensor.KinectSensors[index];
                        this.frontSensor.Start();
                    }
                    
                    // Initialize the rear camera
                    if (KinectSensor.KinectSensors[index].DeviceConnectionId == RearSideKinectID)
                    {
                        this.rearSensor = KinectSensor.KinectSensors[index];
                        this.rearSensor.Start();
                    }

                } 
                catch (IOException ex)
                {
                    this.frontSensor = null;
                    this.rearSensor = null;

                    this.isDisconnectedField = true;

                    this.DisconnectedReason = ex.Message;
                }
                catch (InvalidOperationException ex)
                {
                    this.frontSensor = null;
                    this.rearSensor = null;

                    this.isDisconnectedField = true;

                    this.DisconnectedReason = ex.Message;
                }


                index++;
            }

            if (this.rearSensor != null && this.frontSensor != null)
            {
                ConnectedFeedback2.Visibility = System.Windows.Visibility.Hidden;

                this.frontSensor.SkeletonStream.Enable();
                this.frontSensor.SkeletonFrameReady += this.OnSkeletonFrameReady;

                this.rearSensor.ColorStream.Enable();
                this.rearSensor.ColorFrameReady += this.OnColorFrameReady;
            }


        }

        /// <summary>
        /// Handle removal of Kinect sensor.
        /// </summary>
        private void UninitializeNui()
        {
            if (this.frontSensor != null)
            {
                this.frontSensor.SkeletonFrameReady -= this.OnSkeletonFrameReady;

                this.frontSensor.Stop();

                this.frontSensor = null;
            }

            if (this.rearSensor != null)
            {
                this.rearSensor.SkeletonFrameReady -= this.OnSkeletonFrameReady;

                this.rearSensor.Stop();

                this.rearSensor = null;
            }

            this.IsDisconnected = true;
            this.DisconnectedReason = null;
        }

        /// <summary>
        /// Window loaded actions to initialize Kinect handling.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Start the Kinect system, this will cause StatusChanged events to be queued.
            this.InitializeNui();
        }


        /// <summary>
        /// Handler for color ready handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void OnColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame == null)
                {
                    return;
                }

                byte[] pixels = new byte[colorFrame.PixelDataLength];
                colorFrame.CopyPixelDataTo(pixels);

                int stride = colorFrame.Width * 4;
                colorStreamImage.Source =
                    BitmapSource.Create(colorFrame.Width, colorFrame.Height,
                    96, 96, PixelFormats.Bgr32, null, pixels, stride);

            }
        }

        /// <summary>
        /// Handler for skeleton ready handler.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void OnSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            // Get the frame.
            using (var frame = e.OpenSkeletonFrame())
            {
                // Ensure we have a frame.
                if (frame != null)
                {
                    // Resize the skeletons array if a new size (normally only on first call).
                    if (this.skeletons.Length != frame.SkeletonArrayLength)
                    {
                        this.skeletons = new Skeleton[frame.SkeletonArrayLength];
                    }

                    // Get the skeletons.
                    frame.CopySkeletonDataTo(this.skeletons);

                    // Assume no nearest skeleton and that the nearest skeleton is a long way away.
                    var newNearestId = -1;
                    var nearestDistance2 = double.MaxValue;

                    bool __skeletonPresent = false;

                    // Look through the skeletons.
                    foreach (var skeleton in this.skeletons)
                    {
                        // Only consider tracked skeletons.
                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            __skeletonPresent = true;
                            // Find the distance squared.
                            var distance2 = (skeleton.Position.X * skeleton.Position.X) +
                                (skeleton.Position.Y * skeleton.Position.Y) +
                                (skeleton.Position.Z * skeleton.Position.Z);

                            // Is the new distance squared closer than the nearest so far?
                            if (distance2 < nearestDistance2)
                            {
                                // Use the new values.
                                newNearestId = skeleton.TrackingId;
                                nearestDistance2 = distance2;
                            }
                        }
                    }

                    //Toggle between SlideShow and Video Stream
                    ToggleViews(__skeletonPresent);
                    
                    //Reset SlideShow if Skeleton is lost
                    if (__skeletonPresent == false)
                    {
                        Index = 1;

                        // Setup corresponding picture if pictures are available.
                        this.PreviousPicture = LoadPicture(Index - 1);
                        this.Picture = LoadPicture(Index);
                        this.NextPicture = LoadPicture(Index + 1);

                        // Notify world of change to Index and Picture.
                        if (this.PropertyChanged != null)
                        {
                            this.PropertyChanged(this, new PropertyChangedEventArgs("PreviousPicture"));
                            this.PropertyChanged(this, new PropertyChangedEventArgs("Picture"));
                            this.PropertyChanged(this, new PropertyChangedEventArgs("NextPicture"));
                        }
                    }

                    if (this.nearestId != newNearestId)
                    {
                        this.nearestId = newNearestId;
                    }

                    // Pass skeletons to recognizer.
                    this.activeRecognizer.Recognize(sender, frame, this.skeletons);

                    this.DrawStickMen(this.skeletons);

                    //Draw hands
                    foreach (var skeleton in this.skeletons)
                    {
                        // Only consider tracked skeletons.
                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked && 
                            skeleton.TrackingId == this.nearestId) {
                                var scaledJointLeft = skeleton.Joints[JointType.HandLeft].ScaleTo(900, 540,.8f, .8f);
                                var scaledJointRight = skeleton.Joints[JointType.HandRight].ScaleTo(900, 540, .8f, .8f);


                                SetEllipsePosition(leftEllipse, scaledJointLeft);
                                SetEllipsePosition(rightEllipse, scaledJointRight);
                        }
                    }

                }
            }
        }

        /// <summary>
        /// Move the Hand
        /// </summary>
        /// <param name="ellipse"></param>
        /// <param name="hoverJoint"></param>
        private void SetEllipsePosition(FrameworkElement ellipse, Joint hoverJoint)
        {
            Canvas.SetLeft(ellipse, hoverJoint.Position.X);
            Canvas.SetTop(ellipse, hoverJoint.Position.Y);
        }


        /// <summary>
        /// Toggle Between SlideShow and video stream
        /// </summary>
        /// <param name="skeletonPresent"></param>
        private void ToggleViews(bool skeletonPresent)
        {
            if (skeletonPresent)
            {
                next.Visibility = System.Windows.Visibility.Visible;
                current.Visibility = System.Windows.Visibility.Visible;
                previous.Visibility = System.Windows.Visibility.Visible;

                colorStreamImage.Visibility = System.Windows.Visibility.Hidden;

                rightEllipse.Visibility = System.Windows.Visibility.Visible;
                leftEllipse.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                next.Visibility = System.Windows.Visibility.Hidden;
                current.Visibility = System.Windows.Visibility.Hidden;
                previous.Visibility = System.Windows.Visibility.Hidden;

                colorStreamImage.Visibility = System.Windows.Visibility.Visible;

                rightEllipse.Visibility = System.Windows.Visibility.Hidden;
                leftEllipse.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        /// <summary>
        /// Select a skeleton to be highlighted.
        /// </summary>
        /// <param name="skeleton">The skeleton</param>
        private void HighlightSkeleton(Skeleton skeleton)
        {
            // Set the highlight time to be a short time from now.
            this.highlightTime = DateTime.UtcNow + TimeSpan.FromSeconds(0.5);

            // Record the ID of the skeleton.
            this.highlightId = skeleton.TrackingId;
        }

        /// <summary>
        /// Draw stick men for all the tracked skeletons.
        /// </summary>
        /// <param name="skeletons">The skeletons to draw.</param>
        private void DrawStickMen(Skeleton[] skeletons)
        {
            // Remove any previous skeletons.
            StickMen.Children.Clear();

            foreach (var skeleton in skeletons)
            {
                // Only draw tracked skeletons.
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                {
                    // Draw a background for the next pass.
                    this.DrawStickMan(skeleton, Brushes.WhiteSmoke, 7);
                }
            }

            foreach (var skeleton in skeletons)
            {
                // Only draw tracked skeletons.
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                {
                    // Pick a brush, Red for a skeleton that has recently gestures, black for the nearest, gray otherwise.
                    var brush = DateTime.UtcNow < this.highlightTime && skeleton.TrackingId == this.highlightId ? Brushes.Red :
                        skeleton.TrackingId == this.nearestId ? Brushes.Black : Brushes.Gray;

                    // Draw the individual skeleton.
                    this.DrawStickMan(skeleton, brush, 3);
                }
            }
        }

        /// <summary>
        /// Draw an individual skeleton.
        /// </summary>
        /// <param name="skeleton">The skeleton to draw.</param>
        /// <param name="brush">The brush to use.</param>
        /// <param name="thickness">This thickness of the stroke.</param>
        private void DrawStickMan(Skeleton skeleton, Brush brush, int thickness)
        {
            Debug.Assert(skeleton.TrackingState == SkeletonTrackingState.Tracked, "The skeleton is being tracked.");

            foreach (var run in SkeletonSegmentRuns)
            {
                var next = this.GetJointPoint(skeleton, run[0]);
                for (var i = 1; i < run.Length; i++)
                {
                    var prev = next;
                    next = this.GetJointPoint(skeleton, run[i]);

                    var line = new Line
                    {
                        Stroke = brush,
                        StrokeThickness = thickness,
                        X1 = prev.X,
                        Y1 = prev.Y,
                        X2 = next.X,
                        Y2 = next.Y,
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeStartLineCap = PenLineCap.Round
                    };

                    StickMen.Children.Add(line);
                }
            }
        }

        /// <summary>
        /// Convert skeleton joint to a point on the StickMen canvas.
        /// </summary>
        /// <param name="skeleton">The skeleton.</param>
        /// <param name="jointType">The joint to project.</param>
        /// <returns>The projected point.</returns>
        private Point GetJointPoint(Skeleton skeleton, JointType jointType)
        {
            var joint = skeleton.Joints[jointType];

            // Points are centered on the StickMen canvas and scaled according to its height allowing
            // approximately +/- 1.5m from center line.
            var point = new Point
            {
                X = (StickMen.Width / 2) + (StickMen.Height * joint.Position.X / 3),
                Y = (StickMen.Width / 2) - (StickMen.Height * joint.Position.Y / 3)
            };

            return point;
        }
    }
}
