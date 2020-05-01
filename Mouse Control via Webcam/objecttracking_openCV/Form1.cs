using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using System.Runtime.InteropServices;


namespace objecttracking_openCV
{
    public partial class Form1 : Form
    {

        Capture CapWebCam;
        Seq<Point> Hull;
        Image<Bgr, byte> imgOrignal;
        Seq<MCvConvexityDefect> defects;
        MCvConvexityDefect[] defectArray;
        MCvBox2D box;
        MemStorage storage = new MemStorage();
        
        bool button_pressed = false;


        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
		//trying to capture a vedio input device
            try
            {
                CapWebCam = new Capture();                 
            }
            catch (NullReferenceException except)
            {
                //label3.Text = except.Message;
                return;
            }
		
            Application.Idle += ProcessFramAndUpdateGUI;
        }
		
		//transferring mouse control to webcam
        private void btmPauseOrResum_Click(object sender, EventArgs e)
        {                   
            if (!button_pressed)
            {
                button_pressed = true;                                              // Set Flag
                btmPauseOrResum.Text = "Shift Mouse Control to Mouse Device";       // Change button text
            }
            else
            {
                btmPauseOrResum.Text = "Shift Mouse Control to Webcam";             // Change text
                button_pressed = false;                                             // Unset flag
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (CapWebCam != null)
            {
                CapWebCam.Dispose();
            }
        }

        void ProcessFramAndUpdateGUI(object Sender, EventArgs agr)
        {
            int Finger_num = 0;
            Double Result1 = 0;
            Double Result2 = 0;
		//querying image
            imgOrignal = CapWebCam.QueryFrame();

            if (imgOrignal == null) return;
            //Applying YCrCb filter
            Image<Ycc, Byte> currentYCrCbFrame = imgOrignal.Convert<Ycc, byte>();
            Image<Gray, byte> skin = new Image<Gray, byte>(imgOrignal.Width, imgOrignal.Height);
            
            skin = currentYCrCbFrame.InRange(new Ycc(0, 131, 80), new Ycc(255, 185, 135));
            
            StructuringElementEx rect_12 = new StructuringElementEx(10, 10, 5, 5, Emgu.CV.CvEnum.CV_ELEMENT_SHAPE.CV_SHAPE_RECT);
		//Eroding the source image using the specified structuring element
            CvInvoke.cvErode(skin, skin, rect_12, 1);
            
            StructuringElementEx rect_6 = new StructuringElementEx(6, 6, 3, 3, Emgu.CV.CvEnum.CV_ELEMENT_SHAPE.CV_SHAPE_RECT);
		//dilating the source image using the specified structuring element
            CvInvoke.cvDilate(skin, skin, rect_6, 2);
            
            skin = skin.Flip(FLIP.HORIZONTAL);
		//smoothing the filterd , eroded and dilated image.
            skin = skin.SmoothGaussian(9);
            
            imgOrignal = imgOrignal.Flip(FLIP.HORIZONTAL);
            	//extracting contours.
            Contour<Point> contours = skin.FindContours();
		
            Contour<Point> biggestContour = null;
            	//extracting the biggest contour.
            while (contours != null)
            {

                Result1 = contours.Area;
                if (Result1 > Result2)
                {
                    Result2 = Result1;
                    biggestContour = contours;
                }
                contours = contours.HNext;
            }
		//applying convexty defect allgoritm to find the count of fingers
            if (biggestContour != null)
            {
                Finger_num = 0;
                
                biggestContour = biggestContour.ApproxPoly((0.00025));
                imgOrignal.Draw(biggestContour, new Bgr(Color.LimeGreen), 2);
                
                Hull = biggestContour.GetConvexHull(ORIENTATION.CV_CLOCKWISE);
                defects = biggestContour.GetConvexityDefacts(storage, ORIENTATION.CV_CLOCKWISE);
                imgOrignal.DrawPolyline(Hull.ToArray(), true, new Bgr(0, 0, 256), 2);
                
                box = biggestContour.GetMinAreaRect();
                
                defectArray = defects.ToArray();
                
                for (int i = 0; i < defects.Total; i++)
                {
                    PointF startPoint = new PointF((float)defectArray[i].StartPoint.X,
                                                (float)defectArray[i].StartPoint.Y);

                    PointF depthPoint = new PointF((float)defectArray[i].DepthPoint.X,
                                                    (float)defectArray[i].DepthPoint.Y);

                    PointF endPoint = new PointF((float)defectArray[i].EndPoint.X,
                                                    (float)defectArray[i].EndPoint.Y);

                    
                    CircleF startCircle = new CircleF(startPoint, 5f);
                    CircleF depthCircle = new CircleF(depthPoint, 5f);
                    CircleF endCircle = new CircleF(endPoint, 5f);
                    
                    
                    if (    (startCircle.Center.Y < box.center.Y || depthCircle.Center.Y < box.center.Y) && 
                            (startCircle.Center.Y < depthCircle.Center.Y) && 
                            (Math.Sqrt(Math.Pow(startCircle.Center.X - depthCircle.Center.X, 2) + 
                                       Math.Pow(startCircle.Center.Y - depthCircle.Center.Y, 2)) > 
                                       box.size.Height / 6.5)   )
                    {
                        Finger_num++;
                    }
                }
                
                label2.Text = Finger_num.ToString();            // updating finger count
            }

            // Finding the center of contour

            MCvMoments moment = new MCvMoments();               // a new MCvMoments object

            try
            {
                moment = biggestContour.GetMoments();           // Moments of biggestContour
            }
            catch (NullReferenceException except)
            {
                //label3.Text = except.Message;
                return;
            }

            CvInvoke.cvMoments(biggestContour, ref moment, 0);
            
            double m_00 = CvInvoke.cvGetSpatialMoment(ref moment, 0, 0);
            double m_10 = CvInvoke.cvGetSpatialMoment(ref moment, 1, 0);
            double m_01 = CvInvoke.cvGetSpatialMoment(ref moment, 0, 1);

            int current_X = Convert.ToInt32(m_10 / m_00) / 10;      // X location of centre of contour              
            int current_Y = Convert.ToInt32(m_01 / m_00) / 10;      // Y location of center of contour
            
            // transfer control to webcam only if button has already been clicked

            if (button_pressed)                 
            {

                // move cursor to center of contour only if Finger count is 1 or 0
                // i.e. palm is closed

                if (Finger_num == 0 || Finger_num == 1)
                {
                    Cursor.Position = new Point(current_X * 20, current_Y * 20);   
                }          
                
                // Leave the cursor where it was and Do mouse click, if finger count >= 4
                
                if (Finger_num >= 4)
                {
                    DoMouseClick();                     // function clicks mouse left button
                }
            }
            
            iborignal.Image = imgOrignal;               // display orignal image
        }

        
        // function for mouse clicks

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData,
           int dwExtraInfo);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;          // mouse left button pressed 
        private const int MOUSEEVENTF_LEFTUP = 0x04;            // mouse left button unpressed
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;         // mouse right button pressed
        private const int MOUSEEVENTF_RIGHTUP = 0x10;           // mouse right button unpressed

        //this function will click the mouse using the parameters assigned to it
        public void DoMouseClick()
        {
            //Call the imported function with the cursor's current position
            uint X = Convert.ToUInt32(Cursor.Position.X);
            uint Y =Convert.ToUInt32(Cursor.Position.Y);
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, X, Y, 0, 0);
        }         
    }
}
