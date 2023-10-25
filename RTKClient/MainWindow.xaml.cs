using RTKClientLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DualSenseAPI;
using System.Timers;
using System.Runtime.Intrinsics.X86;
using System.Numerics;
using Emgu.CV;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Reactive.Linq;
using System.Collections.ObjectModel;
using System.Drawing;
using Emgu.CV.Structure;
using System.Windows.Media.Media3D;
using Emgu.CV.CvEnum;
using System.Security.Cryptography;
using static System.Net.Mime.MediaTypeNames;

namespace RTKClient
{
    public class MomontsInfo
    {
        public Moments moments;
        public Vector2 center;
        public double array;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Connection con;
        DualSense sense;
        Timer timer;
        float middle = 250f;
        VideoCapture cam;
        readonly ObservableCollection<string> QRs = new();
        bool LineMove = false;
        bool LastSquareButton = false;
        QRCodeDetector detector = new QRCodeDetector();
        float ax = 0;
        float ay = 0;
        short last_l = 0;
        short last_r = 0;

        public MainWindow()
        {
            InitializeComponent();

            
            cam = new VideoCapture(0);
            cam.ImageGrabbed += Cam_ImageGrabbed;
            cam.Start();
            list_qr.ItemsSource = QRs;
        }

        private void Cam_ImageGrabbed(object? sender, EventArgs e)
        {
            var frame = cam.QueryFrame();
            
            if (LineMove)
            {
                try
                {
                    var im = frame.ToImage<Gray, Byte>();
                    im.ROI = new System.Drawing.Rectangle(0, frame.Rows - 50, frame.Cols, 50);
                    var bim = im.SmoothBlur(3, 3);
                    var bin_im = bim.ThresholdBinaryInv(new Gray(50), new Gray(255));
                    Emgu.CV.Util.VectorOfVectorOfPoint contours = new Emgu.CV.Util.VectorOfVectorOfPoint();
                    Mat hier = new Mat();
                    CvInvoke.FindContours(bin_im, contours, hier, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                    if (contours.Size > 0)
                    {
                        
                        List<MomontsInfo> moments = new List<MomontsInfo>();
                        for (int i = 0; i < contours.Size; i++)
                        {
                            var array = CvInvoke.ContourArea(contours[i]);
                            if (array > 1000)
                            {
                                var m = CvInvoke.Moments(contours[i]);
                                int x = (int)(m.M10 / m.M00);
                                int y = (int)(m.M01 / m.M00);
                                moments.Add(new MomontsInfo() { moments = m, center = new Vector2(x, y), array = array });
                            }
                        }
                        CvInvoke.DrawContours(frame, contours, -1, new MCvScalar(255, 0, 0), 2);
                        if (moments.Count > 0)
                        {
                            var ac = moments[0].array;
                            Vector2 c = moments[0].center;
                            if (moments.Count > 1)
                            {
                                for (int i = 1; i < moments.Count; i++)
                                {
                                    var m2 = moments[i];
                                    c = Vector2.Lerp(c, m2.center, (float)(ac > m2.array ? (m2.array / ac) : (1 - (ac / m2.array))));
                                    ac += m2.array;
                                }
                            }
                            CvInvoke.Circle(frame, new System.Drawing.Point((int)c.X, (int)c.Y), 2, new MCvScalar(0, 255, 0), 2);
                            ay = 0.15f;
                            var cf = (int)(frame.Cols / 2);
                            var dif = c.X - cf;
                            ax = (dif / cf) / 2f;
                        }
                        else
                        {
                            ay = 0.2f;
                            ax = 0f;
                        }
                    }
                    //Image<Bgr, Byte> cropped_im = im.Copy();
                    //var r = new Mat(frame, new System.Drawing.Rectangle(0, frame.Rows - 50, frame.Cols, 50));
                    //Image<Gray, Byte> grayImage = new Image<Gray, byte>(cropped_im.bi);
                    //var ggray = grayImage.CopyBlank();
                    //CvInvoke.GaussianBlur(grayImage, ggray, new System.Drawing.Size(13, 13), 1.5, 1);
                    //var bgray = ggray.CopyBlank();
                    //CvInvoke.Threshold(ggray, bgray, 200, 255, ThresholdType.Binary);
                    Dispatcher.Invoke(() =>
                    {
                        line_im_out.Source = bin_im.ToBitmapSource();
                    });
                }
                catch (Exception ex) 
                { }
            }
            else
            {
                var qrs = detector.DetectAndDecodeMulti(frame);
                if (qrs.Length > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        QRs.Clear();
                        foreach (var qr in qrs)
                        {
                            QRs.Add(qr.DecodedInfo);
                            CvInvoke.Polylines(frame, qr.Points.Select(x => new System.Drawing.Point(Convert.ToInt32(x.X), Convert.ToInt32(x.Y))).ToArray(), true, new Emgu.CV.Structure.MCvScalar(50, 200, 50), 2);
                            //CvInvoke.Rectangle(frame, new System.Drawing.Rectangle(qr.), new Emgu.CV.Structure.MCvScalar(50, 200, 50), 2);
                        }
                    });
                }
            }
            

