using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ColorPickerWPF;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.Globalization;

namespace Slide_Video_Player
{
    public partial class MainWindow : Window
    {
        private Timer videoUpdateEvents = new Timer(); 

        private bool isFullscreen = false;
        private bool isPaused = false;
        private bool isFileListEnabled = false;
        private bool isKeyBindListEnabled = false; 

        private Border? previousBorder;
        private List<string> loadedVideoFiles = new List<string>();
        private bool loadedFilesFirstTime = true;
        private bool spareBind = false; 
        private int currentFileIndex = 0;
        private int currentVideoFileSelectedIndex = -1;

        private enum KeyBindSelected
        {
            None,
            Fullscreen,
            FullscreenEscape,
            BackFiveSeconds,
            GoFiveSeconds,
            PreviousVideo,
            NextVideo, 
            Pause
        }
        private KeyBindSelected bindSelected;
        class Settings
        {
            public List<string> VideoFiles { get; set; } = new List<string>(); 
            public Color BackgroundColor { get; set; } = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
            public Key[] Binds { get; set; } = { Key.F11, Key.Escape, Key.Left, Key.Right, Key.F6, Key.F8, Key.Space };
            public enum Language
            {
                English, 
                Russian,
                Ukrainian
            }
            public Language LanguageEnum { get; set; }
            public double AudioSound { get; set; } = .5f;
            public bool NewVideoAfterEnd { get; set; } = false; 
            public bool FullScreen { get; set; } = false;
            public bool IsVideoWide { get; set; } = true;
            public Settings()
            {
                CultureInfo userCulture = CultureInfo.InstalledUICulture;
                switch (userCulture.DisplayName)
                {
                    case "ru-RU":
                        LanguageEnum = Language.Russian;
                        break;
                    case "en-US":
                        LanguageEnum = Language.English;
                        break;
                    case "uk-UA":
                        LanguageEnum = Language.Ukrainian;
                        break;
                    default:
                        LanguageEnum = Language.English;
                        break;
                }
            }
        }

        private Settings settings = new Settings();
        public MainWindow()
        {
            InitializeComponent();


            // Creating JSON file 
            if (File.Exists(@"save.json"))
            {
                string json = File.ReadAllText("save.json");
                if (!string.IsNullOrEmpty(json))
                {
                    settings = JsonConvert.DeserializeObject<Settings>(json);
                }
            }
            else
            {
                FileStream stream = File.Create("save.json");
                stream.Close();
            }

            videoUpdateEvents.Interval = 1;
            videoUpdateEvents.Elapsed += VideoUpdateEvents_Elapsed;

            //Loading Sound Value
            //Loading background color
            //Loading fullscreen
            //Loading video resolution
            //Loading if video should play after finishing other
            //Loading translation
            if (settings != null)
            {

                volumeSlider.Value = settings.AudioSound;
                videoPlayer.Volume = volumeSlider.Value;
                volumeSliderPercent.Content = (volumeSlider.Value * 100).ToString("F1") + "%";
                entireBody.Background = new SolidColorBrush(settings.BackgroundColor);
                isFullscreen = !settings.FullScreen;
                FullScreenHandling();

                if (settings.IsVideoWide)
                {
                    IsPerfectWideRadio.IsChecked = false;
                    IsWideRadio.IsChecked = true;
                    videoPlayer.Stretch = Stretch.Fill;
                }
                else
                {   
                    IsPerfectWideRadio.IsChecked = true;
                    IsWideRadio.IsChecked = false;
                    videoPlayer.Stretch = Stretch.Uniform;
                }

                newVideoAfterEndCheckBox.IsChecked = settings.NewVideoAfterEnd;
                fullScreenButton.Content = settings.Binds[0].ToString();
                removeFullScreenButton.Content = settings.Binds[1].ToString();   
                pastFiveSecondsButton.Content = settings.Binds[2].ToString();
                nextFiveSecondsButton.Content = settings.Binds[3].ToString();
                previousVideoButtonBind.Content = settings.Binds[4].ToString();
                nextVideoButtonBind.Content = settings.Binds[5].ToString();
                pauseButtonBind.Content = settings.Binds[6].ToString();

                loadedVideoFiles = settings.VideoFiles;
                if (loadedVideoFiles.Count != 0)
                {
                    int index = 0;
                    foreach (var file in loadedVideoFiles)
                    {
                        if (!File.Exists(loadedVideoFiles[index]))
                        {
                            index++;
                            continue;
                        }
                        Dispatcher.BeginInvoke(() =>
                        {
                            Border border = new Border();
                            if (index > 0) border.Margin = new Thickness(0, 5, 0, 0);
                            border.Name = $"b{index}";
                            border.Background = new SolidColorBrush(Colors.White);
                            border.BorderThickness = new Thickness(1);
                            border.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD3, 0xD3, 0xD3)); // #FFD3D3D3
                            border.MouseDown += VideoFile_Clicked;
                            Label label = new Label();
                            FileInfo info = new FileInfo(file);
                            label.Content = info.Name;
                            label.FontSize = 14;
                            label.HorizontalContentAlignment = HorizontalAlignment.Center;
                            border.Child = label;
                            fileList.Children.Add(border);
                            index++;
                        });
                    }
                    LoadVideoAndPlay(loadedVideoFiles[0]);
                }
                comboBoxLanguage.SelectedIndex = (int)settings.LanguageEnum;
                Translation(settings.LanguageEnum);
            }

