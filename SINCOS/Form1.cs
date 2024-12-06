using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;


namespace SINCOS
{
    public partial class Form1 : Form
    {
        private TcpListener _tcpListener;
        private Thread _listenerThread;
        private TcpClient _connectedClient; // Store the connected client
        private NetworkStream _stream; // Store the network stream
        private bool _isSendingData; // Track sending state
        private List<List<double>> channelData = new List<List<double>>();
        private List<List<float>> sinData = new List<List<float>>();
        private List<List<float>> cosData = new List<List<float>>();
        private int count = 0;
        private bool _isZooming = false;
        private Point _startPoint;
        private Rectangle _selectionRectangle;

        public class PacketHeader
        {
            public uint ID0 { get; set; }
            public uint ID1 { get; set; }
            public uint FrameCount { get; set; }
            public uint Length { get; set; }
        }
        public Form1()
        {
            InitializeComponent();
            _isSendingData = false; // Initial state is not sending
            buttonsend.Text = "启动数据传输"; // Initial button text
            textBox1.Multiline = true; // Enable multiline
            textBox1.ScrollBars = ScrollBars.Vertical; // Add vertical scroll bar
            // Initialize the channelData lists
            channelData.Add(new List<double>());
            sinData.Add(new List<float>());
            cosData.Add(new List<float>());
            // Enable mouse events for the chart
            this.chart2.MouseDown += Chart2_MouseDown;
            this.chart2.MouseMove += Chart2_MouseMove;
            this.chart2.MouseUp += Chart2_MouseUp;
            this.chart2.Paint += Chart2_Paint; // To draw the selection rectangle
            this.chart2.MouseWheel += Chart2_MouseWheel; // Handle mouse wheel event
            this.chart2.MouseDoubleClick += Chart2_MouseDoubleClick; // Handle double-click event

            checkBox1.Checked = true;
            checkBox2.Checked = true;
            checkBox3.Checked = true;
            checkBox4.Checked = true;

            textBox2.Text = "0";
            textBox3.Text = "0";
            textBox2.TextAlign = HorizontalAlignment.Center;
            textBox3.TextAlign = HorizontalAlignment.Center;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            // 启动监听线程
            _listenerThread = new Thread(StartListening);
            _listenerThread.IsBackground = true;
            _listenerThread.Start();
        }