            Dispatcher.Invoke(() =>
            {
                video_out.Source = frame.ToBitmapSource();
            });
        }

        private void Sense_OnStatePolled(DualSense sender)
        {
            float x = 0;
            float y = 0;
            if (sender.InputState.SquareButton != LastSquareButton && sender.InputState.SquareButton)
            {
                
                LineMove = !LineMove;
                ax = 0;
                ay = 0;
            }
            LastSquareButton = sender.InputState.SquareButton;
            if (LineMove)
            {
                x = ax;
                y = ay;
            }
            else
            {
                x = sender.InputState.RightAnalogStick.X;
                y = sender.InputState.RightAnalogStick.Y;
                x = MathF.Tan((MathF.Abs(x) < 0.1f ? 0 : x) * 1.25f) / 3f;
                y = MathF.Tan((MathF.Abs(y) < 0.1f ? 0 : y) * 1.25f) / 3f;
            }
            Vector2 xy = new Vector2(x, y);
            if (xy.Length() > 1) xy = Vector2.Normalize(xy);
            Int16 m = Convert.ToInt16(y * middle);
            Int16 d = Convert.ToInt16(x * middle);
            Int16 l = m;
            Int16 r = m;
            
            l += d;
            r -= d;
            if (r < -middle)
            {
                var s = Convert.ToInt16(Math.Abs(r) - middle);
                r += s;
                l += s;
            }
            if (r > middle)
            {
                var s = Convert.ToInt16(r - middle);
                r -= s;
                l -= s;
            }
            if (l < -middle)
            {
                var s = Convert.ToInt16(Math.Abs(l) - middle);
                l += s;
                r += s;
            }
            if (l > middle)
            {
                var s = Convert.ToInt16(l - middle);
                l -= s;
                r -= s;
            }
            l += 250;
            r += 250;
            Dispatcher.Invoke(() =>
            {
                //textBlock.Text = str;
                textBlockX.Text = x.ToString();
                textBlockY.Text = y.ToString();
            });
            if (l != last_l || r != last_r)
            {
                var lb = BitConverter.GetBytes(l);
                var rb = BitConverter.GetBytes(r);
                string str = $"r: {MathF.Round(r).ToString().PadLeft(4, '0')}, l: {MathF.Round(l).ToString().PadLeft(4, '0')}";
                con.SendCommand(new byte[] { lb[0], lb[1], rb[0], rb[1], rb[0], rb[1], rb[0], rb[1], lb[0], lb[1], lb[0], lb[1], 10 });
                
                last_l = l;
                last_r = r;
            }
        }

        private void Sense_OnButtonStateChanged(DualSense sender, DualSenseAPI.State.DualSenseInputStateButtonDelta changes)
        {
            //throw new NotImplementedException();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            short p = 350;
            var b = BitConverter.GetBytes(p);
            con.SendCommand(new byte[] { b[0], b[1], b[0], b[1], b[0], b[1], b[0], b[1], b[0], b[1], b[0], b[1], 10 });
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            short p = 150;
            var b = BitConverter.GetBytes(p);
            con.SendCommand(new byte[] { b[0], b[1], b[0], b[1], b[0], b[1], b[0], b[1], b[0], b[1], b[0], b[1], 10 });
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            con.SendTestMessage();
        }

        private void btn_conn_Click(object sender, RoutedEventArgs e)
        {
            con = new(com_port.Text);
            sense = DualSense.EnumerateControllers().First();
            sense.Acquire();
            sense.BeginPolling(2);
            sense.OnButtonStateChanged += Sense_OnButtonStateChanged;
            sense.OnStatePolled += Sense_OnStatePolled;
        }
    }
}