            if (videoPlayer.Source == null)
            {
                pauseButton.IsEnabled = false;
                nextVideoButton.IsEnabled = false;
                endOfVideoButton.IsEnabled = false;
                previousVideoButton.IsEnabled = false;
                fromStartVideoButton.IsEnabled = false;
                return;
            }

        }
   
        private void SaveJSONClass()
        {
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText("save.json", json);
        }

        private void ChangeBackground_Click(object sender, RoutedEventArgs e)
        {
            Color color; 
            bool ok = ColorPickerWindow.ShowDialog(out color);
            if (!ok)
            {
                return;
            }
            entireBody.Background = new SolidColorBrush(color);
            settings.BackgroundColor = color;
        }

        private async void VideoUpdateEvents_Elapsed(object? sender, ElapsedEventArgs e)
        {
            await Dispatcher.BeginInvoke(() =>
            {
                if (videoPlayer.Source == null)
                {
                    progressOfVideoSlider.IsEnabled = true;
                    videoUpdateEvents.Stop();
                }

                progressOfVideoSlider.IsEnabled = false;

                if (videoPlayer.NaturalDuration.HasTimeSpan)
                {
                    currentTime.Content = videoPlayer.Position.ToString(@"hh\:mm\:ss");
                    finalTime.Content = videoPlayer.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
                    progressOfVideoSlider.Value = videoPlayer.Position.Seconds;
                
                    long currentMediaTicks = videoPlayer.Position.Ticks;
                    long totalMediaTicks = videoPlayer.NaturalDuration.TimeSpan.Ticks;

                    progressOfVideoSlider.Maximum = totalMediaTicks;

                    if (totalMediaTicks > 0) progressOfVideoSlider.Value = currentMediaTicks;
                    else progressOfVideoSlider.Value = 0;

                    if (currentMediaTicks == totalMediaTicks && !settings.NewVideoAfterEnd)
                    {
                        progressOfVideoSlider.IsEnabled = true;
                        videoUpdateEvents.Stop();
                    }
                    else if (currentMediaTicks == totalMediaTicks && settings.NewVideoAfterEnd)
                    {
                        SwitchVideo(true);
                    }
                }

            });
        }

        private async void progressOfVideoSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await Dispatcher.BeginInvoke(() =>
            {
                if (videoPlayer.NaturalDuration.HasTimeSpan && isPaused)
                {
                    videoPlayer.Position = TimeSpan.FromTicks((long)progressOfVideoSlider.Value);
                    currentTime.Content = videoPlayer.Position.ToString(@"hh\:mm\:ss");
                    finalTime.Content = videoPlayer.NaturalDuration.TimeSpan.ToString();
                }
            });
        }

        private void Window_KeyDownInput(object sender, KeyEventArgs e)
        {
            if (isKeyBindListEnabled)
            {
                switch (bindSelected)
                {
                    case KeyBindSelected.None:
                        break;
                    case KeyBindSelected.Fullscreen:
                        fullScreenButton.Content = e.Key.ToString();
                        settings.Binds[0] = e.Key;
                        break;
                    case KeyBindSelected.FullscreenEscape:
                        removeFullScreenButton.Content = e.Key.ToString();
                        settings.Binds[1] = e.Key;
                        break;
                    case KeyBindSelected.BackFiveSeconds:
                        pastFiveSecondsButton.Content = e.Key.ToString();
                        settings.Binds[2] = e.Key;
                        break;
                    case KeyBindSelected.GoFiveSeconds:
                        nextFiveSecondsButton.Content = e.Key.ToString();
                        settings.Binds[3] = e.Key;
                        break;
                    case KeyBindSelected.PreviousVideo:
                        previousVideoButtonBind.Content= e.Key.ToString();
                        settings.Binds[4] = e.Key;
                        break;
                    case KeyBindSelected.NextVideo:
                        nextVideoButtonBind.Content = e.Key.ToString();
                        settings.Binds[5] = e.Key;
                        break;
                    case KeyBindSelected.Pause:
                        pauseButtonBind.Content = e.Key.ToString();
                        settings.Binds[6] = e.Key;
                        break;
                }
                return;
            }
            if (spareBind)
            {
                spareBind = false;
                return;
            }
            if (e.Key == settings.Binds[0])
            {
                FullScreenHandling();
            }
            if (e.Key == settings.Binds[1])
            {
                EscapeFullScreen();
            }
            if (e.Key == settings.Binds[6])
            {
                if (isFileListEnabled || isKeyBindListEnabled)
                {
                    return;
                }
                PauseHandling();
            }
            if (e.Key == settings.Binds[2])
            {
                if (isFileListEnabled || isKeyBindListEnabled)
                {
                    return;
                }
                progressOfVideoSlider.Value -= 50_000_000; // 5 seconds 10_000_000 - 1sec
                videoPlayer.Position = TimeSpan.FromTicks((long)progressOfVideoSlider.Value);
            }
            if (e.Key == settings.Binds[3])
            {
                if (isFileListEnabled || isKeyBindListEnabled)
                {
                    return;
                }
                progressOfVideoSlider.Value += 50_000_000; // 5 seconds 10_000_000 - 1sec
                videoPlayer.Position = TimeSpan.FromTicks((long)progressOfVideoSlider.Value);
            }
            if (e.Key == settings.Binds[4])
            {
                if (isFileListEnabled || isKeyBindListEnabled)
                {
                    return;
                }
                SwitchVideo(false);
            }
            if (e.Key == settings.Binds[5])
            {
                if (isFileListEnabled || isKeyBindListEnabled)
                {
                    return;
                }
                SwitchVideo(true);
            }
        }

        private void FullScreenHandling()
        {
            isFullscreen = !isFullscreen;
            settings.FullScreen = isFullscreen;
            if (isFullscreen)
            {
                if (!isFileListEnabled)
                {
                    menuVideoPlayer.Visibility = Visibility.Hidden;
                    videoPlayerPanel.Visibility = Visibility.Hidden;
                }
                if (!isKeyBindListEnabled)
                {
                    menuVideoPlayer.Visibility = Visibility.Hidden;
                    videoPlayerPanel.Visibility = Visibility.Hidden;
                }
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
                return;
            }
            if (!isFileListEnabled)
            {
                menuVideoPlayer.Visibility = Visibility.Visible;
                videoPlayerPanel.Visibility = Visibility.Visible;
            }
            if (!isKeyBindListEnabled)
            {
                menuVideoPlayer.Visibility = Visibility.Visible;
                videoPlayerPanel.Visibility = Visibility.Visible;
            }
            this.WindowStyle = WindowStyle.SingleBorderWindow;
            this.WindowState = WindowState.Normal;
        }

        private void EscapeFullScreen()
        {
            if (!isFullscreen)
            {
                return;
            }
            isFullscreen = false;
            settings.FullScreen = isFullscreen;
            if (isFileListEnabled)
            {
                menuVideoPlayer.Visibility = Visibility.Visible;
                videoPlayerPanel.Visibility = Visibility.Visible;
            }
            if (isKeyBindListEnabled)
            {
                menuVideoPlayer.Visibility = Visibility.Visible;
                videoPlayerPanel.Visibility = Visibility.Visible;
            }
            this.WindowStyle = WindowStyle.SingleBorderWindow;
            this.WindowState = WindowState.Normal;
        }

        private void Window_MouseMoved(object sender, MouseEventArgs e)
        {
            //videoPlayerPanel.Visibility = Visibility.Visible;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            videoUpdateEvents.Stop();
            settings.VideoFiles = loadedVideoFiles;
            SaveJSONClass();
        }

        private void LoadVideoAndPlay(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                videoPlayer.ScrubbingEnabled = true;
                videoPlayer.LoadedBehavior = MediaState.Manual;
                videoPlayer.Source = uri;
                if (!isFileListEnabled)
                {
                    isPaused = true;
                    PauseHandling();
                    videoPlayer.Play();
                    videoUpdateEvents.Start();
                }

                if (videoPlayer.Source != null)
                {
                    pauseButton.IsEnabled = true;
                    nextVideoButton.IsEnabled = true;
                    endOfVideoButton.IsEnabled = true;
                    previousVideoButton.IsEnabled = true;
                    fromStartVideoButton.IsEnabled = true;
                }
                
            }
            catch
            {
                MessageBox.Show("Неподдерживаемый формат видео-файла, загрузка остановлена!", "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
                videoPlayer.Source = null;
                videoPlayer.Stop();
                if (videoPlayer.Source == null)
                {
                    pauseButton.IsEnabled = false;
                    nextVideoButton.IsEnabled = false;
                    endOfVideoButton.IsEnabled = false;
                    previousVideoButton.IsEnabled = false;
                    fromStartVideoButton.IsEnabled = false;
                    return;
                }
            }
        }

        private async void ChooseFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                AddExtension = true,
                Multiselect = true,
                Title = "Выберите видео файлы...",
                Filter = ".MP4 видео файлы (*.mp4)|*.mp4|.FLV видео файлы (*.flv)|*.flv|.AVI видео файлы (*.avi)|*.avi|.WMV видео файлы (*.wmv)|(*.wmv)|All files (*.*)|*.*",
                FilterIndex = 0,
            };  
            if (ofd.ShowDialog() == true)
            {
                int fileIndex = 0; 
                foreach (var file in ofd.FileNames)
                {
                    if (!loadedFilesFirstTime)
                    {
                        if (loadedVideoFiles[fileIndex].Contains(file))
                        {
                            fileIndex++;
                            continue;
                        }
                    }
                    
                    loadedVideoFiles.Add(file);
                    await Dispatcher.BeginInvoke(() =>
                    {
                        Border border = new Border();
                        if (fileIndex > 0) border.Margin = new Thickness(0,5,0,0);
                        border.Background = new SolidColorBrush(Colors.White);
                        border.Name = $"b{fileIndex}";
                        border.BorderThickness = new Thickness(1);
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD3, 0xD3, 0xD3)); // #FFD3D3D3
                        border.MouseDown += VideoFile_Clicked;
                        Label label = new Label();
                        FileInfo info = new FileInfo(file);    
                        label.Content = info.Name;
                        label.FontSize = 14;
                        label.HorizontalContentAlignment = HorizontalAlignment.Center; 
                        border.Child = label;
                        fileList.Children.Add(border);
                    });
                    fileIndex++; 
                }
                if (!isFileListEnabled)
                {
                    LoadVideoAndPlay(loadedVideoFiles[0]);
                }
                else
                {
                    isPaused = false;
                    LoadVideoAndPlay(loadedVideoFiles[0]);
                    PauseHandling();
                }
                currentFileIndex = 0;
                loadedFilesFirstTime = false;
            }
        }

        private void VideoFile_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (previousBorder != null)
            {
                previousBorder.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            }
            Border currentBorder = (Border)sender;
            previousBorder = currentBorder;
            currentBorder.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xAD, 0xD8, 0xE6)); //#FFADD8E6
            string s1 = currentBorder.Name;
            char charToRemove = 'b';
            string result = s1.Replace(charToRemove.ToString(), string.Empty);
            currentVideoFileSelectedIndex = int.Parse(result);
        }

        private async void DownButtonFileList_Click(object sender, MouseButtonEventArgs e)
        {
            if (loadedVideoFiles.Count == 0 || currentVideoFileSelectedIndex == -1)
            {
                return;
            }
            var element = loadedVideoFiles[currentVideoFileSelectedIndex];
            loadedVideoFiles.RemoveAt(currentVideoFileSelectedIndex);
            currentVideoFileSelectedIndex++;
            if (currentVideoFileSelectedIndex > loadedVideoFiles.Count - 1)
            {
                currentVideoFileSelectedIndex = 0;
                loadedVideoFiles.Insert(currentVideoFileSelectedIndex, element);
            }
            else
            {
                loadedVideoFiles.Insert(currentVideoFileSelectedIndex, element);
            }
            int index = 0;
            fileList.Children.Clear();
            foreach (var file in loadedVideoFiles)
            {
                if (!File.Exists(loadedVideoFiles[index]))
                {
                    index++;
                    continue;
                }
                await Dispatcher.BeginInvoke(() =>
                {
                    Border border = new Border();
                    if (index > 0) border.Margin = new Thickness(0, 5, 0, 0);
                    border.Name = $"b{index}";
                    border.Background = new SolidColorBrush(Colors.White);
                    border.BorderThickness = new Thickness(1);
                    border.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD3, 0xD3, 0xD3)); // #FFD3D3D3
                    border.MouseDown += VideoFile_Clicked;
                    Label label = new Label();
                    FileInfo info = new FileInfo(file);
                    label.Content = info.Name;
                    label.FontSize = 14;
                    label.HorizontalContentAlignment = HorizontalAlignment.Center;
                    border.Child = label;
                    fileList.Children.Add(border);
                    index++;
                });
            }
            isPaused = false;
            LoadVideoAndPlay(loadedVideoFiles[0]);
            PauseHandling();
            if (loadedVideoFiles.Count == 0)
            {
                RemoveAllFiles(false);
                currentVideoFileSelectedIndex = -1;
                return;
            }
        }

        private async void UpButtonFileList_Click(object sender, MouseButtonEventArgs e)
        {
            if (loadedVideoFiles.Count == 0 || currentVideoFileSelectedIndex == -1)
            {
                return;
            }
            var element = loadedVideoFiles[currentVideoFileSelectedIndex];
            loadedVideoFiles.RemoveAt(currentVideoFileSelectedIndex);
            currentVideoFileSelectedIndex--;
            if (currentVideoFileSelectedIndex < 0)
            {
                currentVideoFileSelectedIndex = loadedVideoFiles.Count;
                loadedVideoFiles.Insert(currentVideoFileSelectedIndex, element);
            }
            else
            {
                loadedVideoFiles.Insert(currentVideoFileSelectedIndex, element);
            }
            int index = 0; 
            fileList.Children.Clear();
            foreach (var file in loadedVideoFiles)
            {
                if (!File.Exists(loadedVideoFiles[index]))
                {
                    index++;
                    continue;
                }
                await Dispatcher.BeginInvoke(() =>
                {
                    Border border = new Border();
                    if (index > 0) border.Margin = new Thickness(0, 5, 0, 0);
                    border.Name = $"b{index}";
                    border.BorderThickness = new Thickness(1);
                    border.Background = new SolidColorBrush(Colors.White);  
                    border.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD3, 0xD3, 0xD3)); // #FFD3D3D3
                    border.MouseDown += VideoFile_Clicked;
                    Label label = new Label();
                    FileInfo info = new FileInfo(file);
                    label.Content = info.Name;
                    label.FontSize = 14;
                    label.HorizontalContentAlignment = HorizontalAlignment.Center;
                    border.Child = label;
                    fileList.Children.Add(border);
                    index++; 
                });
            }
            isPaused = false;
            LoadVideoAndPlay(loadedVideoFiles[0]);
            PauseHandling();
            if (loadedVideoFiles.Count == 0)
            {
                RemoveAllFiles(false);
                currentVideoFileSelectedIndex = -1;
                return;
            }
        }

        private async void RemoveFileFromList_Click(object sender, MouseButtonEventArgs e)
        {
            if (loadedVideoFiles.Count == 0 || currentVideoFileSelectedIndex == -1)
            {
                return;
            }
            int index = 0; 
            loadedVideoFiles.RemoveAt(currentVideoFileSelectedIndex);
            fileList.Children.RemoveAt(currentVideoFileSelectedIndex);
            foreach (var file in fileList.Children)
            {
                if (!File.Exists(loadedVideoFiles[index]))
                {
                    index++;
                    continue;
                }
                await Dispatcher.BeginInvoke(() =>
                {
                    Border border = (Border)file;
                    border.Name = $"b{index}";
                    border.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
                    index++;    
                });
            }
            if (loadedVideoFiles.Count == 0)
            {
                RemoveAllFiles(false);
                currentVideoFileSelectedIndex = -1;
                return;
            }
            currentVideoFileSelectedIndex = -1;
            isPaused = false;
            LoadVideoAndPlay(loadedVideoFiles[0]);
            PauseHandling();
        }

        private void ListOfFiles_Click(object sender, RoutedEventArgs e)
        {
            isFileListEnabled = !isFileListEnabled;
            if (isFileListEnabled)
            {
                videoPlayer.Visibility = Visibility.Hidden;
                videoPlayerPanel.Visibility = Visibility.Hidden;
                listOfFilesGrid.Visibility = Visibility.Visible;
                isPaused = false;
                PauseHandling();
                return;
            }
            videoPlayer.Visibility = Visibility.Visible;
            videoPlayerPanel.Visibility = Visibility.Visible;
            listOfFilesGrid.Visibility = Visibility.Hidden;
            isPaused = true;
            PauseHandling();
        }

        private void RemoveFiles_Click(object sender, RoutedEventArgs e)
        {
            RemoveAllFiles(true);
        }

        private void RemoveFiles_Click(object sender, MouseButtonEventArgs e)
        {
            RemoveAllFiles(true);
        }

        private void RemoveAllFiles(bool removeLiterally)
        {
            if (removeLiterally)
            {
                loadedVideoFiles.Clear();
                fileList.Children.Clear();
            }
            videoPlayer.ScrubbingEnabled = false;
            videoPlayer.Stop();
            videoPlayer.Source = null;
            currentTime.Content = "00:00:00";
            finalTime.Content = "00:00:00";
            progressOfVideoSlider.Value = 0;
            loadedFilesFirstTime = true; 
            if (videoPlayer.Source == null)
            {
                pauseButton.IsEnabled = false;
                nextVideoButton.IsEnabled = false;
                endOfVideoButton.IsEnabled = false;
                previousVideoButton.IsEnabled = false;
                fromStartVideoButton.IsEnabled = false;
                return;
            }
        }

        private void volumeSlider_ChangedValue(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            videoPlayer.Volume = volumeSlider.Value;    
            volumeSliderPercent.Content = (volumeSlider.Value * 100).ToString("F1") + "%";
            settings.AudioSound = volumeSlider.Value;
        }

        private void FromStartButton_Click(object sender, MouseButtonEventArgs e)
        {
            videoPlayer.Position = TimeSpan.Zero;
            videoUpdateEvents.Start();
        }

        private void PreviousVideoButton_Click(object sender, MouseButtonEventArgs e)
        {
            SwitchVideo(false);
        }

        private void PauseButton_Click(object sender, MouseButtonEventArgs e)
        {
            PauseHandling();
        }

        /// <summary>
        /// Turning on next video depending on direction
        /// </summary>
        /// <param name="direction">true = right; false = left (right = + 1) (left = - 1)</param>
        private void SwitchVideo(bool direction)
        {
            if (loadedVideoFiles.Count == 0)
            {
                return;
            }
            if (direction)
            {
                currentFileIndex++;
                if (currentFileIndex >= loadedVideoFiles.Count)
                {
                    currentFileIndex = 0;
                }
                LoadVideoAndPlay(loadedVideoFiles[currentFileIndex]);
                return;
            }
            currentFileIndex--;
            if (currentFileIndex < 0)
            {
                currentFileIndex = loadedVideoFiles.Count - 1;
            }
            LoadVideoAndPlay(loadedVideoFiles[currentFileIndex]);
        }

        private void PauseHandling()
        {
            if (videoPlayer.Source == null)
            {
                pauseButton.IsEnabled = false;
                nextVideoButton.IsEnabled = false;
                endOfVideoButton.IsEnabled = false;
                previousVideoButton.IsEnabled = false;
                fromStartVideoButton.IsEnabled = false;
                return;
            }
            isPaused = !isPaused;
            if (isPaused)
            {
                videoPlayer.Pause();
                switch (settings.LanguageEnum)
                {
                    case Settings.Language.English:
                        pauseText.Content = "Continue";
                        break;
                    case Settings.Language.Ukrainian:
                        pauseText.Content = "Продовжити";
                        break;
                    case Settings.Language.Russian:
                        pauseText.Content = "Продолжить";
                        break;
                }
                progressOfVideoSlider.IsEnabled = true;
                videoUpdateEvents.Stop();
                return;
            }
            videoPlayer.Play();
            switch (settings.LanguageEnum)
            {
                case Settings.Language.English:
                    pauseText.Content = "Pause";
                    break;
                case Settings.Language.Ukrainian:
                    pauseText.Content = "Пауза";
                    break;
                case Settings.Language.Russian:
                    pauseText.Content = "Пауза";
                    break;
            }
            progressOfVideoSlider.IsEnabled = false;
            videoUpdateEvents.Start();
        }

        private void NextVideoButton_Click(object sender, MouseButtonEventArgs e)
        {
            SwitchVideo(true);
        }

        private void EndOfVideoButton_Click(object sender, MouseButtonEventArgs e)
        {
            if (videoPlayer.NaturalDuration.HasTimeSpan)
            {
                videoPlayer.Position = videoPlayer.NaturalDuration.TimeSpan;
            }
            videoUpdateEvents.Start();
        }

        private void ClearRAM_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
        }

        private void IsPerfectWideRadioButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (!settings.IsVideoWide) return;
            settings.IsVideoWide = false;
            videoPlayer.Stretch = Stretch.Uniform;
        }

        private void IsWideRadio_Clicked(object sender, RoutedEventArgs e)
        {
            if (settings.IsVideoWide) return;
            settings.IsVideoWide = true;
            videoPlayer.Stretch = Stretch.Fill;
        }

        private void ChangeNewVideoAfterEnd_Click(object sender, RoutedEventArgs e)
        {
            settings.NewVideoAfterEnd = !settings.NewVideoAfterEnd;
            newVideoAfterEndCheckBox.IsChecked = settings.NewVideoAfterEnd;
        }

        private void FullScreenKeyBind_Click(object sender, RoutedEventArgs e)
        {
            bindSelected = KeyBindSelected.Fullscreen;
            spareBind = true;
        }

        private void EscapeFullScreen_Click(object sender, RoutedEventArgs e)
        {
            bindSelected = KeyBindSelected.FullscreenEscape;
            spareBind = true;
        }

        private void FiveSecondsToPast_Click(object sender, RoutedEventArgs e)
        {
            bindSelected = KeyBindSelected.BackFiveSeconds;
            spareBind = true;
        }

        private void nextFiveSecondsButton_Click(object sender, RoutedEventArgs e)
        {
            bindSelected = KeyBindSelected.GoFiveSeconds;
            spareBind = true;
        }

        private void PreviousVideoBind_Click(object sender, KeyEventArgs e)
        {
            bindSelected = KeyBindSelected.PreviousVideo;
            spareBind = true;
        }

        private void NextVideoBind_Click(object sender, KeyEventArgs e)
        {
            bindSelected = KeyBindSelected.NextVideo;
            spareBind = true;
        }

        private void PauseButtonBind_Click(object sender, KeyEventArgs e)
        {
            bindSelected = KeyBindSelected.Pause;
            spareBind = true;
        }

        private void ChangeKeyBindingMenu_Click(object sender, RoutedEventArgs e)
        {
            isFileListEnabled = false;
            isKeyBindListEnabled = !isKeyBindListEnabled;
            if (isKeyBindListEnabled)
            {
                videoPlayer.Visibility = Visibility.Hidden;
                videoPlayerPanel.Visibility = Visibility.Hidden;
                listOfFilesGrid.Visibility = Visibility.Hidden;
                keyBindingsGrid.Visibility = Visibility.Visible;
                isPaused = false;
                PauseHandling();
                return;
            }
            videoPlayer.Visibility = Visibility.Visible;
            videoPlayerPanel.Visibility = Visibility.Visible;
            listOfFilesGrid.Visibility = Visibility.Hidden;
            keyBindingsGrid.Visibility = Visibility.Hidden;
            isPaused = true;
            PauseHandling();
        }

        private void DeselectedKeyBind_Click(object sender, MouseButtonEventArgs e)
        {
            bindSelected = KeyBindSelected.None;
            spareBind = false;
        }

        private bool ignoreTranslation = false;
        private void ComboBoxLanguage_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!ignoreTranslation)
            {
                ignoreTranslation = true;
                return;
            }
            switch (comboBoxLanguage.SelectedIndex)
            {
                case 0:
                    settings.LanguageEnum = Settings.Language.English;
                    break;
                case 1:
                    settings.LanguageEnum = Settings.Language.Russian;
                    break;
                case 2:
                    settings.LanguageEnum = Settings.Language.Ukrainian;
                    break;
            }
            Translation(settings.LanguageEnum);
        }

        private void Translation(Settings.Language language)
        {
            switch (language)
            {
                case Settings.Language.English:
                    menuItemFile.Header = "File";
                    menuSubItemChooseFiles.Header = "Choose Files...";
                    menuSubItemListOfFiles.Header = "List of Files...";
                    menuSubItemRemoveFiles.Header = "Remove all files...";
                    menuSubItemCloseMenuFile.Header = "Close";

                    menuItemSettings.Header = "Settings...";
                    menuSubItemChangeHotkeys.Header = "Change hotkeys...";
                    menuSubItemChangeLanguage.Header = "Change language...";
                    menuSubItemEnglishLanguage.Content = "English";
                    menuSubItemUkrainianLanguage.Content = "Ukrainian";
                    menuSubItemRussianLanguage.Content = "Russian";

                    menuSubItemChangeBackground.Header = "Change background...";
                    menuSubItemSettingsOfPlayer.Header = "Settings of player...";
                    newVideoAfterEndCheckBox.Content = "Skip to the next video if the current one is over?";
                    menuSubItemSettingsResolution.Header = "Resolution of Video...";
                    IsPerfectWideRadio.Content = "Show video in the correct size";
                    IsPerfectWideRadio.ToolTip = "Shows the video in its exact size (the edges of the empty field (background color) may be visible)";
                    IsWideRadio.Content = "Show video in a stretched size";
                    IsWideRadio.ToolTip = "Closes the back edges of the background, but stretches the video across the screen.";

                    menuClearRAM.Header = "Clear RAM...";
                    menuSettingsClose.Header = "Close";
                    menuItemInformation.Header = "Info";
                    menuItemCreator.Header = "Developer";

                    fromStartLabel.Content = "To start";
                    previousLabel.Content = "Previous";
                    pauseText.Content = "Pause";
                    nextVideoLabel.Content = "Next";
                    nextVideoToEndLabel.Content = "To end";

                    removeLabel.Content = "REMOVE";
                    upLabel.Content = "UP";
                    downLabel.Content = "DOWN";
                    removeAllLabel.Content = "REMOVE ALL";

                    bindLabelFullscreen.Content = "Fullscreen: ";
                    bindLabelDisableFullscreen.Content = "Disable fullscreen: ";
                    bindPastFiveSeconds.Content = "Five seconds back: ";
                    bindToFutureFiveSeconds.Content = "Five seconds to go: ";
                    bindVideoPreviousLabel.Content = "Previous video: ";
                    bindNextVideo.Content = "Next video: ";
                    bindPause.Content = "Pause: ";

                    break;
                case Settings.Language.Ukrainian:
                    menuItemFile.Header = "Файли";
                    menuSubItemChooseFiles.Header = "Обрати файли...";
                    menuSubItemListOfFiles.Header = "Список усіх файлів...";
                    menuSubItemRemoveFiles.Header = "Прибрати усі файли...";
                    menuSubItemCloseMenuFile.Header = "Зачинити";

                    menuItemSettings.Header = "Налаштування...";
                    menuSubItemChangeHotkeys.Header = "Змінити гарячі клавіши...";
                    menuSubItemChangeLanguage.Header = "Змінити мову...";
                    menuSubItemEnglishLanguage.Content = "Англійська мова";
                    menuSubItemUkrainianLanguage.Content = "Українська мова";
                    menuSubItemRussianLanguage.Content = "Російська мова";

                    menuSubItemChangeBackground.Header = "Змінити фон...";
                    menuSubItemSettingsOfPlayer.Header = "Налаштування плеера...";
                    newVideoAfterEndCheckBox.Content = "Переходити на наступне відео, якщо поточне закінчилося?";
                    menuSubItemSettingsResolution.Header = "Роздільна здатність відео...";
                    IsPerfectWideRadio.Content = "Відображати відео у правильному розмірі";
                    IsPerfectWideRadio.ToolTip = "Відображає відео в його точному розмірі (краї порожнього поля (колір фону) можуть бути видимими)";
                    IsWideRadio.Content = "Показати відео в розтягнутому розмірі";
                    IsWideRadio.ToolTip = "Закриває задні краї тла, але розтягує відео на весь екран.";

                    menuClearRAM.Header = "Очистити оперативну пам'ять...";
                    menuSettingsClose.Header = "Зачинити";
                    menuItemInformation.Header = "Довідка";
                    menuItemCreator.Header = "Розробник";

                    fromStartLabel.Content = "Зі старту";
                    previousLabel.Content = "Попередні";
                    pauseText.Content = "Пауза";
                    nextVideoLabel.Content = "Далі";
                    nextVideoToEndLabel.Content = "До кінця";

                    removeLabel.Content = "ПРИБРАТИ";
                    upLabel.Content = "ВГОРУ";
                    downLabel.Content = "ВНИЗ";
                    removeAllLabel.Content = "ПРИБРАТИ УСЕ";

                    bindLabelFullscreen.Content = "Повноекранний режим: ";
                    bindLabelDisableFullscreen.Content = "Вимкнути повноекранний режим: ";
                    bindPastFiveSeconds.Content = "П'ять секунд назад: ";
                    bindToFutureFiveSeconds.Content = "П'ять секунд до кінця: ";
                    bindVideoPreviousLabel.Content = "Попереднє відео: ";
                    bindNextVideo.Content = "Наступне відео: ";
                    bindPause.Content = "Пауза: ";

                    break;
                case Settings.Language.Russian:
                    menuItemFile.Header = "Файл";
                    menuSubItemChooseFiles.Header = "Выбрать файлы...";
                    menuSubItemListOfFiles.Header = "Список файлов...";
                    menuSubItemRemoveFiles.Header = "Убрать все файлы...";
                    menuSubItemCloseMenuFile.Header = "Закрыть";

                    menuItemSettings.Header = "Настройки";
                    menuSubItemChangeHotkeys.Header = "Сменить горячие клавиши...";
                    menuSubItemChangeLanguage.Header = "Сменить язык...";
                    menuSubItemEnglishLanguage.Content = "Английский";
                    menuSubItemUkrainianLanguage.Content = "Украинский";
                    menuSubItemRussianLanguage.Content = "Русский";

                    menuSubItemChangeBackground.Header = "Сменить фон...";
                    menuSubItemSettingsOfPlayer.Header = "Настройки плеера...";
                    newVideoAfterEndCheckBox.Content = "Переход к следующему видео, если текущее закончилось?";
                    menuSubItemSettingsResolution.Header = "Разрешение видео...";
                    IsPerfectWideRadio.Content = "Показывать видео в правильном размере";
                    IsPerfectWideRadio.ToolTip = "Показывает видео в точном размере (могут быть видны края пустого поля (цвет фона))";
                    IsWideRadio.Content = "Показывать видео в растянутом размере";
                    IsWideRadio.ToolTip = "Закрывает задние края фона, но растягивает видео на весь экран.";

                    menuClearRAM.Header = "Очистить оперативную память";
                    menuSettingsClose.Header = "Закрыть";
                    menuItemInformation.Header = "Справка";
                    menuItemCreator.Header = "Разработчик";

                    fromStartLabel.Content = "Сначала";
                    previousLabel.Content = "Предыдущие";
                    pauseText.Content = "Пауза";
                    nextVideoLabel.Content = "Далее";
                    nextVideoToEndLabel.Content = "К концу";

                    removeLabel.Content = "УБРАТЬ";
                    upLabel.Content = "ВВЕРХ";
                    downLabel.Content = "ВНИЗ";
                    removeAllLabel.Content = "УБРАТЬ ВСЁ";

                    bindLabelFullscreen.Content = "Включить полноэкранный режим: ";
                    bindLabelDisableFullscreen.Content = "Отключить полноэкранный режим: ";
                    bindPastFiveSeconds.Content = "Пять секунд назад: ";
                    bindToFutureFiveSeconds.Content = "Осталось пять секунд: ";
                    bindVideoPreviousLabel.Content = "Предыдущее видео: ";
                    bindNextVideo.Content = "Следующее видео: ";
                    bindPause.Content = "Пауза: ";

                    break;
            }
        }

        private bool showInformationList = false; 
        private void ShowInformation_Click(object sender, RoutedEventArgs e)
        {
            showInformationList = !showInformationList;
            if (showInformationList)
            {
                infoGrid.Visibility = Visibility.Visible;
                videoPlayer.Visibility = Visibility.Hidden;
                videoPlayerPanel.Visibility = Visibility.Hidden;
                listOfFilesGrid.Visibility = Visibility.Hidden;
                menuVideoPlayer.Visibility = Visibility.Hidden;
                videoPlayerPanel.Visibility = Visibility.Hidden;
                isPaused = false;
                PauseHandling();
            }
            else
            {
                infoGrid.Visibility = Visibility.Hidden;
                videoPlayer.Visibility = Visibility.Visible;
                videoPlayerPanel.Visibility = Visibility.Visible;
                listOfFilesGrid.Visibility = Visibility.Hidden;
                menuVideoPlayer.Visibility = Visibility.Visible;
                videoPlayerPanel.Visibility = Visibility.Visible;
                isPaused = false;
                PauseHandling();
            }
        }

        private void CreatorClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Slide Video Player by J0nathan550 © 2023", "Developer");
        }
    }
}