#region License stuff

// Eric McCann - 2011
// University of Massachusetts Lowell
// 
// 
// The DREAMController is intellectual property of the University of Massachusetts lowell, and is patent pending.
// 
// Your rights to distribute, videotape, etc. any works that make use of the DREAMController are entirely contingent on the specific terms of your licensing agreement.
// 
// Feel free to edit any of the supplied samples, or reuse the code in other projects that make use of the DREAMController. They are provided as a resource.
// 
// 
// For license-related questions, contact:
// 	Kerry Lee Andken
// 	kerrylee_andken@uml.edu
// m
// For technical questions, contact:
// 	Eric McCann
// 	emccann@cs.uml.edu
// 	
// 

#endregion

#region USINGZ

using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DREAMController;
using GenericTouchTypes;
using System.IO;
using System.Threading;
using Messages;
using Messages.custom_msgs;
using Ros_CSharp;
using XmlRpc_Wrapper;
using Int32 = Messages.std_msgs.Int32;
using String = Messages.std_msgs.String;
using m = Messages.std_msgs;
using gm = Messages.geometry_msgs;
using nm = Messages.nav_msgs;
using sm = Messages.sensor_msgs;
using d = System.Drawing;
using Touch = GenericTouchTypes.Touch;
using cm = Messages.custom_msgs;
using tf = Messages.tf;
using System.Text;
using EM3MTouchLib;
using System.ComponentModel;

#endregion

namespace DREAMPioneer
{ 
    /// <summary>
    ///   Interaction logic for SurfaceWindow1.xaml
    /// </summary>
    public partial class SurfaceWindow1 : Window//, INotifyPropertyChanged
    {
        public static SurfaceWindow1 current;
        private SortedList<int, SlipAndSlide> captureVis = new SortedList<int, SlipAndSlide>();
        private SortedList<int, Touch> captureOldVis = new SortedList<int, Touch>();
        private JoystickManager joymgr;
        private NodeHandle node;

        private Messages.geometry_msgs.Twist t;
        private cm.ptz pt;
        private EM3MTouch em3m;
        private DateTime currtime;

        private Publisher<gm.Twist> joyPub;
        private Publisher<cm.ptz> servosPub;
        private Publisher<gm.PoseWithCovarianceStamped> initialPub;
        private Subscriber<sm.LaserScan> laserSub;
        private Publisher<gm.PoseStamped> goalPub;
        private gm.PoseWithCovarianceStamped pose;
        private gm.PoseStamped goal;

        private ScaleTransform scale;
        private TranslateTransform translate;
        private ScaleTransform dotscale;
        private TranslateTransform dottranslate;


        private Timer YellowTimer;
        private Timer GreenTimer;
        TimeData checkHold;
        int numRobots;

        private TimerManager timers = new TimerManager();

        // <summary>
        ///   Should contain all the robots in existance.
        /// </summary>
        public SortedList<int, ROS_ImageWPF.RobotControl> robots = new SortedList<int, ROS_ImageWPF.RobotControl>();

        /// <summary>
        ///   The yellow dots.
        /// </summary>
        private List<ROS_ImageWPF.RobotControl> YellowDots = new List<ROS_ImageWPF.RobotControl>();

        /// <summary>
        ///   The green dots.
        /// </summary>
        private List<ROS_ImageWPF.RobotControl> GreenDots = new List<ROS_ImageWPF.RobotControl>();

        /// <summary>
        ///   The time in seconds after which the 'double tap' gesture can be considered activated.
        /// </summary>
        private const int TimeDT = 600;

        private DateTime n;
        private Touch lastt;
        private static float PPM = 0.02868f;
        private static float MPP = 1.0f / PPM;

        /// <summary>
        ///   The waypoint dots.
        /// </summary>
        private Dictionary<Point, Ellipse> waypointDots = new Dictionary<Point, Ellipse>();

        /// <summary>
        ///   The lasso points.
        /// </summary>
        /// TODO Mark: add summary information to these variables
        private List<Point> lassoPoints = new List<Point>();

        /// <summary>
        ///   The lasso line.
        /// </summary>
        private Polyline lassoLine = new Polyline
        {
            Stroke = Brushes.AntiqueWhite,
            StrokeThickness = 3,
            StrokeDashOffset = 2,
            StrokeDashArray = new DoubleCollection(new double[] { 2, 1 })
        };

        /// <summary>
        ///   The lasso line dash style.
        /// </summary>
        private DoubleCollection lassoLineDashStyle = new DoubleCollection(2);


        /// <summary>
        ///   The yellow current.
        /// </summary>
        private double yellowCurrent = 0.2;

        /// <summary>
        ///   The yellow min.
        /// </summary>
        private double yellowMin = 0.2;

        /// <summary>
        ///   The yellow max.
        /// </summary>
        private double yellowMax = 0.7;

        /// <summary>
        ///   The yellow delta.
        /// </summary>
        private double yellowDelta = 0.02;

        /// <summary>
        ///   The green current.
        /// </summary>
        private double greenCurrent = 0.1;

        /// <summary>
        ///   The green min.
        /// </summary>
        private double greenMin = 0.1;

        /// <summary>
        ///   The green max.
        /// </summary>
        private double greenMax = 0.9;

        /// <summary>
        ///   The green delta.
        /// </summary>
        private double greenDelta = 0.05;

        /// <summary>
        ///   These are the possible states for the robot movement FSM.
        /// </summary>
        public enum RMState
        {
            /// <summary>
            ///   The start.
            /// </summary>
            Start = 0,

            /// <summary>
            ///   The r m 1.
            /// </summary>
            State1,

            /// <summary>
            ///   The r m 2.
            /// </summary>
            State2,

            /// <summary>
            ///   The r m 3.
            /// </summary>
            State3,