        private void StartListening()
        {
            try
            {
                // 创建 TCP 监听器
                _tcpListener = new TcpListener(IPAddress.Any, 5001);
                _tcpListener.Start();
                Invoke(new Action(() => textBox1.AppendText("Server started, waiting for connections...\r\n")));

                while (true)
                {
                    // Accept client connection
                    _connectedClient = _tcpListener.AcceptTcpClient();
                    _stream = _connectedClient.GetStream();
                    Invoke(new Action(() => textBox1.AppendText("Client connected.\r\n")));
                    // Handle client connection
                    Thread clientThread = new Thread(() => HandleClient(_connectedClient));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                Invoke(new Action(() => textBox1.AppendText($"Error: {ex.Message}\r\n")));
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[6176];
            int bytesRead;

            try
            {
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    // Read the packet header
                    PacketHeader header = new PacketHeader
                    {
                        ID0 = BitConverter.ToUInt32(buffer, 0),
                        ID1 = BitConverter.ToUInt32(buffer, 4),
                        FrameCount = BitConverter.ToUInt32(buffer, 12),
                        Length = BitConverter.ToUInt32(buffer, 12)
                    };

                    // Validate the header
                    if (header.ID0 == 0xAA55AA55 && header.ID1 == 0xAA55AA55)
                    {
                        // Extract the payload data
                        int[] payload = new int[header.Length/4];

                        Buffer.BlockCopy(buffer, 32, payload, 0, bytesRead-32);

                        // Process the payload (for example, display it graphically)
                        //DisplayData(payload);
                        UpdateDataPoints(payload, bytesRead - 32);

                        UpdateChart();

                        UpdateChart2();

                        Invoke(new Action(() => textBox1.AppendText($"Received valid packet: FrameCount={header.FrameCount}, Length={header.Length}\r\n")));
                    }
                    else
                    {
                        Invoke(new Action(() => textBox1.AppendText("Invalid packet header received.\r\n")));
                    }
                }
            }
            catch (Exception ex)
            {
                Invoke(new Action(() => textBox1.AppendText($"Client disconnected: {ex.Message}\r\n")));
            }
            finally
            {
                client.Close();
            }
        }

        private void UpdateDataPoints(int[] payload, int bytesRead)
        {
            List<List<double>> dataToSave_distance = new List<List<double>>();
            List<List<float>> dataToSave_sin = new List<List<float>>();
            List<List<float>> dataToSave_cos = new List<List<float>>();

            // Initialize the dataToSave lists
            dataToSave_distance.Add(new List<double>()); // For distance data
            dataToSave_sin.Add(new List<float>());    // For sine data
            dataToSave_cos.Add(new List<float>());    // For cosine data

            // Assuming payload and bytesRead are defined and populated elsewhere
            for (int i = 0; i < bytesRead / 12; i++) // Each iteration processes 12 bytes
            {
                // For dataToSave_distance (assuming it's the first part of the payload)
                int distanceValue = payload[i * 3]; // Get the distance value
                double convertedDistanceValue = distanceValue * 2.048 / 8192 * 1e-6; // Convert to double
                dataToSave_distance[0].Add(convertedDistanceValue);
                channelData[0].Add(convertedDistanceValue); // Assuming channelData[0] is for distance

                // Update textBox4 every 10 iterations
                if (i % 100 == 0)
                {
                    textBox4.Text = convertedDistanceValue.ToString("F9"); // Format to 6 decimal places
                }

                // For dataToSave_sin (assuming it's the second part of the payload)
                int sinValue = payload[i * 3 + 1]; // Get the sine value
                float sinFloatValue = BitConverter.ToSingle(BitConverter.GetBytes(sinValue), 0); // Convert to float
                dataToSave_sin[0].Add(sinFloatValue);
                sinData[0].Add(sinFloatValue);

                // For dataToSave_cos (assuming it's the third part of the payload)
                int cosValue = payload[i * 3 + 2]; // Get the cosine value
                float cosFloatValue = BitConverter.ToSingle(BitConverter.GetBytes(cosValue), 0); // Convert to float
                dataToSave_cos[0].Add(cosFloatValue);
                cosData[0].Add(cosFloatValue);

                // Limit channelData[0] to the most recent 10 data points
                if (channelData[0].Count > 2048)
                {
                    channelData[0].RemoveAt(0); // Remove the oldest data point
                }

                // Limit sinData[0] to the most recent 10 data points
                if (sinData[0].Count > 2048)
                {
                    sinData[0].RemoveAt(0); // Remove the oldest data point
                }

                // Limit sinData[0] to the most recent 10 data points
                if (cosData[0].Count > 2048)
                {
                    cosData[0].RemoveAt(0); // Remove the oldest data point
                }

            }
        }

        public void UpdateChart2()
        {
            chart2.Invoke(new Action(() =>
            {
                // Set X and Y axis ranges
                chart2.ChartAreas[0].AxisY.Minimum = -1.5;
                chart2.ChartAreas[0].AxisY.Maximum = 1.5;

                // Clear existing points for all series
                chart2.Series[0].Points.Clear();
                chart2.Series[1].Points.Clear();
                chart2.Series[2].Points.Clear();

                int dataPointsCount = Math.Min(channelData[0].Count, Math.Min(sinData[0].Count, cosData[0].Count));

                // Add points to the chart for the available data
                for (int j = 0; j < dataPointsCount; j++)
                {
                    chart2.Series[0].Points.AddY(channelData[0][j]); // Distance data
                    chart2.Series[1].Points.AddY(sinData[0][j]);     // Sine data
                    chart2.Series[2].Points.AddY(cosData[0][j]);     // Cosine data
                }

                // Hide series if their corresponding checkbox is not checked
                chart2.Series[0].Enabled = checkBox2.Checked;
                chart2.Series[1].Enabled = checkBox3.Checked;
                chart2.Series[2].Enabled = checkBox4.Checked;

                chart2.Invalidate();
            }));
        }

        public void UpdateChart()
        {
            chart1.Invoke(new Action(() =>
            {
                // Clear existing points if needed
                chart1.Series[0].Points.Clear();

                // Set X and Y axis ranges
                chart1.ChartAreas[0].AxisX.Minimum = -1;
                chart1.ChartAreas[0].AxisX.Maximum = 1;
                chart1.ChartAreas[0].AxisY.Minimum = -1;
                chart1.ChartAreas[0].AxisY.Maximum = 1;

                // Add points for the 2D plot using sinData and cosData
                for (int j = 0; j < Math.Min(sinData[0].Count, cosData[0].Count); j++) // Ensure you're accessing the correct list
                {
                    chart1.Series[0].Points.AddXY(sinData[0][j], cosData[0][j]); // X = sinData[0], Y = cosData[0]
                }

                // Hide series if their corresponding checkbox is not checked
                chart1.Series[0].Enabled = checkBox1.Checked; // This series is now the 2D plot
                chart1.Invalidate();
            }));
            // Additional logic for managing data points can be added here if needed
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _tcpListener?.Stop();
            _listenerThread?.Abort();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (_isSendingData)
            {
                // Stop sending data
                SendData(0xB1FF55AA);
                buttonsend.Text = "启动数据传输"; // Change button text
                Invoke(new Action(() => textBox1.AppendText("Stop Sending Data......\r\n")));
            }
            else
            {
                // Start sending data
                SendData(0xA0FF55AA);
                buttonsend.Text = "停止数据传输"; // Change button text
                Invoke(new Action(() => textBox1.AppendText("Start Sending Data......\r\n")));
            }

            _isSendingData = !_isSendingData; // Toggle the state
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            SendData(0xC1FF55AA);
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            textBox1.Clear();
        }

        private void SendData(uint data)
        {
            if (_stream != null && _stream.CanWrite)
            {
                byte[] bytesToSend = BitConverter.GetBytes(data);
                _stream.Write(bytesToSend, 0, bytesToSend.Length);
                _stream.Flush();
                //Invoke(new Action(() => textBox1.AppendText($"Sent: {data:X}\n")));
            }
            else
            {
                //Invoke(new Action(() => textBox1.AppendText("No client connected to send data.\n")));
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SendData(0xAA55FFC1);
            Invoke(new Action(() => textBox1.AppendText("Reset Sending Data......\r\n")));
        }

        private void Chart2_MouseDown(object sender, MouseEventArgs e)
        {
            // Start zooming when the left mouse button is pressed
            if (e.Button == MouseButtons.Left)
            {
                _isZooming = true;
                _startPoint = e.Location; // Store the starting point of the zoom
                _selectionRectangle = new Rectangle(e.Location.X, e.Location.Y, 0, 0); // Initialize the rectangle
            }
        }

        private void Chart2_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isZooming)
            {
                // Update the selection rectangle dimensions
                _selectionRectangle.Width = e.Location.X - _startPoint.X;
                _selectionRectangle.Height = e.Location.Y - _startPoint.Y;

                // Invalidate the chart to trigger a repaint
                chart2.Invalidate();
            }
        }

