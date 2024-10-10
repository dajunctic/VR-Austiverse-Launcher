using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Windows;
using Path = System.IO.Path;

namespace VR_Autiverse
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
                        PlayButton.Content = "Update Failed - Retry";
                        break;
                    case LauncherStatus.DownloadingGame:
                        PlayButton.Content = "Downloading All File";
                        break;
                    case LauncherStatus.DownloadingUpdate:
                        PlayButton.Content = "Downloading Patch Update";
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
            gameExe = Path.Combine(rootPath, "Build", "VR Autiverse.exe");
        }

        private void CheckForUpdates()
        {
            if (File.Exists(versionFile))
            {
                var localVersion = new Version(File.ReadAllText(versionFile));
                VersionText.Text = localVersion.ToString();

                try
                {
                    var client = new HttpClient();

                    var onlineVersion = new Version(client.GetStringAsync("https://www.dropbox.com/scl/fi/svbmnoggkvrigknjcg3j1/Version.txt?rlkey=7leuki1jkm2y46kzjx88o1wag&st=jifcjxvi&dl=0").Result);

                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        InstallGameFiles(false, onlineVersion);
                    }
                    else
                    {
                        Status = LauncherStatus.Ready;
                    }
                    
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.Failed;
                    MessageBox.Show("Error checking for game updates: " + ex);
                }
            }
            else
            {
                InstallGameFiles(false, Version.zero);
            }
        }

        private void InstallGameFiles(bool isUpdate, Version onlineVersion)
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
                    onlineVersion = new Version(webClient.DownloadString(
                        "https://www.dropbox.com/scl/fi/svbmnoggkvrigknjcg3j1/Version.txt?rlkey=7leuki1jkm2y46kzjx88o1wag&st=jifcjxvi&dl=0"));

                }

                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                webClient.DownloadFileAsync(
                    new Uri("https://www.dropbox.com/scl/fi/7kd0hcq3nlnlkh65soa8f/Build.zip?rlkey=1gt9nfsqzhc7993ulylrshbda&st=4j6ddsx5&dl=0"),
                    gameZip, onlineVersion);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.Failed;
                MessageBox.Show("Error installing game files: " +  ex);
            }
        }
        
        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                var onlineVersion = ((Version)e.UserState).ToString();
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
            if (version.Length != 3)
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