            /// <summary>
            ///   The r m 4.
            /// </summary>
            State4,

            /// <summary>
            ///   The r m 7.
            /// </summary>
            State5
        } ;

        /// <summary>
        ///   The RM FSM state.
        /// </summary>
        private RMState state = RMState.Start;

        public SurfaceWindow1()
        {
            current = this;
            InitializeComponent();
            em3m = new EM3MTouch();
            if (!em3m.Connect())
            {
                Console.WriteLine("IT'S BORKED OH NO!");
            }
            else
            {
                em3m.DownEvent += Down;
                em3m.ChangedEvent += Changed;
                em3m.UpEvent += Up;
                
            }

        }

        private void rosStart()
        {
            ROS.ROS_MASTER_URI = "http://10.0.2.182:11311";
            Console.WriteLine("CONNECTING TO ROS_MASTER URI: " + ROS.ROS_MASTER_URI);
          //  ROS.ROS_HOSTNAME = "10.0.2.163";
            ROS.Init(new string[0], "DREAM");
            node = new NodeHandle();

            t = new gm.Twist { angular = new gm.Vector3 { x = 0, y = 0, z = 0 }, linear = new gm.Vector3 { x = .1, y = .1, z = 0 } };
            joyPub = node.advertise<gm.Twist>("/robot_brain_1/virtual_joystick/cmd_vel", 1000); 

            pt = new cm.ptz { x = 0, y = 0, CAM_MODE = ptz.CAM_ABS };
            servosPub = node.advertise<cm.ptz>("/robot_brain_1/servos", 1000);
            
            goal = new gm.PoseStamped() { header = new m.Header { frame_id = new String("/robot_brain_1/map") }, pose = new gm.Pose { position = new gm.Point {x=1, y= 1, z = 0 }, orientation = new gm.Quaternion {w = 0, x = 0, y = 0, z = 0 } } };
            goalPub = node.advertise<gm.PoseStamped>("/robot_brain_1/goal", 1000);

            //Deprecated until I make an abstraction in ros that can publish transforms
            //pose = new gm.PoseWithCovarianceStamped() { header = new m.Header { frame_id = new String("/robot_brain_1/map") }, pose = new gm.PoseWithCovariance { pose = new gm.Pose { orientation = new gm.Quaternion { w = .015, x = 0, y = 0, z = 1 }, position = new gm.Point { x = 29.9, y = 3.5, z = 0 } }, covariance = new double[] { .25, 0, 0, 0, 0, 0, 0, .25, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .06853891945200942 } } };
            //initialPub = node.advertise<gm.PoseWithCovarianceStamped>("/robot_brain_1/initialpose",1000);

            laserSub = node.subscribe<sm.LaserScan>("/robot_brain_1/scan", 1000, laserCallback);
            
            
            currtime = DateTime.Now;
            tf_node.init();
            Dispatcher.Invoke(new Action(() =>
            {
                lastt = new Touch();
                scale = new ScaleTransform();
                translate = new TranslateTransform();
            dotscale = new ScaleTransform();
            dottranslate = new TranslateTransform();

                TransformGroup group = new TransformGroup();
            TransformGroup dotgroup = new TransformGroup();
                group.Children.Add(scale);
                group.Children.Add(translate);
            dotgroup.Children.Add(dotscale);
            dotgroup.Children.Add(dottranslate);
                SubCanvas.RenderTransform = group;
            DotCanvas.RenderTransform = dotgroup;
                n = DateTime.Now;
                lastupdown = DateTime.Now;
            for (int i = 0; i < 1; i++)
            {
                AddRobot();
            }

            timers.StartTimer(ref YellowTimer, YellowTimer_Tick, 0, 10);
            timers.StartTimer(ref GreenTimer, GreenTimer_Tick, 0, 5);
            timers.MakeTimer(ref RM5Timer, RM5Timer_Tick, TimeDT, Timeout.Infinite);
            

            for (int i = 0; i < turboFingering.Length; i++)
                timers.MakeTimer(ref turboFingering[i], fingerHandler, i, 600, Timeout.Infinite);


            }));

            timers.StartTimer(ref YellowTimer, YellowTimer_Tick, 0, 10);
            timers.StartTimer(ref GreenTimer, GreenTimer_Tick, 0, 5);
            timers.MakeTimer(ref RM5Timer, RM5Timer_Tick, TimeDT, Timeout.Infinite);
            numRobots = 1;

            for (int i = 0; i < turboFingering.Length; i++)
                timers.MakeTimer(ref turboFingering[i], fingerHandler, i, 600, Timeout.Infinite);

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            joymgr = new JoystickManager(this, MainCanvas);
            Console.WriteLine("Loading Window");
            joymgr.Joystick += joymgr_Joystick;
            joymgr.UpEvent += JoymgrFireUpEvent;
            joymgr.SetDesiredControlPanelWidthToHeightRatios(1, 1.333);
            
            //tell the joystick manager HOW TO INITIALIZE YOUR CUSTOM CONTROL PANELS... and register for events, if your control panel fires them
            //EVERY JOYSTICK'S CONTROL PANEL IS (now) A NEW INSTANCE! TO MAKE VALUES PERSIST, USE STATICS OR A STORAGE CLASS!
            joymgr.InitControlPanels(
                () =>
                {
                    LeftControlPanel lp = new LeftControlPanel();
                    lp.Init(this);
                    return (ControlPanel)lp;
                },
                () =>
                {
                    RightControlPanel rp = new RightControlPanel();
                    rp.Init(this);
                    return (ControlPanel)rp;
                });


            new Thread(rosStart).Start();
        }

        private void JoymgrFireUpEvent(Touch e)
        {
            if (captureVis.ContainsKey(e.Id))
                captureVis.Remove(captureVis[e.Id].DIEDIEDIE());
        }

