using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MediaDevices;
using Newtonsoft.Json.Linq;
using System.Diagnostics;


namespace biliCache
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public class VideoInfo(int id, string name, string fullPath)
    {
        public int Id { get; set; } = id;
        public string Name { get; set; } = name;
        public string FullPath { get; set; } = fullPath;
        public bool? State { get; set; }
    }


    public partial class MainWindow : Window
    {
        private MediaDevice? _currentMediaDevice;


        private string _exportRootPath = Path.Combine(Environment.CurrentDirectory, "output");
        private string _tempRootPath = Path.Combine(Environment.CurrentDirectory, "temp");

        private string _androidPath = "\\内部存储设备\\Android\\data\\tv.danmaku.bili\\download";
        private List<VideoInfo> _videoInfos = new List<VideoInfo>();

        private List<MediaDevice> _mediaDevices = new List<MediaDevice>();


        public MainWindow()
        {
            InitializeComponent();
            Directory.CreateDirectory(_exportRootPath);
            Directory.CreateDirectory(_tempRootPath);
            RefuseAllDeviceId();
            DataGrid1.ItemsSource = null;
        }


        private void RefuseAllDeviceId()
        {
            ComboBox1.Items.Clear();


            _mediaDevices = MediaDevice.GetDevices().ToList();
            if (_mediaDevices.Any())
            {
                foreach (var mediaDevice in _mediaDevices)
                {
                    ComboBox1.Items.Add(mediaDevice.FriendlyName);
                }
            }
            else
            {
                MessageBox.Show("无法找到手机设备,请将手机通过usb连接至电脑,并设置为文件传输模式！");
            }
        }


        private void ButtonClickRefreshDevice(object sender, RoutedEventArgs e)
        {
            RefuseAllDeviceId();
        }


        private void ButtonClickExit(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("是否退出？", "退出", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                foreach (var mediaDevice in _mediaDevices)
                {
                    if (mediaDevice.IsConnected)
                    {
                        mediaDevice.Disconnect();
                    }
                }

                Environment.Exit(0);
            }
        }


        private void ButtonClickSelectFolder(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFolderDialog dialog = new();

            dialog.Multiselect = false;
            dialog.Title = "选择导出目录";
            dialog.FolderName = _exportRootPath;
            if (dialog.ShowDialog() == true)
            {
                _exportRootPath = dialog.FolderName;
            }

            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, delegate()
            {
                int failed = 0;

                foreach (var videoInfo in _videoInfos)
                {
                    try
                    {
                        string[] files = _currentMediaDevice.GetFiles(videoInfo.FullPath);
                        string audioFile = Path.Combine(videoInfo.FullPath, "audio.m4s");
                        string loaclAudioFile = Path.Combine(_tempRootPath, "audio.m4s");
                        string videoFile = Path.Combine(videoInfo.FullPath, "video.m4s");
                        string localVideoFile = Path.Combine(_tempRootPath, "video.m4s");
                        foreach (string file in files)
                        {
                            if (Path.GetFileName(file).StartsWith("audio."))
                            {
                                audioFile = Path.Combine(videoInfo.FullPath,
                                    Path.GetFileName(file));
                                loaclAudioFile =
                                    Path.Combine(_tempRootPath, Path.GetFileName(file));
                            }

                            if (Path.GetFileName(file).StartsWith("video."))
                            {
                                videoFile = Path.Combine(videoInfo.FullPath,
                                    Path.GetFileName(file));
                                localVideoFile =
                                    Path.Combine(_tempRootPath, Path.GetFileName(file));
                            }
                        }

                        DownloadFile(audioFile, loaclAudioFile);
                        DownloadFile(videoFile, localVideoFile);
                        string exportFileName = Path
                            .Combine(_exportRootPath, string.Format("{0}.mp4", videoInfo.Name)).Replace("\"", " ");
                        videoInfo.State = Convert2Mp4(loaclAudioFile, localVideoFile, exportFileName);
                        if (videoInfo.State == false)
                        {
                            failed += 1;
                        }

                        DataGrid1.ItemsSource = null;
                        DataGrid1.ItemsSource = _videoInfos;
                    }
                    catch (System.Runtime.InteropServices.COMException )
                    {
                        MessageBox.Show("与手机连接中断！");
                        return;
                    }
                }

                MessageBox.Show(string.Format("转换完成,失败{0}个", failed));
                Process.Start("explorer", $"\"{_exportRootPath}\"");
            });
        }

        private bool Convert2Mp4(string audio, string video, string mp4File)
        {
            Process process = new Process();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments =
                $"-i {audio} -i {video} -threads {Environment.ProcessorCount} -c copy \"{mp4File}\" -y";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                return true;
            }

            return false;
        }

        private void comboBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBox1.SelectedItem != null)
            {
                SearchButton.IsEnabled = true;
            }
        }

        private void DownloadFile(string remotePath, string localPath)
        {
            FileStream fileStream = File.Create(localPath);
            _currentMediaDevice.DownloadFile(remotePath, fileStream);
            fileStream.Flush();
            fileStream.Close();
        }

        private void search_button_Click(object sender, RoutedEventArgs e)
        {
            SearchButton.IsEnabled = false;
            foreach (var mediaDevice in _mediaDevices)
            {
                if (mediaDevice.FriendlyName == ComboBox1.Text)
                {
                    _currentMediaDevice = mediaDevice;
                    break;
                }
            }

            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, delegate()
            {
                _currentMediaDevice.Connect();
                if (!_currentMediaDevice.DirectoryExists(_androidPath))
                {
                    MessageBox.Show(string.Format("找不到Bilibli缓存目录{0}", _androidPath));
                }

                string[] cachePaths = _currentMediaDevice.GetDirectories(_androidPath);
                foreach (string cachesPath in cachePaths)
                {
                    string[] mediaRootPaths = _currentMediaDevice.GetDirectories(cachesPath);
                    if (mediaRootPaths.Length == 0)
                    {
                        continue;
                    }

                    string mediaRootPath = mediaRootPaths[0];
                    string entryFile = Path.Combine(mediaRootPath, "entry.json");
                    if (!_currentMediaDevice.FileExists(entryFile))
                    {
                        continue;
                    }

                    string localEntry = Path.Combine(_tempRootPath, "entry.json");
                    DownloadFile(entryFile, localEntry);

                    JObject entryJson = JObject.Parse(File.ReadAllText(localEntry));

                    if ((bool)entryJson["is_completed"])
                    {
                        string name = entryJson["title"]?.ToString();
                        VideoInfo videoInfo = new VideoInfo(_videoInfos.Count() + 1, name.Replace("\"", " "),
                            Path.Combine(mediaRootPath, (string)entryJson["type_tag"]));
                        videoInfo.Name = name;
                        _videoInfos.Add(videoInfo);
                        DataGrid1.ItemsSource = null;
                        DataGrid1.ItemsSource = _videoInfos;
                    }

                    if (File.Exists(localEntry))
                    {
                        File.Delete(localEntry);
                    }
                }

                SearchButton.IsEnabled = true;
                MessageBox.Show("缓存搜索完成！");
                if (_videoInfos.Any())
                {
                    ExportButton.IsEnabled = true;
                }
            });
        }
    }
}