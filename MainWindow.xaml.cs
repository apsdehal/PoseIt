//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using xna = Microsoft.Xna.Framework;
    using math = System.Math;
    using System.Collections.Generic;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        private readonly Pen correctBonePen = new Pen(Brushes.Green, 6);

        private readonly Pen incorrectBonePen = new Pen(Brushes.Red, 6);

        private readonly Pen readingBonePen = new Pen(Brushes.Yellow, 6);
       
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;


        ///<summary>
        /// Variable for storing first Skeleton
        ///</summary>
        private Skeleton[] currentSkeletons = new Skeleton[0];

        private Skeleton firstSkeleton;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;
                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);
 
            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1, Pen passedPen = null )
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            if (passedPen != null)
            {
                drawPen = passedPen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

        private double getAngle(Skeleton skeleton, JointType type1, JointType type2, JointType type3)
        {
            xna.Vector3 cross, j1to2, j2to3;

            Joint joint1 = skeleton.Joints[type1];
            Joint joint2 = skeleton.Joints[type1];
            Joint joint3 = skeleton.Joints[type1];

            j1to2 = new xna.Vector3(joint1.Position.X - joint2.Position.Y, joint1.Position.Y - joint2.Position.Y, joint1.Position.Z - joint2.Position.Z);
            j2to3 = new xna.Vector3(joint2.Position.X - joint3.Position.Y, joint2.Position.Y - joint3.Position.Y, joint2.Position.Z - joint3.Position.Z);

            j1to2.Normalize();
            j2to3.Normalize();

            double dot = xna.Vector3.Dot(j1to2, j2to3);

            cross = xna.Vector3.Cross(j1to2, j2to3);
            double crosslength = cross.Length();

            double angle = math.Atan2(crosslength, dot);
            angle = angle * (180 / math.PI);

            angle = math.Round(angle, 2);

            return angle;
        }

        private List<double> getAnglesFromSkeleton(Skeleton skeleton)
        {
            List<double> Angles = new List<double>();
            Angles.Add(this.getAngle(skeleton, JointType.Head, JointType.ShoulderCenter, JointType.ShoulderRight));
            Angles.Add(this.getAngle(skeleton, JointType.ShoulderCenter, JointType.ShoulderRight, JointType.ElbowRight));
            Angles.Add(this.getAngle(skeleton, JointType.ShoulderRight, JointType.ElbowRight, JointType.WristRight ));
            Angles.Add(this.getAngle(skeleton, JointType.ElbowRight, JointType.WristRight, JointType.HandRight));
            Angles.Add(this.getAngle(skeleton, JointType.Head, JointType.ShoulderCenter, JointType.ShoulderLeft));
            Angles.Add(this.getAngle(skeleton, JointType.ShoulderCenter, JointType.ShoulderLeft, JointType.ElbowLeft));
            Angles.Add(this.getAngle(skeleton, JointType.ShoulderLeft, JointType.ElbowLeft, JointType.WristLeft));
            Angles.Add(this.getAngle(skeleton, JointType.ElbowLeft, JointType.WristLeft, JointType.HandLeft ));
            Angles.Add(this.getAngle(skeleton, JointType.HipCenter, JointType.HipRight, JointType.KneeRight));
            Angles.Add(this.getAngle(skeleton, JointType.HipRight, JointType.KneeRight, JointType.AnkleRight));
            Angles.Add(this.getAngle(skeleton, JointType.HipCenter, JointType.HipLeft, JointType.KneeLeft));
            Angles.Add(this.getAngle(skeleton, JointType.HipLeft, JointType.KneeLeft, JointType.AnkleLeft));

            return Angles;
        }

        private List<bool> getAngleDifference( List<double> angles1, List<double> angles2)
        {
            List<bool> Results = new List<bool>();
            int i = 0;
            for (i = 0; i < 12; i++)
            {
                if (math.Abs(angles1[i] - angles2[i]) > 20)
                {
                    Results.Add(true);
                }
                else
                {
                    Results.Add(false);
                }
            }
            return Results;
        }

        private void checkResults(Skeleton skeleton, DrawingContext drawingContext, List<bool> results)
        {
            int i = 0;
            for (i = 0; i < 12; i++)
            {
                if (results[i] == true)
                {
                    JointType[] wrongJoints = this.retrieveWrongJoints(i);
                    this.DrawBone(skeleton, drawingContext, wrongJoints[0], wrongJoints[1], this.incorrectBonePen);
                    this.DrawBone(skeleton, drawingContext, wrongJoints[1], wrongJoints[2], this.incorrectBonePen);
                }
            }

        }

        private JointType[] retrieveWrongJoints(int i)
        {
            JointType[] joints = new JointType[3];
            switch (i)
            {
                case 0:
                    joints[0] = JointType.Head;
                    joints[1] = JointType.ShoulderCenter;
                    joints[2] = JointType.ShoulderRight;
                    break;
                case 1:
                    joints[0] = JointType.ShoulderCenter;
                    joints[1] = JointType.ShoulderRight;
                    joints[2] = JointType.ElbowRight;
                    break;
                case 2:
                    joints[0] = JointType.ShoulderRight;
                    joints[1] = JointType.ElbowRight;
                    joints[2] = JointType.WristLeft;
                    break;
                case 3:
                    joints[0] = JointType.ElbowRight;
                    joints[1] = JointType.WristRight;
                    joints[2] = JointType.HandRight;
                    break;
                case 4:
                    joints[0] = JointType.Head;
                    joints[1] = JointType.ShoulderCenter;
                    joints[2] = JointType.ShoulderLeft;
                    break;
                case 5:
                    joints[0] = JointType.ShoulderCenter;
                    joints[1] = JointType.ShoulderLeft;
                    joints[2] = JointType.ElbowLeft;
                    break;
                case 6:
                    joints[0] = JointType.ShoulderLeft;
                    joints[1] = JointType.ElbowLeft;
                    joints[2] = JointType.WristLeft;
                    break;
                case 7:
                    joints[0] = JointType.ElbowLeft;
                    joints[1] = JointType.WristLeft;
                    joints[2] = JointType.HandLeft;
                    break;
                case 8:
                    joints[0] = JointType.HipCenter;
                    joints[1] = JointType.HipRight;
                    joints[2] = JointType.KneeRight;
                    break;
                case 9:
                    joints[0] = JointType.HipRight;
                    joints[1] = JointType.KneeRight;
                    joints[2] = JointType.AnkleRight;
                    break;
                case 10:
                    joints[0] = JointType.HipCenter;
                    joints[1] = JointType.HipLeft;
                    joints[2] = JointType.KneeLeft;
                    break;
                case 11:
                    joints[0] = JointType.HipLeft;
                    joints[1] = JointType.KneeLeft;
                    joints[2] = JointType.AnkleLeft;
                    break;
                    
            }
            return joints;
        }

        private void GetFirstSkeleton(SkeletonFrameReadyEventArgs e)
        {
            Skeleton first = null;
            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {
                if (frame == null) return null;
                frame.CopySkeletonDataTo(this.currentSkeletons);
                foreach (Skeleton s in this.currentSkeletons)
                {
                    if (s.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        first = s;
                        break;
                    }
                }
            }

            this.firstSkeleton = first;
        }
    }
}