        private void joymgr_Joystick(bool RightJoystick, double rx, double ry)
        {
            if (!RightJoystick)
            {
                t.linear.x = ry / -10.0;
                t.angular.z = rx / -10.0;
                joyPub.publish(t);
            }
            else
            {
                if(currtime.Ticks + (long)(Math.Pow(10,6)) <= ( DateTime.Now.Ticks ))
                {
                    pt.x = (float)(rx /* 10.0*/);
                    pt.y = (float)(ry * 1 /* -10.0*/);
                    pt.CAM_MODE = ptz.CAM_ABS;
                    servosPub.publish(pt);
                    currtime = DateTime.Now;
                }
            }
        }

        private void joymgr_Button(int action, bool down)
        {
           // Console.WriteLine("" + action + " = " + down);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (joymgr != null)
            {
                joymgr.Close();
                joymgr = null;
            }
            base.OnClosed(e);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Q && e.KeyStates == KeyStates.Down)
                Close();
        }

        public void robotCallback(gm.PolygonStamped poly)
        {

        }
        public void videoCallback(sm.Image image)
        {
            
            d.Size size = new d.Size();
            size.Height = (int)image.height;
            size.Width = (int)image.width;

//            t1 = t2;
//            t2 = DateTime.Now;
//            double fps = 1000.0 / (t2.Subtract(t1).Milliseconds);
//            Console.WriteLine(fps);

            Dispatcher.BeginInvoke(new Action(() => { RightControlPanel rcp = joymgr.RightPanel as RightControlPanel; if (rcp != null)
            
                rcp.webcam.UpdateImage(image.data, new System.Windows.Size(size.Width, size.Height), false); }));
        }

        public void laserCallback(sm.LaserScan laserScan)
        {
            double[] scan = new double[laserScan.ranges.Length];
            for (int i = 0; i < laserScan.ranges.Length; i++)
            {
                 if (i - 1 >= 0)
                {
                    if (laserScan.ranges[i] < 0.3f)
                        scan[i] = scan[i-1];
                    else
                        scan[i] = laserScan.ranges[i];
                }
                else
                {
                    if (laserScan.ranges[i] < 0.3f)
                        scan[i] = laserScan.ranges[i+1];
                    else 
                        scan[i] = laserScan.ranges[i];
                }
            }

            
            Dispatcher.BeginInvoke(new Action(() => {
                LeftControlPanel lcp = joymgr.LeftPanel as LeftControlPanel;
                if (lcp != null)
                    lcp.newRangeCanvas.SetLaser(scan, laserScan.angle_increment, laserScan.angle_min); 
            }));
        }

        public void AddRobot()
        {
            robots.Add(0,robot_brain_1);
            numRobots += 1;
        }

        public double distance(Touch c1, Touch c2)
        {
            return distance(c1.Position, c2.Position);
        }

        public static double distance(Point q, Point p)
        {
            return distance(q.X, q.Y, p.X, p.Y);
        }

        /// <summary>
        ///   This function will return the id of the robot that is closest to the point passed to it. 
        ///   This robot should also be within the MaxNearnessDistance range. If no robot is found then
        ///   -1 is returned.
        /// </summary>
        /// <param name = "p">
        ///   This is the point that from which the distance to the robots is measured.
        /// </param>
        /// <returns>
        ///   ID of the robot that passes the nearness criterion and is closest to point p.
        /// </returns>
        private int CloseToRobot(Point p)
        {
            double distance;
            double closestDist = robots[0].robot.Width / scale.ScaleX;
            int id = -1;

            for (int i = 0; i < robots.Count; i++)
            {
                distance = Math.Sqrt(Math.Pow(p.X - robots[i].xPos, 2) + Math.Pow(p.Y - robots[i].yPos, 2));

                // Find the shortest distance and record the id and distance of that robot.
                if (distance < closestDist)
                {
                    closestDist = distance;
                    id = i;
                }
            }

            return id;
        }

        public static double distance(double x2, double y2, double x1, double y1)
        {
            return Math.Sqrt(
                (x2 - x1) * (x2 - x1)
                + (y2 - y1) * (y2 - y1));
        }

