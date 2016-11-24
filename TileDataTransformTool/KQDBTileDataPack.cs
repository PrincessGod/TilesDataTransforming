//------------------------------------------------
//|  KQ database class                           |
//|  Some basic database field and function      |
//|  2016.9.13                                   |
//------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Drawing;
namespace TileDataTransformTool
{
    /// <summary>
    /// signal database file
    /// </summary>
    public class KQDBTileDataPack
    {
        private int _startlevel;
        private int _endlevel;
        private int _tilelevel;//基础地图级别
        private int _tilerow;
        private int _tilecol;
        private int _imagecount;
        private string _kqfilepath;

        private bool _isopen = false;
        private SQLiteConnection _conn = null;

        public KQDBTileDataPack(string kqpath)
        {
            this._kqfilepath = kqpath;
        }

        /// <summary>
        /// get the tile data count of this database file
        /// </summary>
        /// <returns></returns>
        public int DataCount()
        {
            int nCount = 0;
            using (SQLiteCommand cmd = _conn.CreateCommand())
            {
                string sql = "SELECT Count(*) FROM imgindex";
                cmd.CommandText = sql;
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        nCount = reader.GetInt32(0);
                        break;
                    }
                    reader.Close();
                }
            }
            return nCount;
        }

        /// <summary>
        /// get the certen resource is exist or not
        /// </summary>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        /// <returns></returns>
        public bool Exists(int kq_level, int kq_row, int kq_col)
        {
            string filename = GetKongQingTileName(kq_level, kq_row, kq_col);
            int nCount = 0;
            using (SQLiteCommand cmd = _conn.CreateCommand())
            {
                string sql = string.Format("SELECT Count(*) FROM imgindex where filename='{0}'", filename);
                cmd.CommandText = sql;
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        nCount = reader.GetInt32(0);
                        break;
                    }
                    reader.Close();
                }
            }
            if (nCount == 0)
            {
                return false;
            }
            return true;
        }

        public bool CloseDb()
        {
            if (_isopen && _conn != null)
            {
                _conn.Close();
                _conn.Dispose();
                _conn = null;
                _isopen = false;
            }
            return true;
        }

        /// <summary>
        /// creat new table
        /// </summary>
        /// <returns></returns>
        private bool CreateTable()
        {
            if (_conn != null)
            {
                System.Data.SQLite.SQLiteCommand cmd = new System.Data.SQLite.SQLiteCommand();
                string sql = "CREATE TABLE ImgIndex(filename char(16),imgdata blob,googlelevel char(2),downloadtime char(20))";
                cmd.CommandText = sql;
                cmd.Connection = _conn;
                cmd.ExecuteNonQuery();
                sql = "CREATE INDEX KeyIndex on ImgIndex(filename asc)";
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            return true;
        }

        public void OpenDb()
        {
            if (_isopen && _conn != null)
            {
                return;
            }
            try
            {
                System.Data.SQLite.SQLiteConnectionStringBuilder connstr = new System.Data.SQLite.SQLiteConnectionStringBuilder();
                connstr.DataSource = this._kqfilepath;
                //connstr.Password = "11";
                //_conn = new SQLiteConnection(string.Format(@"Data Source={0};", _kqfilepath));
                _conn = new SQLiteConnection();
                _conn.ConnectionString = connstr.ToString();

                if (File.Exists(this._kqfilepath))
                {
                    _conn.Open();
                    _isopen = true;

                }
                else
                {
                    _conn.Open();
                    if (CreateTable())
                    {
                        _isopen = true;
                    }
                }
                return;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("打开KQDB失败");
                _isopen = false;
                if (_conn != null)
                {
                    _conn = null;
                }
                return;
            }
        }

        /// <summary>
        /// insert image to KQ database file
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="kq_level"></param>
        /// <param name="kq_row"></param>
        /// <param name="kq_col"></param>
        /// <param name="operatemode">0:insert image directly 1:if exist file , return</param>
        public void InsertImageBuffer(Byte[] buffer, int kq_level, int kq_row, int kq_col, int operatemode)
        {
            if (buffer == null || !_isopen || _conn == null)
            {
                return;
            }
            string filename = GetKongQingTileName(kq_level, kq_row, kq_col);            
            try
            {
                if (operatemode == 0)
                {
                }
                else if (operatemode == 1)
                {
                    if (Exists(kq_level, kq_row, kq_col))//exists will return
                    { 
                        return;
                    }
                }
                else
                {
                    return;
                }
                using (SQLiteCommand cmd = _conn.CreateCommand())
                {
                    string sql = string.Format("insert into imgindex values('{0}',@data,null,null)", filename);
                    cmd.CommandText = sql;
                    SQLiteParameter para = new SQLiteParameter("@data", System.Data.DbType.Binary);
                    para.Value = buffer;
                    cmd.Parameters.Add(para);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("插入图片错误");
            }


        }
        public void InsertImageBuffer(Byte[] buffer, int kq_level, int kq_row, int kq_col)
        {
            InsertImageBuffer(buffer, kq_level, kq_row, kq_col, 1);
        }
        
        public void InsertImage(Image tileimage, int kq_level, int kq_row, int kq_col)
        {

            if (tileimage == null || !_isopen || _conn == null)
            {
                return;
            }
            System.IO.MemoryStream _TargetMemory = new MemoryStream();

            tileimage.Save(_TargetMemory, System.Drawing.Imaging.ImageFormat.Jpeg);
            tileimage.Dispose();
            tileimage = null;
            Byte[] buffer = _TargetMemory.GetBuffer();
            InsertImageBuffer(buffer, kq_level, kq_row, kq_col);

            _TargetMemory.Close();
            buffer = null;
        }

        public void InsertImageFile(string filepath, int kq_level, int kq_row, int kq_col)
        {
            if (this._conn == null || !this._isopen)
            {
                return;
            }
            if (File.Exists(filepath))
            {
                try
                {
                    FileStream fs = new FileStream(filepath, FileMode.Open);
                    Byte[] buffer = new Byte[fs.Length];
                    fs.Read(buffer, 0, (int)fs.Length);
                    fs.Close();
                    InsertImageBuffer(buffer, kq_level, kq_row, kq_col);
                    buffer = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("插入文件失败，通过地址");
                }
            }
        }

        public Byte[] GetTileImageBuffer(int kq_level, int kq_row, int kq_col)
        {
            if (this._conn == null || !this._isopen)
            {
                return null;
            }
            Byte[] buffer = null;
            try
            {
                string filename = GetKongQingTileName(kq_level, kq_row, kq_col);
                using (SQLiteCommand cmd = _conn.CreateCommand())
                {
                    //cmd.CommandText = string.Format("SELECT * FROM imgindex", filename);
                    cmd.CommandText = string.Format("SELECT * FROM ImgIndex where filename='{0}'", filename);
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            buffer = reader[1] as byte[];
                            break;
                        }
                        reader.Close();
                    }
                }
            }
            catch (Exception ex)
            {
 
            }
            return buffer;
        }

        public Image GetTileImage(int kq_level, int kq_row, int kq_col)
        {
            if (this._conn == null || !this._isopen)
            {
                return null;
            }
            Image tileimage = null;
            try
            {
                Byte[] buffer = GetTileImageBuffer(kq_level, kq_row, kq_col);
                if (buffer != null)
                {
                    MemoryStream streamImage = new MemoryStream(buffer); //
                    tileimage = Image.FromStream(streamImage);
                    streamImage.Close();
                    buffer = null;
                }
            }
            catch (Exception ex)
            {

            }
            return tileimage;
        }


        private static string GetKongQingTileName(int kq_level, int kq_row, int kq_col)
        {
            return string.Format("{0:D2}-{1:D8}-{2:D8}", kq_level, kq_row, kq_col);
        }

        /// <summary>
        /// get the start and end level, reference level and row&col number by file name
        /// </summary>
        private bool getKqFileInfo()
        {
            try
            {
                string _filename = Path.GetFileNameWithoutExtension(this._kqfilepath);

                string[] strlist = _filename.Split('-');

                this._startlevel = Convert.ToInt32(strlist[0]);
                this._endlevel = Convert.ToInt32(strlist[1]);
                this._tilelevel = Convert.ToInt32(strlist[2]);
                this._tilerow = Convert.ToInt32(strlist[3]);
                this._tilecol = Convert.ToInt32(strlist[4]);
                this._imagecount = calImageCountInDb();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        /// <summary>
        /// calculate tile files' count in certain level in this database file 
        /// </summary>
        /// <param name="tilelevel"></param>
        /// <param name="sublevel"></param>
        /// <returns></returns>
        private int calChildrenOfTileAtSubLevel(int tilelevel, int sublevel)
        {
            if (sublevel < tilelevel)
            {
                return 0;
            }
            return (int)System.Math.Pow(4.0, (sublevel - tilelevel));
        }
        private int calImageCountOfDbAtSubLevel(int sublevel)
        {
            if (sublevel < this._startlevel || sublevel > this._endlevel)
            {
                return 0;
            }

            int StartLevelTileCount = 0;
            //如果不是最低分辨率的那个db，该处不符合 2015.9.29 姬宏江
            if (this._tilelevel == this._startlevel && this._tilelevel == 0)
            {
                StartLevelTileCount = 5 * 10;//* (int)System.Math.Pow(2.0, this._startlevel);
            }
            else if (this._tilelevel == this._startlevel && this._tilelevel != 0)
            {
                StartLevelTileCount = 1;
            }

            else
            {
                StartLevelTileCount = calChildrenOfTileAtSubLevel(this._tilelevel, this._startlevel);
            }

            return StartLevelTileCount * (int)System.Math.Pow(4.0, sublevel - this._startlevel);
        }
        private int calImageCountInDb()
        {
            int allcount = 0;
            for (int sublevel = this._startlevel; sublevel <= this._endlevel; sublevel++)
            {
                allcount += calImageCountOfDbAtSubLevel(sublevel);
            }
            return allcount;// +1;//最后追加一个用来计算最后一张图片的大小,不是OMO不需要
        }
    }
}
