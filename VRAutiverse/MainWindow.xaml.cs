using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace VRAutiverse
{
    enum LauncherStatus
    {
        Ready,
        Failed,
        DownloadingGame,
        DownloadingUpdate
    }
    
    public partial class MainWindow : Window
    {
        private string rootPath;
        private string versionFile;
        private string gameZip;
        private string gameExe;

        private LauncherStatus _status;

        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.Ready:
                        PlayButton.Content = "Play";
                        break;
                    case LauncherStatus.Failed:
                        PlayButton.Content = "Failed - Retry";
                        break;
                    case LauncherStatus.DownloadingGame:
                        PlayButton.Content = "Downloading...";
                        break;
                    case LauncherStatus.DownloadingUpdate:
                        PlayButton.Content = "Downloading...";
                        break;
                    default:
                        break;
                }
            }
        }
        
        public MainWindow()
        {
            InitializeComponent();

            rootPath = Directory.GetCurrentDirectory();
            versionFile = Path.Combine(rootPath, "Version.txt");
            gameZip = Path.Combine(rootPath, "Build.zip");
            gameExe = Path.Combine(rootPath, "Build", "VR Autism.exe");
        }

        private void ExitBtn_Click(object sender, EventArgs e)
        {
            Close();
        }

        private UpdateConfig GetServerData()
        {
            try
            {
                var client = new HttpClient();

                var data = client.GetStringAsync("https://www.dropbox.com/scl/fi/f90jiwf9n70bv8so0q74k/UpdateConfig.json?rlkey=mzyev9n9je72zjpw3dgdgk2v2&st=zjeuzkyc&raw=1").Result;
                var config = JsonSerializer.Deserialize<UpdateConfig>(data);
                //MessageBox.Show($"Version: {config.Version}, URL: {config.ZipLink}");
                return config;

            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show("Error checking for game updates: " + ex);
            }

            

            return new UpdateConfig();
        }

        private void CheckForUpdates()
        {
            if (File.Exists(versionFile))
            {
                var localVersion = new Version(File.ReadAllText(versionFile));
                VersionText.Text = localVersion.ToString();

                //MessageBox.Show("Version Text" + localVersion);

                var config = GetServerData();

                var onlineVersion = new Version(config.Version);

                //MessageBox.Show("Online Version" + onlineVersion);

                if (onlineVersion.IsDifferentThan(localVersion))
                {
                    InstallGameFiles(false,config);
                }
                else
                {
                    Status = LauncherStatus.Ready;
                }
                   
               
            }
            else
            {
                var config = GetServerData();
                InstallGameFiles(false, config);
            }
        }



        private void InstallGameFiles(bool isUpdate, UpdateConfig config)
        {

            try
            {
                var webClient = new WebClient();
                if (isUpdate)
                {
                    Status = LauncherStatus.DownloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.DownloadingGame;
                }

                webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                webClient.DownloadFileAsync(
                    new Uri(config.ZipLink),
                    gameZip, config.Version);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show("Error installing game files: " +  ex);
            }
        }

        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            // Cập nhật giá trị của ProgressBar
            //cpb_uc.Value = e.ProgressPercentage;

            //cpb_uc.Visibility = Visibility.Visible;
            TimerLabel.Text = e.ProgressPercentage.ToString("0");

        }

        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                var onlineVersion = e.UserState != null ? e.UserState.ToString() : "1.0.0";
                ZipFile.ExtractToDirectory(gameZip, rootPath, true);
                File.Delete(gameZip);

                File.WriteAllText(versionFile, onlineVersion);

                VersionText.Text = onlineVersion;
                Status = LauncherStatus.Ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show("Error finishing download: " + ex);
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckForUpdates();
        }
        
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
           

            if (File.Exists(gameExe) && Status == LauncherStatus.Ready)
            {
                Start_Btn_Checked();
                var startInfo = new ProcessStartInfo(gameExe);
                startInfo.WorkingDirectory = Path.Combine(rootPath, "Build");
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.Failed)
            {
                CheckForUpdates();
            }
        }


        float totalTime = 1; // s

        private async void StartTimer()
        {
            cpb_uc.Visibility = Visibility.Visible;
            TimerLabel.Text = totalTime.ToString("0");

            for (var percent = 0; percent <= 100; percent++)
            {
                TimerLabel.Text = percent.ToString();
                
                await Task.Delay((int)totalTime * 10); // chờ 10 ms
            }

            PauseAnimation();
            //cpb_uc.Visibility = Visibility.Collapsed;
        }


        private void StartAnimation()
        {
            // Lấy Storyboard từ Resources
            var storyboard = (Storyboard)cpb_uc.Resources["ProgressBarAnimation"];

            // Truy cập PointAnimationUsingPath
            var pointAnimation = (PointAnimationUsingPath)storyboard.Children[0];
            pointAnimation.Duration = TimeSpan.FromSeconds(totalTime); // Thay đổi Duration thành 30 giây

            // Truy cập BooleanAnimationUsingKeyFrames
            var booleanAnimation = (BooleanAnimationUsingKeyFrames)storyboard.Children[1];
            booleanAnimation.Duration = TimeSpan.FromSeconds(totalTime); // Thay đổi Duration thành 30 giây

            // Kiểm tra số lượng KeyFrames
            if (booleanAnimation.KeyFrames.Count >= 2)
            {
                // Truy cập và thay đổi KeyTime cho từng KeyFrame
                var keyFrame1 = (DiscreteBooleanKeyFrame)booleanAnimation.KeyFrames[0];
                var keyFrame2 = (DiscreteBooleanKeyFrame)booleanAnimation.KeyFrames[1];

                // Thay đổi KeyTime
                keyFrame1.KeyTime = TimeSpan.FromSeconds(0); // Thay đổi KeyTime của keyFrame đầu tiên
                keyFrame2.KeyTime = TimeSpan.FromSeconds(totalTime / 2); // Thay đổi KeyTime của keyFrame thứ hai
            }


            ((Storyboard)cpb_uc.Resources["ProgressBarAnimation"]).Begin();
        }

        private void PauseAnimation()
        {
            ((Storyboard)cpb_uc.Resources["ProgressBarAnimation"]).Pause();
        }

        private void StopAnimation()
        {
            ((Storyboard)cpb_uc.Resources["ProgressBarAnimation"]).Stop();
        }

        private void Start_Btn_Checked()
        {
            StartTimer();
            StartAnimation();
        }

        //private void Start_Btn_Unchecked(object sender, RoutedEventArgs e)
        //{
        //    StopTimer();
        //    StopAnimation();
        //}

        //private void Uncheck_Stop(object sender, RoutedEventArgs e)
        //{
        //    Start_Btn.IsChecked = false;
        //}

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    class UpdateConfig
    {
        public string Version { get; set; }
        public string ZipLink { get; set; }
    }

    struct Version
    {
        internal static Version zero = new Version(0, 0, 0);

        private short major;
        private short minor;
        private short subMinor;

        internal Version(short major, short minor, short subMinor)
        {
            this.major = major;
            this.minor = minor;
            this.subMinor = subMinor;
        }

        internal Version(string version)
        {
            var versionString = version.Split('.');

            if (versionString.Length != 3)
            {
                major = 0;
                minor = 0;
                subMinor = 0;
                return;
            }

            major = short.Parse(versionString[0]);
            minor = short.Parse(versionString[1]);
            subMinor = short.Parse(versionString[2]);
        }

        internal bool IsDifferentThan(Version otherVersion)
        {
            return major != otherVersion.major ||
                   (major == otherVersion.major && minor != otherVersion.minor) ||
                   (major == otherVersion.major && minor == otherVersion.minor && subMinor != otherVersion.subMinor);
        }

        public override string ToString()
        {
            return major + "." + minor + "." + subMinor;
        }
    }
}