        /// <summary>
        ///   event handlers for yellow pulsing lights
        /// </summary>
        /// <param name = "sender">
        /// </param>
        private void YellowTimer_Tick(object sender)
        {
            yellowCurrent += yellowDelta;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (ROS_ImageWPF.RobotControl el in YellowDots)
                    el.SetOpacity(yellowCurrent);
            }));
            if (((yellowDelta > 0) && (yellowCurrent >= yellowMax))
                || ((yellowDelta < 0) && (yellowCurrent <= yellowMin)))
                yellowDelta = 0 - yellowDelta;
        }

        /// <summary>
        ///   The green timer_ tick.
        /// </summary>
        /// <param name = "sender">
        ///   The sender.
        /// </param>
        private void GreenTimer_Tick(object sender)
        {
            greenCurrent += greenDelta;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (ROS_ImageWPF.RobotControl el in GreenDots)
                    el.SetOpacity(greenCurrent);
            }));
            if (((greenDelta > 0) && (greenCurrent >= greenMax))
                || ((greenDelta < 0) && (greenCurrent <= greenMin)))
                greenDelta = 0 - greenDelta;
        }

        /// <summary>
        ///   This list contains the ids of all the robots that are currently selected.
        /// </summary>
        private List<int> selectedList = new List<int>();

        /// <summary>
        ///   Hold onto your butt for this one...
        /// </summary>
        /// <param name = "i">
        ///   the number of the robot whose light you want to start pulsing
        /// </param>
        public void PulseYellow(int i)
        {
            NoPulse(i);
            AddYellow(i);
        }

        /// <summary>
        ///   The pulse yellow.
        /// </summary>
        public void PulseYellow()
        {
            for (int i = 0; i < 2/* ADD REAL*/; i++)
                PulseYellow(i);
        }

        /// <summary>
        ///   The pulse green.
        /// </summary>
        /// <param name = "i">
        ///   The i.
        /// </param>
        public void PulseGreen(int i)
        {
            if (i == -1)
                throw new Exception();
            if (robots.ContainsKey(i))
            {
                if (!GreenDots.Contains(robots[i]))
                {
                    NoPulse(i);
                    AddGreen(i);
                }
            }
        }

        /// <summary>
        ///   The no pulse.
        /// </summary>
        public void NoPulse()
        {
            for (int i = 0; i < numRobots; i++)
            {
                    NoPulse(i);
            }
        }

        /// <summary>
        ///   stops the pulsethread, so the light stops pulsing... also makes the light colorless ("off")
        /// </summary>
        /// <param name = "i">
        /// </param>
        public void NoPulse(int i)
        {
            if (i < 0 || i >= numRobots || !robots.ContainsKey(i))
                return;
            RemoveYellow(i);
            RemoveGreen(i);
        }

        /// <summary>
        ///   The pulse green.
        /// </summary>
        public void PulseGreen()
        {
            for (int i = 0; i < numRobots; i++)
                PulseGreen(i);
        }

        /// <summary>
        ///   add a dot to the yellow list and start the yellow timer if it is stopped
        /// </summary>
        /// <param name = "i">
        /// </param>
        public void AddYellow(int i)
        {
            robots[i].SetColor(Brushes.Yellow);
            YellowDots.Add(robots[i]);
            timers.StartTimer(ref YellowTimer);
        }

        /// <summary>
        ///   add a dot to the yellow list and start the yellow timer if it is stopped
        /// </summary>
        /// <param name = "i">
        /// </param>
        public void AddGreen(int i)
        {
            robots[i].SetColor(Brushes.Green);
            GreenDots.Add(robots[i]);
            timers.StartTimer(ref GreenTimer);
        }

        /// <summary>
        ///   remove a dot from the yellow list and stop the yellow timer if it is empty afterwards
        ///   make the circle black and 0.3 opacity for testing reasons
        /// </summary>
        /// <param name = "i">
        /// </param>
        public void RemoveYellow(int i)
        {
            YellowDots.Remove(robots[i]);
            robots[i].SetColor(Brushes.Blue);
            if (YellowDots.Count == 0)
                timers.StopTimer(ref YellowTimer);
        }

        /// <summary>
        ///   The remove green.
        /// </summary>
        /// <param name = "i">
        ///   The i.
        /// </param>
        public void RemoveGreen(int i)
        {
            if (GreenDots.Contains(robots[i]))
            {
                GreenDots.Remove(robots[i]);
                robots[i].SetColor(Brushes.Blue);
                if (GreenDots.Count == 0)
                    timers.StopTimer(ref GreenTimer);
            }
        }

        /// <summary>
        ///   The finger handler.
        /// </summary>
        /// <param name = "state">
        ///   The state.
        /// </param>
        private void fingerHandler(object state)
        {
            int robot = (int)state;
            if (!timers.IsRunning(ref turboFingering[robot]))
                return;
            timers.StopTimer(ref turboFingering[robot]);
        }

        public void AddSelected(int robot, Touch e)
        {
            Console.WriteLine("SELECTING " + robot);
            if ( /* NEED SPECIAL FOR MANUAL &&*/ !selectedList.Contains(robot))
            {
                selectedList.Add(robot);
                PulseYellow(robot);
            }
        }

        DateTime lastupdown = DateTime.Now;

        public void moveStuff(Touch e)
        {
            n = DateTime.Now;
            bool SITSTILL = (n.Subtract(lastupdown).TotalMilliseconds <= 1);
            bool zoomed = false;
            Console.WriteLine(joymgr.FreeTouches);
            if ( distance(e, captureOldVis[e.Id] ) > .1 && !SITSTILL)
            {
                lastupdown = DateTime.Now;

                if(captureOldVis.Count > 1)
                {
                    foreach (Touch p in captureOldVis.Values)
                    {
                        if (p.Id != e.Id )
                        {   //- distance(captureOldVis[e.Id], captureOldVis[p.Id])
                            if (scale.ScaleX + Math.Abs(distance(e, p) - distance(captureOldVis[e.Id], captureOldVis[p.Id]) ) > 2)
                            {
                                Console.WriteLine(Math.Abs(distance(e, p) - distance(captureOldVis[e.Id], captureOldVis[p.Id])));
                                if ( ((distance(e, p) - distance(captureOldVis[e.Id], captureOldVis[p.Id])) / 400) > 0.5)
                                {
                                    scale.ScaleX += ((distance(e, p) - distance(captureOldVis[e.Id], captureOldVis[p.Id])) / 400);
                                    scale.ScaleY += ((distance(e, p) - distance(captureOldVis[e.Id], captureOldVis[p.Id])) / 400);
                                    dotscale.ScaleX += ((distance(e, p) - distance(captureOldVis[e.Id], captureOldVis[p.Id])) / 400);
                                    dotscale.ScaleY += ((distance(e, p) - distance(captureOldVis[e.Id], captureOldVis[p.Id])) / 400);
                                    zoomed = true;
                                }
                            }
                        }
                    }
                    if (!zoomed)
                    {
                        translate.X += (e.Position.X - captureOldVis[e.Id].Position.X);
                        translate.Y += (e.Position.Y - captureOldVis[e.Id].Position.Y);
                        dottranslate.X += (e.Position.X - captureOldVis[e.Id].Position.X);
                        dottranslate.Y += (e.Position.Y - captureOldVis[e.Id].Position.Y);
                    }
                }


            } if (captureOldVis.ContainsKey(e.Id))
                captureOldVis.Remove(e.Id);
            captureOldVis.Add(e.Id, e);
     
        }

        /// <summary>
        ///   Gets FREE.
        /// </summary>
        public Dictionary<int, Touch> FREE
        {
            get { return joymgr.FreeTouches; }
        }

        private void ChangeState(RMState s)
        {
            state = s;

            if (s == RMState.Start)
            {
                selectedList.Clear();
                lock (waypointDots)
                {
                    foreach (KeyValuePair<Point, Ellipse> kvp in waypointDots)
                    {
                        MainCanvas.Children.Remove(kvp.Value);
                    }
                    waypointDots.Clear();
                }
            }
        }

        /// <summary>
        ///   The r m 5 start.
        /// </summary>
        /// <param name = "last">
        ///   The last.
        /// </param>
        public void RM5Start(Point last)
        {
            if (!turnedIntoDrag && Math.Abs(distance(last, lastTouch)) < DTDistance && timers.IsRunning(ref RM5Timer))
            {
                RM5DoIt();
                RM5End();
                return;
            }
            else
            {
                ChangeState(RMState.State3);
                if (timers.IsRunning(ref RM5Timer))
                {
                    AddWaypointDot(lastTouch);
                    RM5End();
                }

                lastTouch = last;
                timers.StartTimer(ref RM5Timer);
            }
        }

        private double DTDistance
        {
            get
            {
                if (joymgr != null)
                    return 0.5 * joymgr.DPI;
                return 30;
            }
        }

        /// <summary>
        ///   The r m 5 do it.
        /// </summary>
        private void RM5DoIt()
        {
            AddWaypointDot(lastTouch);
            HandOutWaypoints();
            EndState("CD-G (t<dt && d<dD)");
        }

        /// <summary>
        ///   The r m 5 timer_ tick.
        /// </summary>
        /// <param name = "sender">
        ///   The sender.
        /// </param>
        private void RM5Timer_Tick(object sender)
        {
            RM5End();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!turnedIntoDrag && captureVis.Count >= 1)
                {
                    AddWaypointDot(lastTouch);
                }
            }));
        }

        /// <summary>
        ///   The r m 5 end.
        /// </summary>
        public void RM5End()
        {
            timers.StopTimer(ref RM5Timer);
        }

        /// <summary>
        ///   The last waypoint dot.
        /// </summary>
        private Point lastWaypointDot;

        /// <summary>
        ///   The add waypoint dot.
        /// </summary>
        /// <param name = "p">
        ///   The p.
        /// </param>
        private void AddWaypointDot(Point p)
        {
            if (Math.Abs(distance(lastWaypointDot, p)) > (joymgr.DPI / 43) * 10)
            {
                lock (waypointDots)
                    if (waypointDots.ContainsKey(p)) return;
                lastWaypointDot = p;
                Ellipse newEllipse = new Ellipse
                {
                    Width = 5 * joymgr.DPI / 80,// / dotscale.ScaleX,
                    Height = 5 * joymgr.DPI / 80,// / dotscale.ScaleY,
                    Fill = Brushes.Yellow,
                    Margin = new Thickness { Bottom = 0, Top = 1 / dotscale.ScaleY * (p.Y - dottranslate.Y), Left = 1 / dotscale.ScaleX * (p.X - dottranslate.X), Right = 0 }
                };
                lock (waypointDots)
                    waypointDots.Add(p, newEllipse);
                DotCanvas.Children.Add(newEllipse);
            }else
            {
                ;
            }
            /*if (waypointDots.Count > 10)
                clearAllDot();*/
        }
        private void clearAllDot()
        {
            List<Point> temp = new List<Point>();
            
            foreach (Point p in waypointDots.Keys)
            {
                temp.Add( p );
            }
            foreach (Point p in temp)
            {
                DotCanvas.Children.Remove( waypointDots[p] );
            }
            waypointDots.Clear();
        }

        private Timer[] turboFingering = new Timer[40];

        /// <summary>
        ///   The turbo finger count.
        /// </summary>
        private int[] turboFingerCount = new int[40];

        private bool ToggleSelected(int robot, Touch e)
        {
            bool res = true;
            if (robot == -1)
                return false;

            if (!timers.IsRunning(ref turboFingering[robot]))
            {
                if (selectedList.Contains(robot))
                    RemoveSelected(robot, e);
                else
                    AddSelected(robot, e);
                turboFingerCount[robot] = 0;
            }
            else
            {
                timers.StopTimer(ref turboFingering[robot]);
            }
            if (selectedList.Count == 0)
            {
                res = false;
            }
            timers.StartTimer(ref turboFingering[robot]);
            turboFingerCount[robot]++;
            return res;
        }

        private bool turnedIntoDrag;

        private void RemoveSelected(int robot, Touch e)
        {
            selectedList.Remove(robot);
            NoPulse(robot);
        }
        /// <summary>
        ///   Returns the id of the robot that was responsible for the contact event.
        /// </summary>
        /// <param name = "e">
        ///   Contact event in question.
        /// </param>
        /// <returns>
        ///   ID of the robot in question. -1 if no robot was involved.
        /// </returns>
        private int robotsCD(Touch e)
        {
            Double xPos = ((e.Position.X - translate.X) / scale.ScaleX * PPM);
            Double yPos = ((e.Position.Y - translate.Y) / scale.ScaleY * PPM);


            for (int i = 0; i < numRobots; i++)
            {
                if (!robots.ContainsKey(i)) continue;
                Double _xPos = ((robots[i].xPos) * PPM);
                Double _yPos = ((robots[i].yPos) * PPM);
                Double Width = robots[i].robot.ActualWidth  * scale.ScaleX * PPM;
                if (distance(xPos, yPos, _xPos, _yPos) < Width / 2)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool robotSelected(int n)
        {
            if (n > -1)
            {
                AddYellow(n);
                return true;
            } return false;
        }

        private void Down(Touch e)
        {
            joymgr.Down(e, (t, b) =>
                                        {
                                            if (!b)
                                            {
                                                captureVis.Add(t.Id, new SlipAndSlide(t));
                                                if(!captureOldVis.ContainsKey(e.Id) )
                                                    captureOldVis.Add(t.Id, e);
                                                captureVis[t.Id].dot.Stroke = Brushes.White;

                                                ZoomDown(e);
                                                int index = robotsCD(e);
                                                if (selectedList.Count == 0 && (state == RMState.State2 || state == RMState.State4))
                                                    ChangeState(RMState.Start);

                                                switch (state)
                                                {
                                                    case RMState.Start:

                                                        // If the CD was on a robot then ...
                                                        if (index != -1)
                                                        {
                                                            ToggleSelected(index, e);
                                                            ChangeState(RMState.State1);
                                                        }

                                    // If the CD was on gound then...
                                                        else if ((index = CloseToRobot(e.Position)) != -1)
                                                        {
                                                            AddSelected(index, e);
                                                            ChangeState(RMState.State1);
                                                        }
                                                        break;
                                                    case RMState.State1:
                                                        break;
                                                    case RMState.State2:
                                                        if (index != -1) // If the CD was on a robot then ...
                                                        {
                                                            ToggleSelected(index, e);
                                                            ChangeState(RMState.State1);
                                                        }
                                                        else // If the CD was on gound then...
                                                        {
                                                            //index = CloseToRobot(e.Position); // Was the contact close enough to a robot?
                                                            if ((index = CloseToRobot(e.Position)) != -1)
                                                            {
                                                                AddSelected(index, e);
                                                                ChangeState(RMState.State3);
                                                            }
                                                            else // CD was far from any robot.
                                                            {
                                                                RM5Start(e.Position);
                                                                ChangeState(RMState.State3);
                                                            }
                                                        }
                                                        break;
                                                    case RMState.State3:

                                                        break;
                                                    case RMState.State4:
                                                        if (index != -1) // If the CD was on a robot then, start movement and reset to 1 robot selected.
                                                        {
                                                            // Reset to state RM1. Select the current robot.
                                                            if (!selectedList.Contains(index))
                                                            {
                                                                NoPulse();
                                                                HandOutWaypoints(); //"selected an unselected robot"
                                                                selectedList.Clear();
                                                                AddSelected(index, e);//"Tap"
                                                            }

                                                            ChangeState(RMState.State1);
                                                        }
                                                        else if ((index = CloseToRobot(e.Position)) != -1)
                                                        {
                                                            if (!selectedList.Contains(index))
                                                            {
                                                                NoPulse();
                                                                HandOutWaypoints(); //"selected an unselected robot"
                                                                selectedList.Clear();
                                                                AddSelected(index, e);
                                                            }

                                                            ChangeState(RMState.State1);
                                                        }
                                                        else // If the CD was on gound then...
                                                        {

                                                            RM5Start(e.Position);
                                                            ChangeState(RMState.State3);
                                                        }

                                                        break;
                                                    case RMState.State5:
                                                        break;
                                                    default:
                                                        break;
                                                }
                                            }
                                        });
        }

        private void Changed(Touch e)
        {
            joymgr.Change(e, (t, b) =>
                                            {
                                                if (!b)
                                                {
                                                    if (captureVis.ContainsKey(t.Id))
                                                    {
                                                        captureVis[t.Id].dot.Stroke = Brushes.White;
                                                        captureVis[t.Id].Update(t.Position);
                                                    }

                                                    int index = robotsCD(e);
                                                    Dictionary<int, Touch> cc = FREE;

                                                    if (FREE.Count == 1 && (state == RMState.State3 || em3m == null && state == RMState.State5 && (robotsCD(t) == -1 /* && menu != null && !Contacts.GetContactsOver(menu).Contains(args.Contact) */)))
                                                    {
                                                        {
                                                            WPDrag++;

                                                            if (state == RMState.State3)
                                                            {
                                                                turnedIntoDrag = true;
                                                                RM5Drag(t.Position);
                                                            }

                                                            if (state == RMState.State5)
                                                            {
                                                                AddWaypointDot(t.Position);
                                                            }
                                                        }
                                                    }

                                                    if (cc.Count > 1)
                                                    {
                                                        moveStuff(e);
                                                        if (!cleanedUpDragPoints.Contains(e.Id))
                                                        {
                                                            if (lassoPoints.Contains(e.Position))
                                                                lassoPoints.Remove(e.Position);
                                                            if (lassoLine.Points.Contains(e.Position))
                                                                lassoPoints.Remove(e.Position);
                                                            cleanedUpDragPoints.Add(e.Id);
                                                        }
                                                        return;
                                                    }

                                                    switch (state)
                                                    {
                                                        case RMState.Start:
                                                            if (cc.Count == 1 && (lassoId == -1 || lassoId == e.Id) && index == -1 &&
                                                                selectedList.Count == 0)
                                                            {
                                                                lassoPoints.Add(e.Position);
                                                                if (lassoId == -1)
                                                                {
                                                                    startLasso(e);
                                                                }
                                                                else
                                                                {
                                                                    addToLasso(e.Position);
                                                                }
                                                            }
                                                            break;
                                                        case RMState.State1:
                                                            if (cc.Count > 1) break;
                                                            /* indexdistfuck idf = DistToNearestGestureObject(e.Position); NEED TO FIX

                                                            if (selectedList.Count > 0 && selectedList.Contains(idf.index) &&
                                                                (idf.distance > ScaleDotToCameraHeight()))
                                                            {
                                                                AddWaypointDot(e.Position);
                                                                if (robots[idf.index].GetColor() == Brushes.Blue)
                                                                    PulseYellow(idf.index);
                                                                ChangeState(RMState.State5);
                                                                break;
                                                            }
                                                            else */if ((lassoId == -1 || lassoId == e.Id) && index == -1 &&
                                                                                 selectedList.Count == 0)
                                                            {
                                                                lassoPoints.Add(e.Position);
                                                                if (lassoId == -1)
                                                                {
                                                                    startLasso(e);
                                                                }
                                                                else
                                                                {
                                                                    addToLasso(e.Position);
                                                                }
                                                            }

                                                            break;
                                                        case RMState.State2:
                                                            break;
                                                        case RMState.State3:
                                                            if (cc.Count == 1 && index == -1)
                                                            {
                                                                if (selectedList.Count > 0)
                                                                {
                                                                    WPDrag++;
                                                                    turnedIntoDrag = true;
                                                                    RM5Drag(e.Position);
                                                                }
                                                                else
                                                                {
                                                                    RM5End();
                                                                    ChangeState(RMState.State1);
                                                                }
                                                            }

                                                            break;
                                                        case RMState.State4:
                                                            break;
                                                        case RMState.State5:
                                                            if (cc.Count == 1 && index == -1 )
                                                            {
                                                                WPDrag++;
                                                                AddWaypointDot(e.Position);
                                                            }
                                                            break;
                                                        default:
                                                            break;
                                                    }
                                                }
                                                else
                                                {
                                                    if (captureVis.ContainsKey(t.Id))
                                                    {
                                                        captureVis[t.Id].dot.Stroke = Brushes.Red;
                                                    }
                                                }
                                                
                                            });
        }

        private void Up(Touch e)
        {
            joymgr.Up(e, (t, b) =>
                                    {
                                        if (!b)
                                        {
                                            if (captureVis.ContainsKey(t.Id))
                                            {
                                                captureVis.Remove(captureVis[t.Id].DIEDIEDIE());
                                            }
                                            if (captureOldVis.ContainsKey(t.Id))
                                            {
                                                captureOldVis.Remove(t.Id);
                                            }
                                            if (cleanedUpDragPoints.Contains(e.Id)) cleanedUpDragPoints.Remove(e.Id);
                                            zoomUp();
                                            List<int> beforeLasso = new List<int>();
                                            List<int> newSelection = new List<int>();
                                            switch (state)
                                            {
                                                case RMState.Start:
                                                    beforeLasso.AddRange(selectedList);
                                                    FinishLasso(e);
                                                    
                                                    newSelection = selectedList.Except(beforeLasso).ToList();
                                                    if (newSelection.Count > 0)
                                                    {
                                                        ChangeState(RMState.State2);//, "CU (lasso done)"
                                                    }

                                                    break;
                                                case RMState.State1:
                                                    beforeLasso.AddRange(selectedList);
                                                    FinishLasso(e);
                                                    newSelection = selectedList.Except(beforeLasso).ToList();
                                                    if (newSelection.Count > 0)
                                                    {
                                                        ChangeState(RMState.State2); // "CU (lasso done)"
                                                    }
                                                    else
                                                    {
                                                        ChangeState(RMState.State2); // "CU"

                                                    }

                                                    break;
                                                case RMState.State2:
                                                    break;
                                                case RMState.State3:
                                                    if (WPDrag > 0)
                                                    {
                                                        WPDrag = 0;
                                                    }

                                                    ChangeState(RMState.State4); // "CU"
                                                    break;
                                                case RMState.State4:
                                                    break;
                                                case RMState.State5:
                                                    if (WPDrag > 0)
                                                    {
                                                        turnedIntoDrag = false;
                                                        WPDrag = 0;
                                                    }

                                                    if (robotsCD(e) == -1)
                                                    {
                                                        ChangeState(RMState.State4);// "CU-G"
                                                    }

                                                    break;
                                                default:
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            if (captureVis.ContainsKey(t.Id))
                                            {
                                                captureVis.Remove(captureVis[t.Id].DIEDIEDIE());
                                                captureOldVis.Remove(e.Id);
                                            }
                                            if(captureOldVis.ContainsKey(e.Id))
                                            {
                                                captureOldVis.Remove(e.Id);
                                            }
                                        }
                                    });
        }

        #region LassoUtility

        private int lassoId = -1;

        /// <summary>
        ///   The start lasso.
        /// </summary>
        /// <param name = "p">
        ///   The p.
        /// </param>
        private void startLasso(Touch e)
        {
            lassoId = e.Id;
            Point p = e.Position;


            // Start adding the new ones.
            lassoLine.Points.Add(p);
            if (lassoLine.Parent == null)
                MainCanvas.Children.Add(lassoLine);

            // Console.WriteLine("Oh yeah!");
        }

        /// <summary>
        ///   The add to lasso.
        /// </summary>
        /// <param name = "p">
        ///   The p.
        /// </param>
        private void addToLasso(Point p)
        {
            lassoLine.Points.Add(p);
        }

        /// <summary>
        ///   The end lasso.
        /// </summary>
        private void endLasso()
        {
            lassoId = -1;
            lassoPoints.Clear();
            lassoLine.Points.Clear();
            MainCanvas.Children.Remove(lassoLine);
        }

        /// <summary>
        ///   The finish lasso.
        /// </summary>
        private void FinishLasso(Touch e)
        {
            if (e.Id != lassoId) { endLasso(); return; }
            lassoId = -1;
            if (lassoPoints.Count != 0)
            {
                if (distance(lassoPoints[0], lassoPoints[lassoPoints.Count - 1]) < 100)
                {
                    for (int i = 0; i < numRobots; i++)
                    {
                        Point p = new Point((robots[i].xPos + (translate.X )) , (robots[i].yPos + (translate.Y))  );
                        if (PointInPoly(lassoPoints, p))
                        {
                            if (!selectedList.Exists(item => item == i))
                            {
                                selectedList.Add(i);
                                PulseYellow(i);
                            }
                        }
                    }
                }
            }

            lassoPoints.Clear();
            endLasso();
        }

        /// <summary>
        ///   The point in poly.
        /// </summary>
        /// <param name = "polygonPoints">
        ///   The polygon points.
        /// </param>
        /// <param name = "testPoint">
        ///   The test point.
        /// </param>
        /// <returns>
        ///   The point in poly.
        /// </returns>
        public static bool PointInPoly(List<Point> polygonPoints, Point testPoint)
        {
            bool isIn = false;
            int i, j = 0;
            for (i = 0, j = polygonPoints.Count - 1; i < polygonPoints.Count; j = i++)
            {
                if (
                    (((polygonPoints[i].Y <= testPoint.Y) && (testPoint.Y < polygonPoints[j].Y)) ||
                     ((polygonPoints[j].Y <= testPoint.Y) && (testPoint.Y < polygonPoints[i].Y))) &&
                    (testPoint.X <
                     (polygonPoints[j].X - polygonPoints[i].X) * (testPoint.Y - polygonPoints[i].Y) /
                     (polygonPoints[j].Y - polygonPoints[i].Y) + polygonPoints[i].X)
                    )
                {
                    isIn = !isIn;
                }
            }

            return isIn;
        }

        #endregion

        public void HandOutWaypoints()
        {
            lock (waypointDots)
            {
                if (waypointDots.Count == 0)
                    return;
            }

            List<Point> waypoints;
            int[] sel;
            Action asyncWaypointStuff;
            double newx;
            double newy;
            double newwx;
            double newwy;
            lock (translate)
            {
                newx = translate.X;
                newy = translate.Y;
            }
            lock (scale)
            {
                newwx = scale.ScaleX;
                newwy = scale.ScaleY;
            }
            lock (waypointDots)
            {
                waypoints = waypointDots.Keys.ToList();
                sel = selectedList.ToArray();
                asyncWaypointStuff = new Action(() =>
                {
                    foreach (int k in sel)
                    {
                        robots[k].updateWaypoints(waypoints, newx, newy, newwx, newwy);
                    }
                });
                foreach (Ellipse el in waypointDots.Values)
                    DotCanvas.Children.Remove(el);
                waypointDots.Clear();
            }
            asyncWaypointStuff.BeginInvoke((iar) =>
            {
                waypoints.Clear();
                waypoints = null;
                sel = null;
            }, null);

            NoPulse();
            selectedList.Clear();
        }

        public void EndState(string s)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                WPDrag = 0;
                ChangeState(RMState.Start);
                lock (waypointDots)
                {
                    foreach (Ellipse el in waypointDots.Values)
                       DotCanvas.Children.Remove(el);
                    waypointDots.Clear();
                }
                NoPulse();
                selectedList.Clear();
            }));
        }


        private void ZoomDown(Touch e)
        {
            if (FREE.Count == 1)
            {
                lassoDontThrowAway.AddRange(lassoPoints.Except(lassoDontThrowAway));
            }
            if (FREE.Count > 1)
            {
                foreach (Touch c in FREE.Values)
                {
                    if (!cleanedUpDragPoints.Contains(c.Id))
                    {
                        if (lassoPoints.Contains(c.Position))
                            lassoPoints.Remove(c.Position);
                        if (lassoLine.Points.Contains(c.Position))
                            lassoPoints.Remove(c.Position);
                        cleanedUpDragPoints.Add(c.Id);
                    }
                }
            }
        }

        private void zoomUp()
        {
            if (FREE.Count > 1)
            {
                lassoPoints = lassoPoints.Intersect(lassoDontThrowAway.AsEnumerable()).ToList();
            }
        }


        // List<Point> waypointList = new List<Point>();
        /// <summary>
        ///   Contains the list of all the waypoints that the robots currently selected will have to go to.
        /// </summary>
        /// <summary>
        ///   The menu that will be shown whenever wanted!.
        /// </summary>
        private int WPDrag;
        private Timer RM5Timer;
        private Point lastTouch;

        /// <summary>
        ///   The lasso dont throw away.
        /// </summary>
        private List<Point> lassoDontThrowAway = new List<Point>();

        private List<int> cleanedUpDragPoints = new List<int>();
        /// <summary>
        ///   The r m 5 drag.
        /// </summary>
        /// <param name = "last">
        ///   The last.
        /// </param>
        public void RM5Drag(Point last)
        {
            if (state != RMState.State5)
                ChangeState(RMState.State5);
            if (timers.IsRunning(ref RM5Timer))
            {
                AddWaypointDot(lastTouch);
                RM5End();
            }

            lastTouch = last;
            timers.StartTimer(ref RM5Timer);
        }

        #region Nested type: SlipAndSlide

        public class SlipAndSlide
        {
            public Ellipse dot;
            private int id;
            private TranslateTransform trans = new TranslateTransform();

            public SlipAndSlide(Touch e)
            {
                id = e.Id;
                dot = new Ellipse
                          {
                              Stroke = Brushes.White,
                              StrokeThickness = 3,
                              Width = 50,
                              Height = 50,
                              RenderTransform = trans,
                              IsHitTestVisible = false
                          };
                current.MainCanvas.Children.Add(dot);
                Update(e.Position);
            }

            public int DIEDIEDIE()
            {
                current.MainCanvas.Children.Remove(dot);
                return id;
            }

            public void Update(Point p)
            {
                trans.X = (p.X - 25);
                trans.Y = (p.Y - 25);
            }
        }

        #endregion

    }
}