        private void Chart2_MouseUp(object sender, MouseEventArgs e)
        {
            // Stop zooming when the left mouse button is released
            if (_isZooming)
            {
                _isZooming = false;

                // Calculate the new axis limits for X-axis and Y-axis
                double minX = chart2.ChartAreas[0].AxisX.PixelPositionToValue(_selectionRectangle.Left);
                double maxX = chart2.ChartAreas[0].AxisX.PixelPositionToValue(_selectionRectangle.Right);
                double minY = chart2.ChartAreas[0].AxisY.PixelPositionToValue(_selectionRectangle.Bottom);
                double maxY = chart2.ChartAreas[0].AxisY.PixelPositionToValue(_selectionRectangle.Top);

                // Adjust the limits based on the data points within the selected area
                AdjustAxisLimits(minX, maxX, minY, maxY);

                // Clear the selection rectangle
                _selectionRectangle = Rectangle.Empty;

                // Refresh the chart to show the new limits
                chart2.Invalidate();
            }
        }

        private void AdjustAxisLimits(double minX, double maxX, double minY, double maxY)
        {
            // Find the points within the selected area
            foreach (var series in chart2.Series)
            {
                foreach (var point in series.Points)
                {
                    if (point.XValue >= minX && point.XValue <= maxX && point.YValues[0] >= minY && point.YValues[0] <= maxY)
                    {
                        // Adjust the axis limits based on the points found
                        if (point.XValue < chart2.ChartAreas[0].AxisX.Minimum || double.IsNaN(chart2.ChartAreas[0].AxisX.Minimum))
                            chart2.ChartAreas[0].AxisX.Minimum = point.XValue;

                        if (point.XValue > chart2.ChartAreas[0].AxisX.Maximum || double.IsNaN(chart2.ChartAreas[0].AxisX.Maximum))
                            chart2.ChartAreas[0].AxisX.Maximum = point.XValue;

                        if (point.YValues[0] < chart2.ChartAreas[0].AxisY.Minimum || double.IsNaN(chart2.ChartAreas[0].AxisY.Minimum))
                            chart2.ChartAreas[0].AxisY.Minimum = point.YValues[0];

                        if (point.YValues[0] > chart2.ChartAreas[0].AxisY.Maximum || double.IsNaN(chart2.ChartAreas[0].AxisY.Maximum))
                            chart2.ChartAreas[0].AxisY.Maximum = point.YValues[0];
                    }
                }
            }
        }

