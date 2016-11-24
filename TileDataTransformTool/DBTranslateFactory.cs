//|-----------------------------------------------------------------------------|
//|                                                                             |                                
//|  google level start from 1 180°*180 ° left top                              |
//|  kq levele start from 0 36°*36°  left botoom                                |
//|  this factory translate google tile data to kq tile data                    |
//|  get tile image from google data source , insert into kq database file      |    
//|                                                                             |
//|-----------------------------------------------------------------------------|
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TileDataTransformTool
{

    public class TileInfo
    {
        public int Index { get; set; }
        public int Level { get; set; }
        public int Row { get; set; }
        public int Col { get; set; } 
    }
    public sealed class DBTranslateFactory:IDisposable
    {
        public delegate void StatusChangeDele(string info);
        public static event StatusChangeDele statusChanged;

        public delegate void ProgressChangeDele(ulong now, ulong sum, ulong successs);
        public static event ProgressChangeDele ProgressChanged;

        public static MongoDBReaderHelper DBReaderHelper;
        /// <summary>
        /// google tile size
        /// </summary>
        private static int TILESIZE = 256;

        public static string kqpath = "F:\\Cache";
        /// <summary>
        /// google level to kq level mast plus level offet
        /// </summary>
        private static int _fromGoogleLevelOffset = 5;

        private static object LockObj = new object();
        private static object LockObj1 = new object();
        private static object LockObj2 = new object();

        private static List<int> minRow = new List<int>();
        private static List<int> minCol = new List<int>();
        private static List<int> maxRow = new List<int>();
        private static List<int> maxCol = new List<int>();
        private static List<int> kqLevel = new List<int>();
        private static List<int> sumTile = new List<int>();
        private static ulong nowTile = 0;
        private static List<int> nowRow = new List<int>();
        private static List<int> nowCol = new List<int>();
        private static int levelStatus = 0;
        private static bool isLast = false;
        private static ulong sumTileNum = 0;
        private static ulong successNum = 0;
        public static ulong configFinished = 0;
        public static ulong configSucceed = 0;

        private static AutoResetEvent[] doneEvents;
        private static object[] images;

        private static readonly Stopwatch Watch = new Stopwatch();

        private static Dictionary<string, byte[]> imageCache = new Dictionary<string, byte[]>();

        //private static object pauseLock = new object();

        private static bool pause = false;
        public static bool Pause
        {
            get { return pause; }
            set 
            {
                 pause = value;                            
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        ~DBTranslateFactory()
        {
            this.Dispose();
        }

        private static Dictionary<string, KQDBTileDataPack> kqfileList = new Dictionary<string, KQDBTileDataPack>();

        /// <summary>
        /// kq data file rank 
        /// </summary>
        public const int KqLevel0_tilelevel = 0;
        public const int KqLevel0_startlevel = 0;
        public const int KqLevel0_endlevel = 5;

        public const int KqLevel1_tilelevel = 3;
        public const int KqLevel1_startlevel = 6;
        public const int KqLevel1_endlevel = 10;

        public const int KqLevel2_tilelevel = 7;
        public const int KqLevel2_startlevel = 11;
        public const int KqLevel2_endlevel = 17;

        /// <summary>
        /// get db file direction floder path
        /// </summary>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        /// <returns></returns>
        public static string GetKqFileParentDir(int kq_level, int kq_row, int kq_col)
        {
            int _startlevel = 0;
            int _endlevel = 0;
            int _tilelevel = 0;
            int _tilerow = 0;
            int _tilecol = 0;

            if (kq_level >= KqLevel0_startlevel && kq_level <= KqLevel0_endlevel)
            {
                _startlevel = KqLevel0_startlevel;
                _endlevel = KqLevel0_endlevel;
                _tilelevel = KqLevel0_tilelevel;
                _tilerow = 0;
                _tilecol = 0;
            }
            else if (kq_level >= KqLevel1_startlevel && kq_level <= KqLevel1_endlevel)
            {
                _startlevel = KqLevel1_startlevel;
                _endlevel = KqLevel1_endlevel;
                _tilelevel = KqLevel1_tilelevel;
                _tilerow = kq_row / ((int)Math.Pow(2.0, kq_level - _tilelevel));
                _tilecol = kq_col / ((int)Math.Pow(2.0, kq_level - _tilelevel));
            }
            else if (kq_level >= KqLevel2_startlevel && kq_level <= KqLevel2_endlevel)
            {
                _startlevel = KqLevel2_startlevel;
                _endlevel = KqLevel2_endlevel;
                _tilelevel = KqLevel1_tilelevel;//这里放在第上一级目录下
                _tilerow = kq_row / ((int)Math.Pow(2.0, kq_level - _tilelevel));
                _tilecol = kq_col / ((int)Math.Pow(2.0, kq_level - _tilelevel));
            }
            return String.Format("{0:D2}-{1:D8}-{2:D8}", _tilelevel, _tilerow, _tilecol);
        }

        /// <summary>
        /// get db file name 
        /// </summary>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        /// <returns></returns>
        public static string GetKqFileNameByKongQingTile(int kq_level, int kq_row, int kq_col)
        {
            int _startlevel = 0;
            int _endlevel = 0;
            int _tilelevel = 0;
            int _tilerow = 0;
            int _tilecol = 0;

            if (kq_level >= KqLevel0_startlevel && kq_level <= KqLevel0_endlevel)
            {
                _startlevel = KqLevel0_startlevel;
                _endlevel = KqLevel0_endlevel;
                _tilelevel = KqLevel0_tilelevel;
                _tilerow = 0;
                _tilecol = 0;
            }
            else if (kq_level >= KqLevel1_startlevel && kq_level <= KqLevel1_endlevel)
            {
                _startlevel = KqLevel1_startlevel;
                _endlevel = KqLevel1_endlevel;
                _tilelevel = KqLevel1_tilelevel;
                _tilerow = kq_row / ((int)Math.Pow(2.0, kq_level - _tilelevel));
                _tilecol = kq_col / ((int)Math.Pow(2.0, kq_level - _tilelevel));
            }
            else if (kq_level >= KqLevel2_startlevel && kq_level <= KqLevel2_endlevel)
            {
                _startlevel = KqLevel2_startlevel;
                _endlevel = KqLevel2_endlevel;
                _tilelevel = KqLevel2_tilelevel;
                _tilerow = kq_row / ((int)Math.Pow(2.0, kq_level - _tilelevel));
                _tilecol = kq_col / ((int)Math.Pow(2.0, kq_level - _tilelevel));
            }
            return String.Format("{0:D2}-{1:D2}-{2:D2}-{3:D8}-{4:D8}", _startlevel, _endlevel, _tilelevel, _tilerow, _tilecol);
        }

        /// <summary>
        /// get full path of db file
        /// </summary>
        /// <param name="directionname"></param>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        /// <returns></returns>
        public static string GetKqFileFullPathByKongQingTile(string directionname, int kq_level, int kq_row, int kq_col)
        {
            return Path.Combine(directionname, string.Format(@"{0}\{1}.kq", GetKqFileParentDir(kq_level, kq_row, kq_col), GetKqFileNameByKongQingTile(kq_level, kq_row, kq_col)));
        }

        /// <summary>
        /// insert KQ image object to current db file
        /// </summary>
        /// <param name="image"></param>
        /// <param name="_kqDir"></param>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        public static void InsertKongQingTileImage(Image image, string _kqDir, int kq_level, int kq_row, int kq_col)
        {
            if (image == null)
            {
                return;
            }
            KQDBTileDataPack curKQ = GetCurrentKqData(_kqDir, kq_level, kq_row, kq_col, true);//没有创建新的 
            if (curKQ != null)
            {
                curKQ.InsertImage(image, kq_level, kq_row, kq_col);
            }
        }

        /// <summary>
        /// insert image buffer to db file 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="_kqDir"></param>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        public static void InsertKongQingTileImageBuffer(Byte[] buffer, string _kqDir, int kq_level, int kq_row, int kq_col)
        {
            if (buffer == null)
            {
                return;
            }
            KQDBTileDataPack curKQ = GetCurrentKqData(_kqDir, kq_level, kq_row, kq_col, true);//if non-existent , creat new file
            if (curKQ != null)
            {
                curKQ.InsertImageBuffer(buffer, kq_level, kq_row, kq_col);
            }
        }

        /// <summary>
        /// insert image file to db file
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="_kqDir"></param>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        public static void InsertKongQingTileImageFile(string filepath, string _kqDir, int kq_level, int kq_row, int kq_col)
        {
            if (!File.Exists(filepath))
            {
                return;
            }
            KQDBTileDataPack curKQ = GetCurrentKqData(_kqDir, kq_level, kq_row, kq_col, true);//if non-existent , creat new file
            if (curKQ != null)
            {
                curKQ.InsertImageFile(filepath, kq_level, kq_row, kq_col);
            }
        }

        /// <summary>
        /// get image buffer form kq db file
        /// </summary>
        /// <param name="_kqDir"></param>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        /// <returns></returns>
        public static Byte[] GetKongQingTileImageBuffer(string _kqDir, int kq_level, int kq_row, int kq_col)
        {
            KQDBTileDataPack curKQ = GetCurrentKqData(_kqDir, kq_level, kq_row, kq_col, false);//when request is performance , do not have to creat new file
            if (curKQ != null)
            {
                return curKQ.GetTileImageBuffer(kq_level, kq_row, kq_col);
            }
            return null;
        }

        /// <summary>
        /// get image object from kq db file
        /// </summary>
        /// <param name="_kqDir"></param>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        /// <returns></returns>
        public static Image GetKongQingTileImage(string _kqDir, int kq_level, int kq_row, int kq_col)
        {
            KQDBTileDataPack curKQ = GetCurrentKqData(_kqDir, kq_level, kq_row, kq_col, false);
            if (curKQ != null)
            {
                return curKQ.GetTileImage(kq_level, kq_row, kq_col);
            }
            return null;
        }

        /// <summary>
        /// get the db file from tile information
        /// </summary>
        /// <param name="_kqDir"></param>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        /// <param name="bCreateNew">true : create a new db file ; false :return null</param>
        /// <returns></returns>
        private static KQDBTileDataPack GetCurrentKqData(string _kqDir, int kq_level, int kq_row, int kq_col, bool bCreateNew)
        {
            lock (LockObj1)
            {
                KQDBTileDataPack curKQ = null;
                string kqpath = GetKqFileFullPathByKongQingTile(_kqDir, kq_level, kq_row, kq_col);
                string kqname = Path.GetFileNameWithoutExtension(kqpath);
                if (kqfileList.ContainsKey(kqpath))
                {
                    curKQ = kqfileList[kqpath];
                }
                else
                {
                    if (File.Exists(kqpath) || bCreateNew)//不存在的的情况下是否创建新的
                    {
                        if (!Directory.Exists(Path.GetDirectoryName(kqpath)))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(kqpath));
                        }

                        curKQ = new KQDBTileDataPack(kqpath);
                        curKQ.OpenDb();
                        kqfileList.Add(kqpath, curKQ);
                    }
                }
                return curKQ;
            }
        }

        /// <summary>
        /// calculate tile information by longitude and latitude
        /// </summary>
        /// <param name="kq_level"></param>
        /// <param name="lon"></param>
        /// <param name="lat"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public static bool GetKongQingRowCol(int kq_level, double lon, double lat, ref int row, ref int col)
        {
            double tileSize = 36 * Math.Pow(0.5, kq_level);
            row = GetKQRowFromLatitude(lat, tileSize);
            col = GetColFromLongitude(lon, tileSize);

            if (row >= 5 * Math.Pow(2, kq_level))
            {
                if (statusChanged != null)
                {
                    statusChanged("GetKongQingRowCol error");
                }
            }
            if (row >= 10 * Math.Pow(2, kq_level))
            {
                if (statusChanged != null)
                {
                    statusChanged("GetKongQingRowCol error");
                }
            }
            return true;
        }

        /// <summary>
        /// get column number from longitude and tile level
        /// </summary>
        /// <param name="longitude"></param>
        /// <param name="tileSize"></param>
        /// <returns></returns>
        public static int GetColFromLongitude(double longitude, double tileSize)
        {
            if (longitude == 180)
            {
                return (int)(360 / tileSize) - 1;
            }
            return (int)Math.Floor((Math.Abs(-180.0 - longitude) % 360) / tileSize);
        }
        /// <summary>
        /// get row number from latitude and tile level , only useful when row start from botoom
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="tileSize"></param>
        /// <returns></returns>
        public static int GetKQRowFromLatitude(double latitude, double tileSize)
        {
            return (int)Math.Floor((Math.Abs(-90.0 - latitude) % 180) / tileSize);
        }

        /// <summary>
        /// get row number from latitude and tile level , only useful when row start from top
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="tileSize"></param>
        /// <returns></returns>
        public static int GetGoogleRowFromLatitude(double latitude, double tileSize)
        {
            return (int)Math.Floor((Math.Abs(180.0 - latitude) % 360) / tileSize);
        }
        /// <summary>
        /// get longitude and latitude range box by tile information
        /// </summary>
        /// <param name="kq_level"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="lonlatbox"></param>
        /// <returns></returns>
        public static bool GetLonLatBoundByRowCol(int kq_level, int row, int col, ref Envelope lonlatbox)
        {
            double tileSize = 36 * Math.Pow(0.5, kq_level);

            if (row >= 5 * (int)Math.Pow(2.0, kq_level) || col >= 10 * (int)Math.Pow(2.0, kq_level))
            {
                return false;
            }

            double minLon = GetLeftLongitudeFromcol(col, tileSize);
            double minLat = GetBottomLatitudeFromRow(row, tileSize);

            double maxLon = minLon + tileSize;
            double maxLat = minLat + tileSize;

            lonlatbox.Init(minLon, maxLon, minLat, maxLat);

            return true;
        }

        /// <summary>
        /// get the bottom latitude from row , only useful when tile row begin from bottom
        /// </summary>
        /// <param name="row"></param>
        /// <param name="tileSize"></param>
        /// <returns></returns>
        public static double GetBottomLatitudeFromRow(int row, double tileSize)
        {
            return row * tileSize - 90;
        }
        /// <summary>
        /// get left longitude from col , only useful when tile column begin from left
        /// </summary>
        /// <param name="longitude"></param>
        /// <param name="tileSize"></param>
        /// <returns></returns>
        public static double GetLeftLongitudeFromcol(int col, double tileSize)
        {
            return col * tileSize - 180;
        }

        /// <summary>
        /// get top latitude from row , only useful when tile row begin from top
        /// </summary>
        /// <param name="row"></param>
        /// <param name="tileSize"></param>
        /// <returns></returns>
        public static double GetTopLatitudeFromRow(int row, double tileSize)
        {
            return 180 - row * tileSize;
        }

        /// <summary>
        /// get right longitude from col , only useful when tile column begin from right
        /// </summary>
        /// <param name="col"></param>
        /// <param name="tileSize"></param>
        /// <returns></returns>
        public static double GetRightLongitudeFromcol(int col, double tileSize)
        {
            return 180 - col * tileSize;
        }

        /// <summary>
        /// get google tile information from level and longitude latitude
        /// </summary>
        /// <param name="google_level"></param>
        /// <param name="lon"></param>
        /// <param name="lat"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public static bool GetGoogleRowCol(int google_level, double lon, double lat, ref int row, ref int col)
        {
            double tileSize = 180 * Math.Pow(0.5, google_level - 1);
            row = GetGoogleRowFromLatitude(lat, tileSize);
            col = GetColFromLongitude(lon, tileSize);

            if (row >= 2 * Math.Pow(2, google_level - 1))
            {
                if (statusChanged != null)
                {
                    statusChanged("GetGoogleRowCol error");
                }
            }
            if (col >= 2 * Math.Pow(2, google_level - 1))
            {
                if (statusChanged != null)
                {
                    statusChanged("GetGoogleRowCol error");
                }
            }
            return true;
        }

        /// <summary>
        /// get google tile longitude latitude range 
        /// </summary>
        /// <param name="google_level"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="lonlatbox"></param>
        /// <returns></returns>
        public static bool GetGoogleLonLatBoundByRowCol(int google_level, int row, int col, ref Envelope lonlatbox)
        {
            double tileSize = 180 * Math.Pow(0.5, google_level - 1);

            if (row >= 2 * (int)Math.Pow(2.0, google_level - 1) || col >= 2 * (int)Math.Pow(2.0, google_level - 1))
            {
                return false;
            }

            double minLon = GetLeftLongitudeFromcol(col, tileSize);
            double maxLat = GetTopLatitudeFromRow(row, tileSize);

            double maxLon = minLon + tileSize;
            double minLat = maxLat - tileSize;

            lonlatbox.Init(minLon, maxLon, minLat, maxLat);

            return true;
        }

        public static bool InsertOperationByMultiThreading(int theratNum, int skqlevel, int ekqlevel, double maxLon, double maxLat, double minLon, double minLat)
        {
            if (skqlevel > 16 || ekqlevel > 16)
                return false;
            if (maxLon < -180 || maxLon > 180 || minLon < -180 || minLon > 180 || maxLat < -90 || maxLat > 90 || minLat < -90 || minLat > 90)
                return false;

            int sRow = 0;
            int sCol = 0;
            int eRow = 0;
            int eCol = 0;

            isLast = false;
            nowTile = 0;
            levelStatus = 0;
            sumTileNum = 0;
            successNum = configSucceed;
            minRow.Clear();
            minCol.Clear();
            maxRow.Clear();
            maxCol.Clear();
            kqLevel.Clear();
            sumTile.Clear();
            nowCol.Clear();
            nowRow.Clear();
            
            for (int level = skqlevel; level <= ekqlevel; level++)
            {
                if (GetKongQingRowCol(level, minLon, minLat, ref sRow, ref sCol) &&
                        GetKongQingRowCol(level, maxLon, maxLat, ref eRow, ref eCol))
                {
                    minRow.Add(sRow);
                    minCol.Add(sCol);
                    maxRow.Add(eRow);
                    maxCol.Add(eCol);
                    kqLevel.Add(level);
                    sumTile.Add((eRow - sRow + 1) * (eCol - sCol + 1));
                    sumTileNum += (ulong)((eRow - sRow + 1) * (eCol - sCol + 1));
                    nowRow.Add(sRow);
                    nowCol.Add(sCol);
                }
            }

            if (kqLevel.Count != 0)
            {
                for (int i = 0; i < theratNum; i++)
                {
                    Thread t = new Thread(StartTask);
                    t.Start();
                }
            }

            if (kqpath[kqpath.Length - 1] != "\\".ToCharArray()[0])
            {
                kqpath += "\\";
            }

            return true;            
        }

        public async static void StartTask()
        {
            int row = 0;
            int col = 0;
            int kqlevel = 0;
            while (configFinished != 0 && nowTile < configFinished)
            {
                GetNextImage(ref kqlevel, ref row, ref col);
                nowTile++;
            }
            while (GetNextImage(ref kqlevel, ref row, ref col))
            {
                Console.WriteLine(string.Format("GetNextImage {0}-{1}-{2}", kqlevel, row, col));
                bool result = await InsertSignalImageToKqDb("", kqlevel, row, col);
                nowTile++;            
                
                if (result)
                {
                    successNum++;
                }

                if (ProgressChanged != null)
                {
                    ProgressChanged(nowTile, sumTileNum, successNum);
                }
            }
        }


        public delegate void TaskFIinishedDele();
        public static event TaskFIinishedDele TaskFinished;
        public static bool GetNextImage(ref int level, ref int row, ref int col)
        {
            lock (LockObj)
            {

                while (pause)
                {
                    Thread.Sleep(1000);
                }
                
                if (isLast)
                {
                    if (TaskFinished != null)
                    {
                        TaskFinished();
                    }
                    Console.WriteLine("islast!");
                    return false;
                }
                if (nowCol[levelStatus] <= maxCol[levelStatus] && nowRow[levelStatus] <= maxRow[levelStatus] && nowRow[levelStatus] >= minRow[levelStatus] && nowCol[levelStatus] >= minCol[levelStatus])
                {
                    row = nowRow[levelStatus];
                    col = nowCol[levelStatus];
                    level = kqLevel[levelStatus];
                    if (nowCol[levelStatus] < maxCol[levelStatus])
                    {
                        nowCol[levelStatus]++;
                        return true;
                    }
                    else if (nowCol[levelStatus] == maxCol[levelStatus] && nowRow[levelStatus] < maxRow[levelStatus])
                    {
                        nowCol[levelStatus] = minCol[levelStatus];
                        nowRow[levelStatus]++;

                        return true;
                    }
                    else if (nowRow[levelStatus] == maxRow[levelStatus] && nowCol[levelStatus] == maxCol[levelStatus] && levelStatus < kqLevel.Count - 1)
                    {
                        levelStatus++;
                        nowCol[levelStatus] = minCol[levelStatus];
                        nowRow[levelStatus] = minRow[levelStatus];

                        return true;
                    }
                    else if (nowRow[levelStatus] == maxRow[levelStatus] && nowCol[levelStatus] == maxCol[levelStatus] && levelStatus == kqLevel.Count - 1)
                    {
                        isLast = true;

                        return true;
                    }


                }
            }

            return false;
            
        }

        public async static Task<bool> InsertSignalImageToKqDb(string dir, int kqlevel, int row, int col)
        {
            try
            {
                byte[] buffer = await GetKQTileImageBuffer(dir, kqlevel, row, col);
                if (buffer == null)
                {
                    return false;
                }
                InsertKongQingTileImageBuffer(buffer, kqpath, kqlevel, row, col);
               
            }
            catch(System.Exception e)
            {
                System.Console.WriteLine(e.Message + e.StackTrace);
                return false;
            }
            return true;
        }
        /// <summary>
        /// insert kq tile to database from google database , set the kq level and longitude latitude range
        /// </summary>
        /// <param name="kqlevel"></param>
        /// <param name="maxLon"></param>
        /// <param name="maxLat"></param>
        /// <param name="minLon"></param>
        /// <param name="minLat"></param>
        public async static Task<bool> InsertKQImageByLonLatRange(int kqlevel,double maxLon, double maxLat, double minLon, double minLat)
        {
            if(kqlevel>16)
                return false;
            if(maxLon <-180 || maxLon > 180 || minLon < -180 || minLon > 180 || maxLat <-90 || maxLat > 90 || minLat <-90 || minLat >90)
                return false;

            int sRow = 0;
            int sCol = 0;
            int eRow = 0;
            int eCol = 0;
            if (GetKongQingRowCol(kqlevel, minLon, minLat, ref sRow, ref sCol) &&
                    GetKongQingRowCol(kqlevel, maxLon, maxLat, ref eRow, ref eCol))
            {
                for (int row = sRow; row <= eRow; row++)
                {
                    for (int col = sCol; col <= eCol; col++)
                    {
                        byte[] buffer = await GetKQTileImageBuffer("", kqlevel, row, col);

                        InsertKongQingTileImageBuffer(buffer, kqpath, kqlevel, row, col);
                    }
                }
            }
            else
            {
                return false;
            }


            return true;
        }


        /// <summary>
        /// get kq tile image buffer from google
        /// </summary>
        /// <param name="_dbDir"></param>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        /// <returns></returns>
        public async static Task<byte[]> GetKQTileImageBuffer(string _dbDir, int kq_level, int kq_row, int kq_col) 
        {
            MemoryStream _TargetMemory = null;
            Image kqimage = await GetKQTileImage(_dbDir, kq_level, kq_row, kq_col);
            if(kqimage != null)
            {
                _TargetMemory = new MemoryStream();
                kqimage.Save(_TargetMemory, ImageFormat.Jpeg);
                kqimage.Dispose();
                kqimage = null;
                return _TargetMemory.GetBuffer();
            }
            return null;
        }

        /// <summary>
        /// get kq tile image from google tile
        /// </summary>
        /// <param name="_dbDir"></param>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        /// <returns></returns>
        public async static Task<Image> GetKQTileImage(string _dbDir, int kq_level, int kq_row, int kq_col)
        {
            if(kq_level + _fromGoogleLevelOffset > 20)
            {
                return null;
            }

            Envelope bound = new Envelope();
            if(GetLonLatBoundByRowCol(kq_level,kq_row,kq_col,ref bound))
            {
                if(bound.MaxY > 90 || bound.MaxY < - 90)
                {
                    return null;
                }
            }
            try
            {
                Image kqimage = null;
                BigImage bigimage = await GetBigImageFromGoogleTile(_dbDir, kq_level + _fromGoogleLevelOffset,bound.MinX, bound.MinY, bound.MaxX, bound.MaxY);


                if(bigimage != null)
                {
                    kqimage = bigimage.GetSubImage(bound, 512, 512);
                    bigimage.Dispose();
                    bigimage = null;
                    return kqimage;
                }
            }
            catch
            {
                return null;
            }
            return null;
        }


        /// <summary>
        /// creat big image from google tiles
        /// </summary>
        /// <param name="_dbDir"></param>
        /// <param name="google_level"></param>
        /// <param name="sLon"></param>
        /// <param name="sLat"></param>
        /// <param name="eLon"></param>
        /// <param name="eLat"></param>
        /// <returns></returns>
        public async static Task<BigImage> GetBigImageFromGoogleTile(string _dbDir, int google_level, double sLon, double sLat, double eLon, double eLat)
        {
            int sRow = 0;
            int sCol = 0;
            int eRow = 0;
            int eCol = 0;
            int nFindCount = 0;
            int count = 0;
            try
            {
                ///根据经纬度范围获取对应google 行列号范围
                if (GetGoogleRowCol(google_level, sLon, eLat, ref sRow, ref sCol) &&
                    GetGoogleRowCol(google_level, eLon, sLat, ref eRow, ref eCol))
                {
                    count = (eCol - sCol + 1) * (eRow - sRow + 1);
                    if (count == 0 || count < 0)
                    {
                        return null;
                    }

                    //images = new object[count];

                    //doneEvents = new AutoResetEvent[count];

                    //for (int i = 0; i < images.Length; i++)
                    //{
                    //    images[i] = "null";
                    //    doneEvents[i] = new AutoResetEvent(false);
                    //}
                    //int index = 0;
                    //for (int row = sRow; row <= eRow; row++)
                    //{
                    //    for (int col = sCol; col <= eCol; col++)
                    //    {
                    //        TileInfo info = new TileInfo() { Index = index, Level = google_level, Row = row, Col = col };
                    //        ThreadPool.QueueUserWorkItem(GetGoogleTileImageAll, info);
                    //        index++;
                    //    }
                    //}

                    //WaitHandle.WaitAll(doneEvents);
                    //Console.WriteLine("All calculations are complete.");

                    //for (int i = 0; i < images.Length; i++)
                    //{
                    //    if (images[i] == null)
                    //    {
                    //        return null;
                    //    }
                    //}
                    BigImage gb = new BigImage() { googleLevel = google_level };
                    
                    //将这些tile组合成一个大的 Image,计算出这个image实际经纬度范围，以及像素值范围
                    int bigimageWidth = TILESIZE * (eCol - sCol + 1);
                    int bigimageHeight = TILESIZE * (eRow - sRow + 1);
                    gb.BigImg = new Bitmap(bigimageWidth, bigimageHeight, PixelFormat.Format24bppRgb);
                    Graphics _CanvasGraphics = Graphics.FromImage(gb.BigImg);
                    _CanvasGraphics.Clear(Color.Black);
                    _CanvasGraphics.CompositingQuality = CompositingQuality.HighQuality;
                    _CanvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                    _CanvasGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    _CanvasGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    int x0 = 0;
                    int y0 = 0;
                    //int[] a = new int[10];
                    //index = 0;
                    for (int row = sRow; row <= eRow; row++)
                    {
                        for (int col = sCol; col <= eCol; col++)
                        {
                            //ThreadPool.QueueUserWorkItem()
                            Image tileimage = await GetGoogleTileImage(google_level, row, col);
                            if (tileimage != null)
                            {
                                _CanvasGraphics.DrawImage((Image)tileimage, new System.Drawing.Point(x0, y0));
                                //画范围框
                                ((Image)(tileimage)).Dispose();
                                tileimage = null;
                                nFindCount++;
                                //index++;
                            }
                            else
                            {
                                //如果有某张图片不存在的情况怎么办,取下一级？？
                                _CanvasGraphics.Dispose();
                                _CanvasGraphics = null;
                                gb.Dispose();
                                gb = null;
                                return null;
                            }
                            x0 += TILESIZE;
                        }
                        y0 += TILESIZE;
                        x0 = 0;

                    }
                    _CanvasGraphics.Dispose();
                    _CanvasGraphics = null;
                    if (nFindCount == 0)
                    {
                        return null;
                    }

                    //更新该bigimage的范围信息
                    gb.ImgRange = new Envelope();
                    Envelope tilebox = new Envelope();
                    if (!GetGoogleLonLatBoundByRowCol(google_level, eRow, sCol, ref tilebox))
                    {
                        Console.WriteLine("GetGoogleLonLatBoundByRowCol error");
                        gb.Dispose();
                        gb = null;
                        return null;
                    }
                    double minLon = tilebox.MinX;
                    double minLat = tilebox.MinY;

                    //浮点计算会出现一些细微误差
                    double offset = minLon - sLon;
                    if (offset > 0)//
                    {
                        minLon = sLon;
                        if (offset > 0.00000001)
                        {
                            Console.WriteLine("offset error");
                        }
                    }
                    offset = minLat - sLat;
                    if (offset > 0)//
                    {
                        minLat = sLat;
                        if (offset > 0.00000001)
                        {
                            Console.WriteLine("offset error");
                        }
                    }

                    if (!GetGoogleLonLatBoundByRowCol(google_level, sRow, eCol, ref tilebox))
                    {
                        Console.WriteLine("GetGoogleLonLatBoundByRowCol 1 error");
                        gb.Dispose();
                        gb = null;
                        return null;
                    }

                    double maxLon = tilebox.MaxX;
                    double maxLat = tilebox.MaxY;

                    offset = maxLon - eLon;
                    if (offset < 0)
                    {
                        maxLon = eLon;
                        if (offset < -0.00000001)
                        {
                            Console.WriteLine("offset error");
                        }
                    }
                    offset = maxLat - eLat;
                    if (offset < 0)
                    {
                        maxLat = eLat;
                        if (offset < -0.00000001)
                        {

                            Console.WriteLine("offset error");
                        }
                    }
                    gb.ImgRange = new Envelope(minLon, maxLon, minLat, maxLat);
                    gb.PixelBox = new PixelBound();
                    if (!LonLatBound2PixelBound(google_level, gb.ImgRange, ref gb.PixelBox))
                    {
                        gb.Dispose();
                        gb = null;
                        return null;
                    }
                    return gb;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return null;
        }


        private static void GetGoogleTileImageAll(object index)
        {
            TileInfo threadInfo = (TileInfo)index;
            images[threadInfo.Index] = GetGoogleTileImage(threadInfo.Level, threadInfo.Row, threadInfo.Col).Result;
            //if(i != null)
            //{
            //    images[threadInfo.Index] = i;
            //    i.Dispose();
            //    i = null;
            //}

            doneEvents[threadInfo.Index].Set();
            Console.WriteLine("ThreatPool Num {0} Thread Set.", threadInfo.Index);
        }

        /// <summary>
        /// get google tile image
        /// </summary>
        /// <param name="googlelevel"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public async static Task<Image> GetGoogleTileImage(int googlelevel, int row, int col)
        {
            Image tileimage = null;
            string key = string.Format("{0}-{1}-{2}", googlelevel, row, col);
            if (DBReaderHelper != null)
            {
                try
                {
                    byte[] buffer = null;
                    lock (LockObj2)
                    {
                        if (imageCache.ContainsKey(key))
                        {
                            buffer = imageCache[key];
                            if (buffer != null)
                            {
                                MemoryStream streamImage = new MemoryStream(buffer);
                                tileimage = Image.FromStream(streamImage);
                                streamImage.Close();
                                buffer = null;
                            }
                            return tileimage;
                        }
                    }
                                      
                    
                    buffer = await DBReaderHelper.GetTiled(key);


                    if (buffer != null)
                    {
                        lock (LockObj2)
                        {
                            if (imageCache.Count > 100)
                            {
                                imageCache.Remove(imageCache.First().Key);
                            }
                            imageCache.Add(key, buffer);
                        }
                    }
                    

                    if (buffer != null)
                    {
                        MemoryStream streamImage = new MemoryStream(buffer);
                        tileimage = Image.FromStream(streamImage);
                        streamImage.Close();
                        buffer = null;
                    }
                    return tileimage;  
                    
                    
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            
            return null; 
        }

        /// <summary>
        /// get google tile bitmap
        /// </summary>
        /// <param name="googlelevel"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public async static Task<Bitmap> GetGoogleTileBitmap(int googlelevel, int row, int col)
        {
            Bitmap tileimage = null;
            string key = string.Format("{0}-{1}-{2}", googlelevel, row, col);
            

            if (DBReaderHelper != null)
            {
                try
                {
                    byte[] buffer;
                    //if (imageCache.ContainsKey(key))
                    //{
                    //    buffer = imageCache[key];
                    //}
                    //else
                    //{
                        buffer = await DBReaderHelper.GetTiled(key);
                    //    if (buffer != null)
                    //    {
                    //        if (imageCache.Count > 100)
                    //        {
                    //            imageCache.Remove(imageCache.First().Key);
                    //        }
                    //        imageCache.Add(key, buffer);
                    //    }
                    //}
                    if (buffer != null)
                    {
                        MemoryStream streamImage = new MemoryStream(buffer);
                        tileimage = new Bitmap(streamImage);
                        streamImage.Close();
                        buffer = null;
                    }
                    return tileimage;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return null;
        }
        /// <summary>
        /// longitude latitude range translate to pixel range   google tile
        /// </summary>
        /// <param name="google_level"></param>
        /// <param name="lonlatbox"></param>
        /// <param name="pixelbox"></param>
        /// <returns></returns>
        public static bool LonLatBound2PixelBound(int google_level, Envelope lonlatbox, ref PixelBound pixelbox)
        {
            if (!LonLat2Pixel(google_level, lonlatbox.MinX, lonlatbox.MaxY, ref pixelbox.minPX, ref pixelbox.minPY))
            {
 
                Console.WriteLine("LonLat2Pixel error");
                return false;
            }
            if (!LonLat2Pixel(google_level, lonlatbox.MaxX, lonlatbox.MinY, ref pixelbox.maxPX, ref pixelbox.maxPY))
            {

                Console.WriteLine("LonLat2Pixel error");
                return false;
            }
            if (!pixelbox.IsValid())
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// longitude latitude translate to pixel piont  google tile
        /// </summary>
        /// <param name="google_level"></param>
        /// <param name="lon"></param>
        /// <param name="lat"></param>
        /// <param name="px"></param>
        /// <param name="py"></param>
        /// <returns></returns>
        public static bool LonLat2Pixel(int google_level, double lon, double lat, ref int px, ref int py)
        {
            try
            {
                if (Math.Abs(lon) > 180 || Math.Abs(lat) > 90)
                {
                    Console.WriteLine("LonLat2Pixel error");
                    return false;
                }
                if (lon < 0)
                    lon = -1 * lon;
                else if (lon >= 0)
                    lon = lon + 180;
                if (lat <= 0)
                    lat = Math.Abs(lat - 180);
                else
                {
                    lat = 180 - lat;
                }
                double[] point = new double[2];
                point[0] = lon;
                point[1] = lat;
                int SL = 2 * TILESIZE * (int)Math.Pow(2, google_level - 1);


                px = (int)(point[0] / 360 * SL + .5);
                py = (int)(point[1] / 360 * SL + .5);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

        }

        /// <summary>
        /// Get google file use multithreading
        /// </summary>
        /// <param name="theratNum"></param>
        /// <param name="sglevel"></param>
        /// <param name="eglevel"></param>
        /// <param name="maxLon"></param>
        /// <param name="maxLat"></param>
        /// <param name="minLon"></param>
        /// <param name="minLat"></param>
        /// <returns></returns>
        public static bool CreatGoogleFileByMultiThreading(int theratNum, int sglevel, int eglevel, double maxLon, double maxLat, double minLon, double minLat)
        {
            if (sglevel > 20 || eglevel > 20)
                return false;
            if (maxLon < -180 || maxLon > 180 || minLon < -180 || minLon > 180 || maxLat < -90 || maxLat > 90 || minLat < -90 || minLat > 90)
                return false;

            int sRow = 0;
            int sCol = 0;
            int eRow = 0;
            int eCol = 0;

            isLast = false;
            nowTile = 0;
            levelStatus = 0;
            sumTileNum = 0;
            successNum = configSucceed;
            minRow.Clear();
            minCol.Clear();
            maxRow.Clear();
            maxCol.Clear();
            kqLevel.Clear();
            sumTile.Clear();
            nowCol.Clear();
            nowRow.Clear();

            Console.WriteLine("Start Working");
            try
            {
                for (int level = sglevel; level <= eglevel; level++)
                {
                    if (GetGoogleRowCol(level, minLon, minLat, ref eRow, ref sCol) &&
                            GetGoogleRowCol(level, maxLon, maxLat, ref sRow, ref eCol))
                    {
                        minRow.Add(sRow);
                        minCol.Add(sCol);
                        maxRow.Add(eRow);
                        maxCol.Add(eCol);
                        kqLevel.Add(level);
                        sumTile.Add((eRow - sRow + 1) * (eCol - sCol + 1));
                        sumTileNum += (ulong)((eRow - sRow + 1) * (eCol - sCol + 1));
                        nowRow.Add(sRow);
                        nowCol.Add(sCol);
                    }
                }

                //if (kqLevel.Count != 0)
                //{
                //    for (int i = 0; i < theratNum; i++)
                //    {
                Thread t = new Thread(StartNewTask);
                t.IsBackground = true;
                t.Start();
                //    }
                //}

                if (kqpath[kqpath.Length - 1] != "\\".ToCharArray()[0])
                {
                    kqpath += "\\";
                }

                Console.WriteLine(kqpath);
            }
            catch (Exception ex)
            {
                
                Console.WriteLine(ex.Message);
                return false;
            }           

            return true;
        }

        /// <summary>
        /// diferent thread start function
        /// </summary>
        public async static void StartNewTask()
        {
            int row = 0;
            int col = 0;
            int level = 0;
            while (configFinished != 0 && nowTile < configFinished)
            {
                GetNextImage(ref level, ref row, ref col);
                nowTile++;
            }
            while (GetNextImage(ref level, ref row, ref col))
            {
                Console.WriteLine(string.Format("get tile {0}-{1}-{2}", level, row, col));
                var result = await CreatSignalGoogleImage(level, row, col);
                nowTile++;
                if (result)
                {
                    successNum++;
                }
                if (ProgressChanged != null)
                {
                    ProgressChanged(nowTile, sumTileNum, successNum);
                }
            }
        }

        /// <summary>
        /// get a specific picture 
        /// </summary>
        /// <param name="glevel"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public async static Task<bool> CreatSignalGoogleImage(int glevel, int row, int col)
        {
            Bitmap buffer = await GetGoogleTileBitmap(glevel, row, col);
            if(buffer == null)
            {
                return false;
            }
            SaveGoogleTile(glevel, row, col, buffer);
            return true;
        }

        /// <summary>
        /// save google tile picture
        /// </summary>
        /// <param name="glevel"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="tile"></param>
        private static void SaveGoogleTile(int glevel, int row, int col, Bitmap tile)
        {           
            string fullpath = string.Format("{0}GoogleTile\\{1}", kqpath, GetGoogleTilePath(glevel, row, col));
            Console.WriteLine(string.Format("SaveGoogleTile {0}-{1}-{2}  {3}", glevel, row, col, fullpath));
            string dic = Path.GetDirectoryName(fullpath);
            if(!Directory.Exists(dic))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(dic);
                directoryInfo.Create();
                Console.WriteLine("creat path" + dic);
            }
            try
            {
                Console.WriteLine("start save");
                Bitmap bit = new Bitmap(256, 256, PixelFormat.Format16bppRgb555);
                Graphics draw = Graphics.FromImage(bit);
                draw.DrawImage(tile, 0, 0);
                bit.Save(fullpath, ImageFormat.Jpeg);
                Console.WriteLine("end save");
                draw.Dispose();
                bit.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            
        }

        /// <summary>
        /// get picture save path
        /// </summary>
        /// <param name="glevel"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        private static string GetGoogleTilePath(int glevel, int row, int col)
        {
            string path = "";
            path = string.Format("{0}\\{1}\\{2}.jpg", glevel, row, col);
            return path;
        }

        public static bool CreatPartialMongoDB(int theratNum, int sglevel, int eglevel, double maxLon, double maxLat, double minLon, double minLat)
        {
            if (sglevel > 20 || eglevel > 20)
                return false;
            if (maxLon < -180 || maxLon > 180 || minLon < -180 || minLon > 180 || maxLat < -90 || maxLat > 90 || minLat < -90 || minLat > 90)
                return false;

            int sRow = 0;
            int sCol = 0;
            int eRow = 0;
            int eCol = 0;

            isLast = false;
            nowTile = 0;
            levelStatus = 0;
            sumTileNum = 0;
            successNum = configSucceed;
            minRow.Clear();
            minCol.Clear();
            maxRow.Clear();
            maxCol.Clear();
            kqLevel.Clear();
            sumTile.Clear();
            nowCol.Clear();
            nowRow.Clear();

            Console.WriteLine("Start Working");
            try
            {
                for (int level = sglevel; level <= eglevel; level++)
                {
                    if (GetGoogleRowCol(level, minLon, minLat, ref eRow, ref sCol) &&
                            GetGoogleRowCol(level, maxLon, maxLat, ref sRow, ref eCol))
                    {
                        minRow.Add(sRow);
                        minCol.Add(sCol);
                        maxRow.Add(eRow);
                        maxCol.Add(eCol);
                        kqLevel.Add(level);
                        sumTile.Add((eRow - sRow + 1) * (eCol - sCol + 1));
                        sumTileNum += (ulong)((eRow - sRow + 1) * (eCol - sCol + 1));
                        nowRow.Add(sRow);
                        nowCol.Add(sCol);
                    }
                }

                //if (kqLevel.Count != 0)
                //{
                //    for (int i = 0; i < theratNum; i++)
                //    {
                Thread t = new Thread(StartNewNewTask);
                t.IsBackground = true;
                t.Start();
                //    }
                //}

                if (kqpath[kqpath.Length - 1] != "\\".ToCharArray()[0])
                {
                    kqpath += "\\";
                }

                Console.WriteLine(kqpath);
            }
            catch (Exception ex)
            {
                return false;
                Console.WriteLine(ex.Message);
            }

            return true;
        }

        //public static bool CreatPartialMongoDBbyConfig(int theratNum, int sglevel, int eglevel, double maxLon, double maxLat, double minLon, double minLat , ulong tileTotal, ulong tileFinished, ulong TileSucceed)
        //{
        //    if (sglevel > 20 || eglevel > 20)
        //        return false;
        //    if (maxLon < -180 || maxLon > 180 || minLon < -180 || minLon > 180 || maxLat < -90 || maxLat > 90 || minLat < -90 || minLat > 90)
        //        return false;

        //    int sRow = 0;
        //    int sCol = 0;
        //    int eRow = 0;
        //    int eCol = 0;

        //    isLast = false;
        //    nowTile = 0;
        //    levelStatus = 0;
        //    sumTileNum = 0;
        //    successNum = 0;
        //    configFinished = 0;
        //    configSucceed = 0;
        //    minRow.Clear();
        //    minCol.Clear();
        //    maxRow.Clear();
        //    maxCol.Clear();
        //    kqLevel.Clear();
        //    sumTile.Clear();
        //    nowCol.Clear();
        //    nowRow.Clear();

        //    Console.WriteLine("Start Working");
        //    try
        //    {
        //        for (int level = sglevel; level <= eglevel; level++)
        //        {
        //            if (GetGoogleRowCol(level, minLon, minLat, ref eRow, ref sCol) &&
        //                    GetGoogleRowCol(level, maxLon, maxLat, ref sRow, ref eCol))
        //            {
        //                minRow.Add(sRow);
        //                minCol.Add(sCol);
        //                maxRow.Add(eRow);
        //                maxCol.Add(eCol);
        //                kqLevel.Add(level);
        //                sumTile.Add((eRow - sRow + 1) * (eCol - sCol + 1));
        //                sumTileNum += (ulong)((eRow - sRow + 1) * (eCol - sCol + 1));
        //                nowRow.Add(sRow);
        //                nowCol.Add(sCol);
        //            }
        //        }

        //        if (sumTileNum == tileTotal)
        //        {
        //            configFinished = tileFinished;
        //            configSucceed = TileSucceed;
        //        }

        //        //if (kqLevel.Count != 0)
        //        //{
        //        //    for (int i = 0; i < theratNum; i++)
        //        //    {
        //        Thread t = new Thread(StartNewNewTask);
        //        t.IsBackground = true;
        //        t.Start();
        //        //    }
        //        //}

        //        if (kqpath[kqpath.Length - 1] != "\\".ToCharArray()[0])
        //        {
        //            kqpath += "\\";
        //        }

        //        Console.WriteLine(kqpath);
        //    }
        //    catch (Exception ex)
        //    {
        //        return false;
        //        Console.WriteLine(ex.Message);
        //    }

        //    return true;
        //}
        public async static void StartNewNewTask()
        {
            int row = 0;
            int col = 0;
            int level = 0;
            while (configFinished != 0 && nowTile < configFinished)
            {
                GetNextImage(ref level, ref row, ref col);
                nowTile++;
             
            }
            while (GetNextImage(ref level, ref row, ref col))
            {
                Console.WriteLine(string.Format("get tile {0}-{1}-{2}", level, row, col));
                var result = await CreatSignalMongoDBDocument(level, row, col);
                nowTile++;
                if (result)
                {
                    successNum++;
                }
                if (ProgressChanged != null)
                {
                    ProgressChanged(nowTile, sumTileNum, successNum);
                }
            }
        }

        public async static Task<bool> CreatSignalMongoDBDocument(int glevel, int row, int col)
        {
            string key = string.Format("{0}-{1}-{2}", glevel, row, col);
            if (DBReaderHelper != null)
            {
                try
                {
                    byte[] buffer = await DBReaderHelper.GetTiled(key);
                    if (buffer == null)
                    {
                        return false;
                    }
                    if (!DBReaderHelper.InsertTiledToMongoDB(key, buffer))
                    {
                        buffer = null;
                        return false;
                    }
                    buffer = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }

            return true;
        }

        //public static bool StartWithConfigFile(double minLon, double minLat, double maxLon, double maxLat, int sLevel, int eLevel, ulong tileTotal, ulong tileFinished, ulong tileSucceed, string filePath, string option)
        //{
        //    if (option == "Get Partial MongoDB")
        //    {
        //        return DBTranslateFactory.CreatPartialMongoDBbyConfig(1, sLevel, eLevel, maxLon, maxLat, minLon, minLat, tileTotal, tileFinished, tileSucceed);
        //    }
        //    return false;
        //}
        
        /// <summary>
        /// write in log file
        /// </summary>
        /// <param name="path"></param>
        //public static void Write(string info)
        //{
        //    FileStream fs = new FileStream("logfile.log", FileMode.Append);
        //    StreamWriter sw = new StreamWriter(fs);
        //    //开始写入
        //    sw.WriteLine(info);
        //    //清空缓冲区
        //    sw.Flush();
        //    //关闭流
        //    sw.Close();
        //    fs.Close();
        //}
    }
}
