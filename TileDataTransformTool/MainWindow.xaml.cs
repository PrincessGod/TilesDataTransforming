using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Configuration;
using System.Drawing;
using WPF.Themes;
using SharpConfig;
using System.Threading;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace TileDataTransformTool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        SharpConfig.Configuration config;

        private TransformConfigion tConfig;

        private ulong tileAmount;
        private ulong tileFinished;
        private ulong tileSucceed;
        private ulong lastFinished;

        private System.Windows.Forms.Timer timeCount;

        private delegate void SpeedChangeDele(int speed, int anticipate);
        private static event SpeedChangeDele SpeedChanged;

        private string exportEXE = null;
        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        /// <summary>
        /// connect to the mongo database
        /// </summary>
        private void Initialize()
        {
            
        }

        void DBTranslateFactory_TaskFinished()
        {
          
            this.Dispatcher.Invoke(new Action(delegate
            {
                SpeedLabel.Content = "Complete";
                start.Content = "Start";
                DBTranslateFactory.configFinished = 0;
                DBTranslateFactory.configSucceed = 0;
                if (timeCount != null)
                {
                    timeCount.Stop();
                }
                try
                {
                    //config = SharpConfig.Configuration.LoadFromFile("Config.ini");
                    if (config != null && config.Contains("Record") && config["Record"].SettingCount == 13)
                    {
                        config["Record"]["NowTileNum"].SetValue<ulong>(tConfig.NowTileNum);
                        config["Record"]["SuccessNum"].SetValue<ulong>(tConfig.SuccessNum);
                        config["Record"]["minLon"].SetValue<double>(tConfig.minLon);
                        config["Record"]["minLat"].SetValue<double>(tConfig.minLat);
                        config["Record"]["maxLon"].SetValue<double>(tConfig.maxLon);
                        config["Record"]["maxLat"].SetValue<double>(tConfig.maxLat);
                        config["Record"]["sLevel"].SetValue<int>(tConfig.sLevel);
                        config["Record"]["eLevel"].SetValue<int>(tConfig.eLevel);
                        config["Record"]["OperateFunction"].SetValue<string>(tConfig.OperateFunction);
                        config["Record"]["StartTime"].SetValue<string>(tConfig.StartTime);
                        config["Record"]["LastTime"].SetValue<string>(tConfig.LastTime);
                        config["Record"]["TotalTilesNum"].SetValue<ulong>(tConfig.TotalTilesNum);
                        config.Save("Config.ini");
                    }

                    if (CheckExport.IsChecked != null && CheckExport.IsChecked == true)
                    {
                        this.GetExportEXE();
                        this.ExportDB();
                    }
                }
                catch (Exception)
                {

                    throw;
                }
            }));            
        }

        void DBTranslateFactory_ProgressChanged(ulong now, ulong sum, ulong successNum)
        {
            this.Dispatcher.Invoke(new Action(delegate
            {
                ProgressInfo.Value = (double)now / (double)sum * 100;

                ProgressLabel.Content = string.Format("Complete\r\n{0}/{1}\r\n{2:f2}% ", now, sum, (double)now / (double)sum * 100);
                ProgressLabel1.Content = string.Format("Successed\r\n{1}/{0,-4}\r\n{2:f2}%", now, successNum, (double)successNum / (double)now * 100);
                tileAmount = sum;
                tileFinished = now;
                tileSucceed = successNum;
                if (tConfig != null)
                {
                    tConfig.SuccessNum = tileSucceed;
                    tConfig.NowTileNum = tileFinished;
                    tConfig.TotalTilesNum = tileAmount;
                }
            }));  
            
        }

        void DBTranslateFactory_statusChanged(string info)
        {
            this.Dispatcher.Invoke(new Action(delegate
            {
                StatusLable.Content = info;
            }));              
        }

        /// <summary>
        /// according to the longitude latitude box and level range insert tile data to kq database file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void start_Click(object sender, RoutedEventArgs e)
        {
            if (start.Content.ToString() == "Start" || start.Content.ToString() == "Config")
            {
                if (start.Content.ToString() == "Config")
                {
                    string configPath = "";
                    System.Windows.Forms.OpenFileDialog odlg = new OpenFileDialog();
                    odlg.InitialDirectory = System.IO.Directory.GetCurrentDirectory();
                    odlg.Filter = "config files (*.ini)|*.ini";
                    if (odlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        configPath = odlg.FileName;
                    }
                    else { return; }

                    tConfig = new TransformConfigion();

                    try
                    {
                        SharpConfig.Configuration configi = SharpConfig.Configuration.LoadFromFile(configPath);
                        if (configi != null && configi.Contains("Record") && configi["Record"].SettingCount == 13)
                        {
                            tConfig = configi["Record"].CreateObject<TransformConfigion>();
                            if (tConfig != null)
                            {
                                SUM_MinX.Text = tConfig.minLon.ToString();
                                SUM_MinY.Text = tConfig.minLat.ToString();
                                SUM_MaxX.Text = tConfig.maxLon.ToString();
                                SUM_MaxY.Text = tConfig.maxLat.ToString();

                                StartLevel.Text = tConfig.sLevel.ToString();
                                EndLevel.Text = tConfig.eLevel.ToString();
                                foreach (ComboBoxItem s in this.CBOX.Items)
                                {
                                    if (tConfig.OperateFunction == s.Content as string)
                                    {
                                        this.CBOX.SelectedItem = s;
                                    }
                                }

                                List<string> item = new List<string>();
                                item.Add(string.Format("Min Longitude :{0}", tConfig.minLon));
                                item.Add(string.Format("Max Longitude :{0}", tConfig.maxLon));
                                item.Add(string.Format("Min Latitude :{0}", tConfig.minLat));
                                item.Add(string.Format("Max Latitude :{0}", tConfig.maxLat));
                                item.Add(string.Format("Start Level :{0}", tConfig.sLevel));
                                item.Add(string.Format("End   Level :{0}", tConfig.eLevel));
                                item.Add(string.Format("Function :{0}", tConfig.OperateFunction));
                                item.Add(string.Format("Start Time :{0}", tConfig.StartTime));
                                item.Add(string.Format("Last  Time :{0}", tConfig.LastTime));
                                item.Add(string.Format("Tiles Amount :{0}", tConfig.TotalTilesNum));
                                item.Add(string.Format("Tiles Finished :{0}", tConfig.NowTileNum));
                                item.Add(string.Format("Tiles Succeed :{0}", tConfig.SuccessNum));
                                item.Add(string.Format("Save Path :{0}", tConfig.SavePath));
                                listBoxConfig.ItemsSource = item;

                                DBTranslateFactory.configSucceed = tConfig.SuccessNum;
                                DBTranslateFactory.configFinished = tConfig.NowTileNum;
                            }
                        }


                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return;

                    }
                }

                double minX = 0, minY = 0, maxX = 0, maxY = 0;
                int sLevel = 0, eLevel = 0, threatNum = 0;

                string func = this.CBOX.SelectionBoxItem as string;

                if (!(double.TryParse(SUM_MinX.Text, out minX)) || !(double.TryParse(SUM_MinY.Text, out minY)) || !(double.TryParse(SUM_MaxX.Text, out maxX)) || !(double.TryParse(SUM_MaxY.Text, out maxY)))
                {
                    StatusLable.Content = "Try to enter anther valuable longitude and latitude range.";
                    return;
                }
                if (!(int.TryParse(StartLevel.Text, out sLevel)) || !(int.TryParse(EndLevel.Text, out eLevel)) || !(int.TryParse(ThreatNum.Text, out threatNum)))
                {
                    StatusLable.Content = "Try to enter anther valuable StartLevel and EndLevel , Threat Number.";
                    return;
                }

                if (minX > maxX || minY > maxY || minX < -180 || minY < -90 || maxX > 180 || maxY > 90)
                {
                    StatusLable.Content = "Try to enter anther valuable longitude and latitude range.";
                    return;
                }

                if (sLevel < 0 || sLevel > 21 || eLevel < 0 || eLevel > 21 || sLevel > eLevel || threatNum < 1 || threatNum > 25)
                {
                    StatusLable.Content = "Try to enter anther valuable StartLevel and EndLevel.";
                    return;
                }

                imagebox.Visibility = Visibility.Hidden;
                bigiamgebox.Visibility = Visibility.Hidden;
                KQLable.Visibility = Visibility.Hidden;
                GoogleLbel.Visibility = Visibility.Hidden;

                ProgressInfo.Visibility = Visibility.Visible;

                //start.IsEnabled = false;

                tConfig = new TransformConfigion();

                tConfig.sLevel = sLevel;
                tConfig.eLevel = eLevel;
                tConfig.minLat = minY;
                tConfig.minLon = minX;
                tConfig.maxLat = maxY;
                tConfig.maxLon = maxX;
                tConfig.TotalTilesNum = 0;
                tConfig.NowTileNum = 0;
                tConfig.SuccessNum = 0;
                tConfig.OperateFunction = func;
                tConfig.StartTime = DateTime.Now.ToString();
                tConfig.LastTime = DateTime.Now.ToString();
                tConfig.SavePath = DBTranslateFactory.kqpath;

                tileAmount = 0;
                tileFinished = 0;
                tileSucceed = 0;
                lastFinished = 0;

                StatusLable.Content = "Transformation is beginning.";

                bool result = false;
                if (func == "MongoDB To KQDB")
                {
                    if (DBTranslateFactory.InsertOperationByMultiThreading(threatNum, sLevel, eLevel, maxX, maxY, minX, minY))
                    {
                        result = true;
                        StatusLable.Content = "Transformation has launched successfully.";
                    }
                }

                if (func == "MongoDB To File")
                {
                    if (DBTranslateFactory.CreatGoogleFileByMultiThreading(threatNum, sLevel, eLevel, maxX, maxY, minX, minY))
                    {
                        result = true;
                        StatusLable.Content = "Transformation has launched successfully.";
                    }
                }

                if (func == "Get Partial MongoDB")
                {
                    if (DBTranslateFactory.CreatPartialMongoDB(threatNum, sLevel, eLevel, maxX, maxY, minX, minY))
                    {
                        result = true;
                        StatusLable.Content = "Transformation has launched successfully.";
                    }
                }
                //DBTranslateFactory.InsertKQImageByLonLatRange(6, 115, 25, 110, 20);
                if (!result)
                {
                    return;
                }
                timeCount = new System.Windows.Forms.Timer();
                timeCount.Tick += timeCount_Tick;
                timeCount.Enabled = true;
                timeCount.Interval = 1000;
                timeCount.Start();

                start.Content = "Pause";
                if (config != null && config.Contains("Record") && config["Record"].SettingCount == 13)
                {
                    config["Record"]["NowTileNum"].SetValue<ulong>(tConfig.NowTileNum);
                    config["Record"]["SuccessNum"].SetValue<ulong>(tConfig.SuccessNum);
                    config["Record"]["minLon"].SetValue<double>(tConfig.minLon);
                    config["Record"]["minLat"].SetValue<double>(tConfig.minLat);
                    config["Record"]["maxLon"].SetValue<double>(tConfig.maxLon);
                    config["Record"]["maxLat"].SetValue<double>(tConfig.maxLat);
                    config["Record"]["sLevel"].SetValue<int>(tConfig.sLevel);
                    config["Record"]["eLevel"].SetValue<int>(tConfig.eLevel);
                    config["Record"]["OperateFunction"].SetValue<string>(tConfig.OperateFunction);
                    config["Record"]["StartTime"].SetValue<string>(tConfig.StartTime);
                    config["Record"]["LastTime"].SetValue<string>(tConfig.LastTime);
                    config["Record"]["TotalTilesNum"].SetValue<ulong>(tConfig.TotalTilesNum);
                    config["Record"]["SavePath"].SetValue<string>(tConfig.SavePath);
                    config.Save("Config.ini");
                }

                return;
            }
            else if (start.Content.ToString() == "Pause")
            {
                DBTranslateFactory.Pause = true;
                timeCount.Stop();
                start.Content = "Continue";
                return;
            }

            else if (start.Content.ToString() == "Continue")
            {
                DBTranslateFactory.Pause = false;
                timeCount.Start();
                start.Content = "Pause";
                return;
            }

        }

        void MainWindow_SpeedChanged(int speed, int anticipate)
        {
            this.Dispatcher.Invoke(new Action(delegate
            {
                string time = TimeSpan.FromSeconds(anticipate).ToString(@"hh\:mm\:ss");
                SpeedLabel.Content = string.Format("Time remaining : {0}", time);
                SpeedLabel1.Content = string.Format("{0} Tiles / S", speed);
            }));   
        }

     

        void timeCount_Tick(object sender, EventArgs e)
        {
            tConfig.LastTime = DateTime.Now.ToString();
            try
            {
                //config = SharpConfig.Configuration.LoadFromFile("Config.ini");
                if (config != null && tConfig != null && config.Contains("Record") && config["Record"].SettingCount == 13)
                {
                    config["Record"]["NowTileNum"].SetValue<ulong>(tConfig.NowTileNum);
                    config["Record"]["SuccessNum"].SetValue<ulong>(tConfig.SuccessNum);
                    config["Record"]["LastTime"].SetValue<string>(tConfig.LastTime);
                    config["Record"]["TotalTilesNum"].SetValue<ulong>(tConfig.TotalTilesNum);
                    config.Save("Config.ini");
                }

                int speed = (int)(tileFinished - lastFinished);
                if (speed != 0)
                {
                    int second = (int)(tileAmount - tileFinished) / speed;
                    if (SpeedChanged != null)
                    {
                        SpeedChanged(speed, second);
                        lastFinished = tileFinished;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        /// <summary>
        /// get the google big image and extracted kq tile image by level-X-Y
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void start_lxy_Click(object sender, RoutedEventArgs e)
        {
            int level = 0,x = 0, y = 0;
            if(!(int.TryParse(Signal_L.Text,out level)) || !(int.TryParse(Signal_X.Text,out x)) || !(int.TryParse(Signal_Y.Text,out y)))
            {
                StatusLable.Content = "Try to enter anther valuable L-X-Y.";
                return;
            }
            string func = this.CBOX.SelectionBoxItem as string;
            if (func == "MongoDB部分转KQDB")
            {
                if (level < 0 || level > 15)
                {
                    StatusLable.Content = "Try to enter anther valuable L.";
                    return;
                }

                int maxX = 5 * (int)Math.Pow(2.0, level);
                int maxY = 10 * (int)Math.Pow(2.0, level);

                if (x >= maxX || y >= maxY || x < 0 || y < 0)
                {
                    StatusLable.Content = "Try to enter anther valuable X-Y.";
                    return;
                }
                Envelope range = new Envelope();

                DBTranslateFactory.GetLonLatBoundByRowCol(level, x, y, ref range);
                BigImage bigimage = await DBTranslateFactory.GetBigImageFromGoogleTile("", level + 5, range.MinX, range.MinY, range.MaxX, range.MaxY);

                byte[] bigimg = null;
                if (bigimage != null && bigimage.BigImg != null)
                {
                    var _TargetMemory = new MemoryStream();
                    bigimage.BigImg.Save(_TargetMemory, ImageFormat.Jpeg);
                    bigimage.BigImg.Dispose();
                    bigimage.BigImg = null;
                    bigimg = _TargetMemory.GetBuffer();
                }
                else
                {
                    StatusLable.Content = "Can not find tile data in google database.";
                    return;
                }

                if (!(bigimg == null))
                {
                    BitmapImage imgSource = new BitmapImage();
                    imgSource.BeginInit();
                    imgSource.StreamSource = new MemoryStream(bigimg);
                    imgSource.EndInit();
                    bigiamgebox.Source = imgSource;
                }

                GoogleLbel.Visibility = Visibility.Visible;

                byte[] image = await DBTranslateFactory.GetKQTileImageBuffer("", level, x, y);

                if (!(image == null))
                {
                    BitmapImage imgSource = new BitmapImage();
                    imgSource.BeginInit();
                    imgSource.StreamSource = new MemoryStream(image);
                    imgSource.EndInit();
                    imagebox.Source = imgSource;
                }
                else
                {
                    StatusLable.Content = "Convert big image to KongQing tile image failed.";
                }
            }

            if (func == "MongoDB提取图片文件")
            {
                string key = string.Format("{0}-{1}-{2}", level, x, y);
                 if (DBTranslateFactory.DBReaderHelper != null)
                {
                    try
                    {
                        byte[] buffer = await DBTranslateFactory.DBReaderHelper.GetTiled(key);
                        if (buffer != null)
                        {
                            BitmapImage imgSource = new BitmapImage();
                            imgSource.BeginInit();
                            imgSource.StreamSource = new MemoryStream(buffer);
                            imgSource.EndInit();
                            imagebox.Source = imgSource;
                        }
                        else
                        {
                            StatusLable.Content = "Can not find tile data in google database";
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusLable.Content = ex.Message;
                    }
                }
            }


            imagebox.Visibility = Visibility.Visible;
            bigiamgebox.Visibility = Visibility.Visible;

            ProgressInfo.Visibility = Visibility.Hidden;
            ProgressLabel.Visibility = Visibility.Hidden;

            KQLable.Visibility = Visibility.Visible;
            
        }

        private void filedir_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            fbd.ShowDialog();
            if (fbd.SelectedPath != string.Empty)
            {
                StatusLable.Content = fbd.SelectedPath;
                DBTranslateFactory.kqpath = fbd.SelectedPath;
            }
        }
        bool load = false;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string ServiceAdress = null;
            string ServiceAdress2 = null;
            if (ConfigurationManager.AppSettings["ServiceAdressSource"] != null)
            {
                ServiceAdress = ConfigurationManager.AppSettings["ServiceAdressSource"];
            }
            else
            {
                ServiceAdress = "mongodb://192.168.1.103:27017";
            }
            if (ConfigurationManager.AppSettings["ServiceAdressTarget"] != null)
            {
                ServiceAdress2 = ConfigurationManager.AppSettings["ServiceAdressTarget"];
            }
            else
            {
                ServiceAdress2 = "mongodb://localhost:27017";
            }
            Console.WriteLine("serverAdress: " + ServiceAdress);
            //string ServiceAdress = "mongodb://192.168.1.103:27017";

            DBTranslateFactory.DBReaderHelper = new MongoDBReaderHelper();
            if (!DBTranslateFactory.DBReaderHelper.InitMongoDB(ServiceAdress, ServiceAdress2))
            {
                System.Windows.Forms.MessageBox.Show("数据库连接失败", "Error");
                this.Close();
            }
            DBTranslateFactory.statusChanged += DBTranslateFactory_statusChanged;
            DBTranslateFactory.ProgressChanged += DBTranslateFactory_ProgressChanged;
            DBTranslateFactory.TaskFinished += DBTranslateFactory_TaskFinished;
            if (DBTranslateFactory.DBReaderHelper.DBNames != null)
            {
                listBoxSourceDB.ItemsSource = DBTranslateFactory.DBReaderHelper.DBNames;
            }
            if (DBTranslateFactory.DBReaderHelper.DBNamesTarget != null)
            {
                listBoxTargetDB.ItemsSource = DBTranslateFactory.DBReaderHelper.DBNamesTarget;
            }
            themes.ItemsSource = ThemeManager.GetThemes();
            try
            {
                config = SharpConfig.Configuration.LoadFromFile("Config.ini");
                if (config != null && config.Contains("Record") && config["Record"].SettingCount == 13)
                {
                    TransformConfigion tConfig = config["Record"].CreateObject<TransformConfigion>();
                    if (tConfig != null)
                    {
                        //SUM_MinX.Text = tConfig.minLon.ToString();
                        //SUM_MinY.Text = tConfig.minLat.ToString();
                        //SUM_MaxX.Text = tConfig.maxLon.ToString();
                        //SUM_MaxY.Text = tConfig.maxLat.ToString();

                        //StartLevel.Text = tConfig.sLevel.ToString();
                        //EndLevel.Text = tConfig.eLevel.ToString();
                        //foreach (ComboBoxItem s in this.CBOX.Items)
                        //{
                        //    if (tConfig.OperateFunction == s.Content as string)
                        //    {
                        //        this.CBOX.SelectedItem = s;
                        //    }
                        //}

                        List<string> item = new List<string>();
                        item.Add(string.Format("Min Longitude: {0}", tConfig.minLon));
                        item.Add(string.Format("Max Longitude: {0}", tConfig.maxLon));
                        item.Add(string.Format("Min Latitude: {0}", tConfig.minLat));
                        item.Add(string.Format("Max Latitude: {0}", tConfig.maxLat));
                        item.Add(string.Format("Start Level: {0}", tConfig.sLevel));
                        item.Add(string.Format("End Level: {0}", tConfig.eLevel));
                        item.Add(string.Format("Function: {0}", tConfig.OperateFunction));
                        item.Add(string.Format("Start Time: {0}", tConfig.StartTime));
                        item.Add(string.Format("Last Time: {0}", tConfig.LastTime));
                        item.Add(string.Format("Tiles Amount: {0}", tConfig.TotalTilesNum));
                        item.Add(string.Format("Tiles Finished: {0}", tConfig.NowTileNum));
                        item.Add(string.Format("Tiles Succeed: {0}", tConfig.SuccessNum));
                        item.Add(string.Format("Save Path: {0}", tConfig.SavePath));
                        listBoxConfig.ItemsSource = item;
                    }
                }
                

            }
            catch (Exception)
            {
                
                throw;
            }


            SpeedChanged += MainWindow_SpeedChanged;
            load = true;

            this.UpdateTree();

            
            //SpeedLabel.Content = "fgfgfgfgfgfgfgfgfgfgfgfgfgfg";
            //SpeedLabel1.Content = "fgfgfgfgfgf";
            //int x = 123, y = 321, z = 113;
            //ProgressLabel.Content = string.Format("Complete\r\n{0}/{1}\r\n{2:f2}% ", x, y, (double)x / (double)y * 100);
            //ProgressLabel1.Content = string.Format("Successed\r\n{1}/{0}\r\n{2:f2}%", x, z, (double)z / (double)x * 100);
        }

        private void themes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string theme = e.AddedItems[0].ToString();

                // Window Level
                // this.ApplyTheme(theme);

                // Application Level
                // Application.Current.ApplyTheme(theme);
            }
        }

        private void DBNames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!load)
                return;
            if (DBTranslateFactory.DBReaderHelper.GetSourceDBNames())
            {
                listBoxSourceDB.ItemsSource = DBTranslateFactory.DBReaderHelper.DBNames;
            }
            if (DBTranslateFactory.DBReaderHelper.GetTargetDBNames())
            {
                listBoxTargetDB.ItemsSource = DBTranslateFactory.DBReaderHelper.DBNamesTarget;
            }

            this.UpdateTree();
        }

        private void start_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right && e.ClickCount == 1)
            {
                if (start.Content.ToString() == "Start")
                {
                    start.Content = "Config";
                }
                else if (start.Content.ToString() == "Config")
                {
                    start.Content = "Start";
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string configPath = "";
            System.Windows.Forms.OpenFileDialog odlg = new OpenFileDialog();
            odlg.InitialDirectory = System.IO.Directory.GetCurrentDirectory();
            odlg.Filter = "config files (*.ini)|*.ini";
            if (odlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                configPath = odlg.FileName;
            }
            else { return; }

            tConfig = new TransformConfigion();

            try
            {
                SharpConfig.Configuration configi = SharpConfig.Configuration.LoadFromFile(configPath);
                if (configi != null && configi.Contains("Record") && configi["Record"].SettingCount == 13)
                {
                    tConfig = configi["Record"].CreateObject<TransformConfigion>();
                    if (tConfig != null)
                    {
                        SUM_MinX.Text = tConfig.minLon.ToString();
                        SUM_MinY.Text = tConfig.minLat.ToString();
                        SUM_MaxX.Text = tConfig.maxLon.ToString();
                        SUM_MaxY.Text = tConfig.maxLat.ToString();

                        StartLevel.Text = tConfig.sLevel.ToString();
                        EndLevel.Text = tConfig.eLevel.ToString();
                        foreach (ComboBoxItem s in this.CBOX.Items)
                        {
                            if (tConfig.OperateFunction == s.Content as string)
                            {
                                this.CBOX.SelectedItem = s;
                            }
                        }

                        List<string> item = new List<string>();
                        item.Add(string.Format("Min Longitude: {0}", tConfig.minLon));
                        item.Add(string.Format("Max Longitude: {0}", tConfig.maxLon));
                        item.Add(string.Format("Min Latitude: {0}", tConfig.minLat));
                        item.Add(string.Format("Max Latitude: {0}", tConfig.maxLat));
                        item.Add(string.Format("Start Level: {0}", tConfig.sLevel));
                        item.Add(string.Format("End Level: {0}", tConfig.eLevel));
                        item.Add(string.Format("Function: {0}", tConfig.OperateFunction));
                        item.Add(string.Format("Start Time: {0}", tConfig.StartTime));
                        item.Add(string.Format("Last Time: {0}", tConfig.LastTime));
                        item.Add(string.Format("Tiles Amount: {0}", tConfig.TotalTilesNum));
                        item.Add(string.Format("Tiles Finished: {0}", tConfig.NowTileNum));
                        item.Add(string.Format("Tiles Succeed: {0}", tConfig.SuccessNum));
                        item.Add(string.Format("Save Path: {0}", tConfig.SavePath));
                        listBoxConfig.ItemsSource = item;

                        DBTranslateFactory.configSucceed = tConfig.SuccessNum;
                        DBTranslateFactory.configFinished = tConfig.NowTileNum;
                    }
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;

            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            GetExportEXE();
            ExportDB();  
        }

        private void ExportDB()
        {
            Process myProcess = new Process();
            List<string> DBnames = new List<string>();
            if (exportEXE == null)
                return;
            if (DBTranslateFactory.DBReaderHelper.DBNamesTarget != null)
            {
                foreach (string name in DBTranslateFactory.DBReaderHelper.DBNamesTarget)
                {
                    string[] names = name.Split('_');
                    if (names.Length == 2 && names[1] == "Partial")
                    {
                        DBnames.Add(name);
                    }
                }
            }
            if (DBnames.Count == 0)
            {
                StatusLable.Content = "TargetDB don't have partial database";
                return;
            }
            string dicuper = string.Format(@"{0}\{1}", System.IO.Directory.GetCurrentDirectory(), DateTime.Now.ToString("yy_MM_dd_hh_mm_ss"));
            DirectoryInfo dif = new DirectoryInfo(dicuper);
            dif.Create();

            foreach (string dbname in DBnames)
            {
                string[] names = dbname.Split('_');
                string dic = string.Format(@"{0}\{1}", dicuper, names[0]);
                if (!Directory.Exists(dic))
                {
                    DirectoryInfo df = new DirectoryInfo(dic);
                    df.Create();
                }
                List<string> collectionNames = new List<string>();
                collectionNames = DBTranslateFactory.DBReaderHelper.GetCollectionNames(dbname);
                if (collectionNames != null && collectionNames.Count != 0)
                {
                    foreach (string collect in collectionNames)
                    {
                        string para = string.Format(@"-d {0} -c {1} -o {2}\{1}.json", dbname, collect, dic);

                        ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(exportEXE, para);

                        myProcess.StartInfo = myProcessStartInfo;

                        myProcess.Start();

                        while (!myProcess.HasExited)
                        {
                            myProcess.WaitForExit();
                        }
                        int returnValue = myProcess.ExitCode;
                        if (returnValue == 0)
                        {
                            StatusLable.Content = string.Format(@"Export file ...{0}\{1} finished", dbname, collect);
                            Console.WriteLine(string.Format(@"Export file ...{0}\{1} finished", dbname, collect));
                        }
                        else
                        {
                            StatusLable.Content = "Export has something wrong!";
                            Console.WriteLine("Export has something wrong!");
                        }
                    }


                }


            }
        }

        private void UpdateTree()
        {
            SourceTree.Items.Clear();
            TargetTree.Items.Clear();
            if (DBTranslateFactory.DBReaderHelper.DBNames != null)
            {              
                foreach (string db in DBTranslateFactory.DBReaderHelper.DBNames)
                {
                    MenuItem root = new MenuItem() {  Title = db };
                    var col = DBTranslateFactory.DBReaderHelper.GetCollectionNames(db);
                    if (col != null)
                    {
                        foreach(string coll in col)
                        {
                            MenuChildItem child = new MenuChildItem() { Title = coll };
                            root.Items.Add(child);
                        }
                    }

                    SourceTree.Items.Add(root);
                }
            }

            if (DBTranslateFactory.DBReaderHelper.DBNamesTarget != null)
            {
                foreach (string db in DBTranslateFactory.DBReaderHelper.DBNamesTarget)
                {
                    MenuItem root = new MenuItem() { Title = db };
                    var col = DBTranslateFactory.DBReaderHelper.GetCollectionNames(db);
                    if (col != null)
                    {
                        foreach (string coll in col)
                        {
                            MenuChildItem child = new MenuChildItem() { Title = coll };
                            root.Items.Add(child);
                        }
                    }

                    TargetTree.Items.Add(root);
                }
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            GetExportEXE();
        }

        private void GetExportEXE()
        {
            if (exportEXE == null)
            {
                System.Windows.Forms.OpenFileDialog odlg = new OpenFileDialog();
                odlg.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                odlg.Filter = "exe files (*.exe)|*.exe";
                odlg.FileName = "mongoexport.exe";
                if (odlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (System.IO.Path.GetFileNameWithoutExtension(odlg.FileName) != "mongoexport")
                    {
                        StatusLable.Content = "Can not find 'mongoexport.exe'";
                        return;
                    }
                    exportEXE = odlg.FileName;
                }
                else { return; }
            }
        }
    }

    public sealed class TransformConfigion
    {
        public double minLon { get; set; }
        public double minLat { get; set; }
        public double maxLon { get; set; }
        public double maxLat { get; set; }
        public int sLevel { get; set; }
        public int eLevel { get; set; }
        public string OperateFunction { get; set; }
        public string StartTime { get; set; }
        public string LastTime { get; set; }
        public ulong TotalTilesNum { get; set; }
        public ulong NowTileNum { get; set; }
        public ulong SuccessNum { get; set; }

        public string SavePath { get; set; }

    }

    public class MenuItem
    {
        public MenuItem()
        {
            this.Items = new ObservableCollection<MenuChildItem>();
        }

        public string Title { get; set; }

        public ObservableCollection<MenuChildItem> Items { get; set; }
    }

    public class MenuChildItem
    {
        public string Title { get; set; }
    }
}