        private void Chart2_Paint(object sender, PaintEventArgs e)
        {
            if (_isZooming)
            {
                // Draw the selection rectangle
                e.Graphics.DrawRectangle(Pens.Red, _selectionRectangle);
            }
        }

        private void Chart2_MouseWheel(object sender, MouseEventArgs e)
        {
            // Get the current mouse position relative to the chart area
            double mouseX = chart2.ChartAreas[0].AxisX.PixelPositionToValue(e.X);

            // Determine the zoom factor
            double zoomFactor = (e.Delta > 0) ? 0.9 : 1.1; // Zoom in or out

            // Calculate new limits
            double xRange = chart2.ChartAreas[0].AxisX.Maximum - chart2.ChartAreas[0].AxisX.Minimum;
            double newXMin = mouseX - (mouseX - chart2.ChartAreas[0].AxisX.Minimum) * zoomFactor;
            double newXMax = mouseX + (chart2.ChartAreas[0].AxisX.Maximum - mouseX) * zoomFactor;

            // Set new limits for the X-axis
            chart2.ChartAreas[0].AxisX.Minimum = newXMin;
            chart2.ChartAreas[0].AxisX.Maximum = newXMax;

            // Refresh the chart to show the new limits
            chart2.Invalidate();
        }

        private void Chart2_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // Reset the zoom level on double-click
            ResetZoom();
        }

        private void ResetZoom()
        {
            // Reset the axis limits to their default values
            chart2.ChartAreas[0].AxisX.Minimum = double.NaN; // Reset to default
            chart2.ChartAreas[0].AxisX.Maximum = double.NaN; // Reset to default
            chart2.ChartAreas[0].AxisY.Minimum = double.NaN; // Reset to default
            chart2.ChartAreas[0].AxisY.Maximum = double.NaN; // Reset to default

            // Refresh the chart
            chart2.Invalidate(); // Refresh the chart
        }

        private uint PrepareDataToSend(byte addr, short data)
        {
            // Construct the uint from the fixed header, addr, and data
            uint packet = 0x06; // Start with the fixed header
            packet = (packet << 8) | addr; // Shift left and add addr
            packet = (packet << 16) | (uint)data; // Shift left and add the short data

            return packet; // Return the constructed uint
        }
        private void button2_Click_1(object sender, EventArgs e)
        {
            // Prepare the data to send
            byte addr = 0x18; // Address for button 2
            short data = short.Parse(textBox2.Text); // Get the short data from textBox2
            uint dataToSend = PrepareDataToSend(addr, data); // Prepare the uint data to send

            // Send the data over TCP
            SendData(dataToSend); // Call the original SendData method
            Invoke(new Action(() => textBox1.AppendText($"Sent: 0x06 + {addr:X2} + {data}\r\n")));
        }

        private void button4_Click(object sender, EventArgs e)
        {
            // Prepare the data to send
            byte addr = 0x1c; // Address for button 4
            short data = short.Parse(textBox3.Text); // Get the short data from textBox3
            uint dataToSend = PrepareDataToSend(addr, data); // Prepare the uint data to send

            // Send the data over TCP
            SendData(dataToSend); // Call the original SendData method
            Invoke(new Action(() => textBox1.AppendText($"Sent: 0x06 + {addr:X2} + {data}\r\n")));
        }

        private void button5_Click(object sender, EventArgs e)
        {
            SendData(0xD1FF55AA);
        }
    }
}
