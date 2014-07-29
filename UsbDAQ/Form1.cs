using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ZedGraph;
using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;
using System.Threading;
using System.IO;

namespace UsbDAQ
{
    public partial class Form1 : Form
    {


        // the 2 arguments of the constructor are the vendor id and the product id of the device
        // (check them out in the device manager)

        public static UsbDeviceFinder MyUsbFinder = new UsbDeviceFinder(0x4242, 0x2);
        public static UsbDevice MyUsbDevice;

        GraphPane plot1;

        LineItem graphLine1;
        LineItem graphLine2;

        //PointPairList graphPoints1 = new PointPairList();
        //PointPairList graphPoints2 = new PointPairList();

        RollingPointPairList graphPoints1;
        RollingPointPairList graphPoints2;

        DateTime StartTime = new DateTime();
        DateTime MeasTime = new DateTime();
        TimeSpan ElapsedTime = new TimeSpan();

        UsbEndpointReader reader;

        StreamWriter fileWriter;

        // boolean showing if measurement is running or not
        bool measRunning=false;

        bool loggingEnabled = false;

        public Form1()
        {
            InitializeComponent();


        }


        private void InitUSB()
        {
            // the code below is based on the example code provided on the LibUsbDotNet website

            MyUsbDevice = UsbDevice.OpenUsbDevice(MyUsbFinder);

            if (MyUsbDevice == null)
            {
                throw new Exception("Device Not Found.");

            }
            else
            {
                toolStripStatusLabel1.Text = "Device found";
            }

            // If this is a "whole" usb device (libusb-win32, linux libusb-1.0)
            // it exposes an IUsbDevice interface. If not (WinUSB) the 
            // 'wholeUsbDevice' variable will be null indicating this is 
            // an interface of a device; it does not require or support 
            // configuration and interface selection.
            IUsbDevice wholeUsbDevice = MyUsbDevice as IUsbDevice;
            if (!ReferenceEquals(wholeUsbDevice, null))
            {
                // This is a "whole" USB device. Before it can be used, 
                // the desired configuration and interface must be selected.

                // Select config #1
                wholeUsbDevice.SetConfiguration(1);

                // Claim interface #0.
                wholeUsbDevice.ClaimInterface(0);
            }

            // open read endpoint 1.
            reader = MyUsbDevice.OpenEndpointReader(ReadEndpointID.Ep01);

            reader.DataReceived += (OnRxEndPointData);
            reader.DataReceivedEnabled = true;

        }
        private void OnRxEndPointData(object sender, EndpointDataEventArgs e)
        {

            double captureval1 = ((int)(e.Buffer[0] * 256 + e.Buffer[1]) * .0009988f);
            double captureval2 = ((int)(e.Buffer[2] * 256 + e.Buffer[3]) * .0009988f);

           
            MeasTime = DateTime.Now;

            ElapsedTime = MeasTime - StartTime;
            AddPoint(ElapsedTime.TotalMilliseconds * 0.001, captureval1, captureval2);
        }

        public void AddPoint(double x, double y, double z)
        {

            // apparently the OnRxEndPointData is running from a separate thread, making this
            // cumbersome invoke necessary

            if (zedGraphControl1.InvokeRequired)
            {
                zedGraphControl1.Invoke(
                  new ThreadStart(delegate
                {
                    // updating the graph
                    graphPoints1.Add(x, y);
                    graphPoints2.Add(x, z);
                    //zedGraphControl1.reset
                    zedGraphControl1.AxisChange();
                    zedGraphControl1.Refresh();

                    // setting the labels

                    dataLabel1.Text = y.ToString("0.000 V");
                    dataLabel2.Text = z.ToString("0.000 V");

                    // writing to file (if enabled)

                    if (loggingEnabled)
                    {
                        fileWriter.WriteLine(x.ToString("0.000")+"\t"+y.ToString("0.000")+"\t"+z.ToString("0.000"));
                        fileWriter.Flush();
                    }
                }));
            }
            else
            {
                //label1.Text = "some text changed from the form's thread";
            }

    }

        private void Form1_Load(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "Press Start button to start the measurement";
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {

            // disabling the OnRxEndPointData event to prevent an exception
            reader.DataReceivedEnabled = false;
 
        }

        private void InitGraph()
        {

            graphPoints1 = new RollingPointPairList(1000);
            graphPoints2 = new RollingPointPairList(1000);

            plot1 = zedGraphControl1.GraphPane;

            plot1.CurveList.Clear();

            graphLine1 = plot1.AddCurve("Ch1", graphPoints1, Color.Red, SymbolType.None);
            graphLine2 = plot1.AddCurve("Ch2", graphPoints2, Color.Blue, SymbolType.None);

            plot1.Title.Text = "";
            plot1.XAxis.Title.Text = "Time [s]";
            plot1.YAxis.Title.Text = "Voltage [V]";

            plot1.YAxis.MajorGrid.IsVisible = true;
            plot1.YAxis.Scale.MajorStep = 1;
            zedGraphControl1.AutoScroll = true;
            
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            loggingEnabled = loggingBox.Checked;

            if (measRunning)
            {
                
                measRunning = false;
                
                // disabling the OnRxEndPointData event to prevent an exception
                reader.DataReceived -= (OnRxEndPointData);
                reader.DataReceivedEnabled = false;

                if (loggingEnabled)
                {
                    fileWriter.Flush();
                    fileWriter.Dispose();
                }

                toolStripStatusLabel1.Text = "Measurement stopped";
            }
            else
            {

                if (loggingEnabled)
                {
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        fileWriter = new StreamWriter(saveFileDialog1.FileName);

                        fileWriter.AutoFlush = true;
                        fileWriter.WriteLine("Time [s]" + "\t" + "CH1 [V]" + "\t" + "CH2 [V]");
                    }
                    else
                        return;
                }

                measRunning = true;

                StartTime = DateTime.Now;
                
                InitGraph();
                InitUSB();

                toolStripStatusLabel1.Text = "Measurement running";

            }
        }

        private void dataLabel1_Click(object sender, EventArgs e)
        {

        }

        private void dataLabel2_Click(object sender, EventArgs e)
        {

        }
    }
}