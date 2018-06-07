using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.SoundIn;
using CSCore.Streams;
using Shockzam.Properties;

namespace Shockzam
{
    public partial class MainForm : Form
    {
        public delegate void StringArgReturningVoidDelegate(string text);

        public static Form instance;
        public static NotifyIcon notifyIcon = new NotifyIcon();
        private readonly ShazamClient client;

        public MainForm()
        {
            InitializeComponent();
            client = new ShazamClient();
            client.OnRecongnitionStateChanged += ShazamInt.ShazamStateChanged;
            instance = this;
            Icon = Resources.shazam_512;
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon.Icon = Icon;
            notifyIcon.Text = "Shockzam ⚡";
            notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
            notifyIcon.MouseClick += NotifyIcon_Click;
        }

        private void NotifyIcon_Click(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Right) != 0) GetData();
        }

        private async Task GetData()
        {
            //var ass = new Thread(Run);
            var loopback = await GetLoopbackAudio(5000);
            client.DoRecognition(loopback.GetBuffer(), MicrophoneRecordingOutputFormatType.PCM);
        }

    
        public async Task<MemoryStream> GetLoopbackAudio(int ms)
        {
            var Stream = new MemoryStream();
            using (WasapiCapture virtualaudiodev =
                new WasapiLoopbackCapture())
            {
                virtualaudiodev.Initialize();
                var soundInSource = new SoundInSource(virtualaudiodev) {FillWithZeros = false};
                var convertedSource = soundInSource.ChangeSampleRate(44100).ToSampleSource().ToWaveSource(16);
                using (convertedSource = convertedSource.ToMono())
                {
                    using (var waveWriter = new WaveWriter(Stream, convertedSource.WaveFormat))
                    {
                        soundInSource.DataAvailable += (s, e) =>
                        {
                            var buffer = new byte[convertedSource.WaveFormat.BytesPerSecond / 2];
                            int read;
                            while ((read = convertedSource.Read(buffer, 0, buffer.Length)) > 0)
                                waveWriter.Write(buffer, 0, read);
                        };
                        virtualaudiodev.Start();
                        Thread.Sleep(ms);
                        virtualaudiodev.Stop();
                    }
                }
            }

            return Stream;
        }

        public static void SetText(string text)
        {
            // InvokeRequired required compares the thread ID of the  
            // calling thread to the thread ID of the creating thread.  
            // If these threads are different, it returns true.  
            if (listBox1.InvokeRequired)
            {
                listBox1.Invoke(new MethodInvoker(delegate
                {
                    listBox1.Items.Add(text);

                    listBox1.Refresh();
                }));
            }
            else
            {
                listBox1.Items.Add(text);
            }
        }
        public static void SetStatus(string text)
        {
            // InvokeRequired required compares the thread ID of the  
            // calling thread to the thread ID of the creating thread.  
            // If these threads are different, it returns true.  
            if (label4.InvokeRequired)
            {
                label4.Invoke(new MethodInvoker(delegate
                {
                    label4.Text = text;

                }));
            }
            else
            {
                label4.Text = text;
            }
        }
        public static void SetBool(bool state)
        {
            // InvokeRequired required compares the thread ID of the  
            // calling thread to the thread ID of the creating thread.  
            // If these threads are different, it returns true.  
            if (button1.InvokeRequired)
            {
                button1.Invoke(new MethodInvoker(delegate { button1.Enabled = state; }));
            }
            else
            {
                button1.Enabled = state;
            }
        }




        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            notifyIcon.Visible = false;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                notifyIcon.Visible = true;
                ShowInTaskbar = false;
            }
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            SetStatus("Recording..");
            button1.Enabled = false;
            Task.Run(GetData);
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                Process.Start("https://www.youtube.com/results?search_query="+listBox1.SelectedItem.ToString());
            }
        }
    }
}