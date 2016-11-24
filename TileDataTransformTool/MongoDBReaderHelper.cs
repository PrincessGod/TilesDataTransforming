//-------------------------------------------------------------------------------------------------------------------------
//|  Using Mongodb read tile-data from Google original data      Coordinate System : WGS84                                |
//|  Google tile-data from level1 cut world map by 2 multiply 2                                                           |    
//|  Level1 have four part and each one of them are 90 degrees multiply 90 degrees                                        |
//|  This reader helper provide an interface to get the tile data bay key "level-rownumber-columennumber" like "13-7-10"  |
//|  Interface named GetTiled(string key),return byte[] list contain img picture resource 256 hight and 256 width         |
//|                                                                                                                       |   
//|  2016.9.12 JHJ                                                                                                        |
//|                                                                                                                       |   
//-------------------------------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Diagnostics;
using System.Windows.Forms;

namespace TileDataTransformTool
{
    public class MongoDBReaderHelper
    {
        /// <summary>
        /// 链接字符串
        /// </summary>
        private string conn = "mongodb://127.0.2.103:27017";
        /// <summary>
        /// 指定的数据库
        /// </summary>
        private string dbName = "Level1-Level14";
        /// <summary>
        /// Mongo客户端
        /// </summary>
        private MongoClient client;
        /// <summary>
        /// 当前操作数据库
        /// </summary>
        protected IMongoDatabase database;
        /// <summary>
        /// Mongo客户端
        /// </summary>
        private MongoClient clientTarget;
        /// <summary>
        /// 当前操作数据库
        /// </summary>
        protected IMongoDatabase databaseTarget;
        /// <summary>
        /// 当前操作的数据库表
        /// </summary>
        protected IMongoCollection<Tiled> collection;
        /// <summary>
        /// 数据库名列表
        /// </summary>
        public List<string> DBNames = new List<string>();
        /// <summary>
        /// 数据库名列表
        /// </summary>
        public List<string> DBNamesTarget = new List<string>();
        /// <summary>
        /// 瓦片文件
        /// </summary>
        protected Tiled Tile = new Tiled();

        MongoServer server;
        MongoServer serverTarget;
        private Dictionary<string, IMongoCollection<Tiled>> CollectionList = new Dictionary<string, IMongoCollection<Tiled>>();
        public bool InitMongoDB(string connectionString, string connectionString2 = null)
        {
            try
            {
                this.conn = connectionString;
                client = new MongoClient(conn);
                server = client.GetServer();
                server.Connect();
                DBNames = server.GetDatabaseNames().ToList();
                if (server.Instance.State != MongoServerState.Connected)
                {
                    //cons .Show("服务器连接失败");
                    Console.WriteLine("mongodb数据库连接失败");
                    return false;
                }

                if (connectionString2 != null)
                {
                    clientTarget = new MongoClient(connectionString2);
                    serverTarget = clientTarget.GetServer();
                    serverTarget.Connect();
                    DBNamesTarget = serverTarget.GetDatabaseNames().ToList();
                    if (serverTarget.Instance.State != MongoServerState.Connected)
                    {
                        //cons .Show("服务器连接失败");
                        Console.WriteLine("mongodb数据库2连接失败");
                        clientTarget = null;
                    }

                }

                //var xxxx = client.GetDatabase("local");
                database = client.GetDatabase(dbName);
                

               
                //database.CreateCollectionAsync("test");
                //xxxx.CreateCollectionAsync("test");
                //var ll = xxxx.GetCollection<Entity>("test");
                //var lx = database.GetCollection<Entity>("test");              
                //Entity ee = new Entity() { Name = "JHJ" };
                //ll.InsertOneAsync(ee);
                //lx.InsertOneAsync(ee);
                //var x = ee.Id;
                //ee.Name = "new";
                //ll.InsertOneAsync(ee);
                collection = database.GetCollection<Tiled>("Titles");//数据库表
                this.CollectionList.Add(dbName, collection);
                Console.WriteLine("mongodb数据库连接成功");
                Console.WriteLine(server.Instance.GetServerDescription().ToString());
            }
            catch (Exception)
            {
                Console.WriteLine("mongodb数据库连接失败");
                return false;
            }
            

            return true;
        }

        public bool GetSourceDBNames()
        {
            if (server != null && server.Instance.State == MongoServerState.Connected)
            {
                DBNames = server.GetDatabaseNames().ToList();
                if (DBNames != null)
                {
                    return true;
                }
                
            }
            return false;
        }

        public bool GetTargetDBNames()
        {
            if (serverTarget != null && serverTarget.Instance.State == MongoServerState.Connected)
            {
                DBNamesTarget = serverTarget.GetDatabaseNames().ToList();
                if (DBNamesTarget != null)
                {
                    return true;
                }

            }
            return false;
        }
        /// <summary>
        /// 获取瓦片
        /// </summary>
        /// <param name="key">Key值：4-6-11</param>
        /// <param name="collection"></param>
        /// <returns>把图片输出到本地</returns>
        protected async Task<byte[]> _GetTiled(string key, IMongoCollection<Tiled> collection)
        {
            if (collection == null) return null;
            var filter = Builders<Tiled>.Filter.Eq("_id", key);
            Task<Tiled> document = collection.Find<Tiled>(filter).FirstOrDefaultAsync();
            Tiled tiled = await document;
            if (tiled == null)
                return null;
            return tiled.ByteImg;
        }


        /// <summary>
        /// 获取瓦片
        /// </summary>
        /// <param name="key">Key值：4-6-11</param>
        /// <param name="collection"></param>
        /// <returns>把图片输出到本地</returns>
        public async Task<byte[]> GetTiled(string key)
        {
            string[] tmplist = key.Split('-');
            if (tmplist.Length != 3)
            {
                Console.WriteLine("Tile关键字不符合要求" + key);
                return null;
            }
            int level = 0;
            int x = 0;
            int y = 0;
            if (!int.TryParse(tmplist[0], out level))
            {
                Console.WriteLine("Tile级别不符合要求" + key);
                return null;
            }
            if (!int.TryParse(tmplist[1], out x))
            {
                Console.WriteLine("X不符合要求" + key);
                return null;
            }
            if (!int.TryParse(tmplist[2], out y))
            {
                Console.WriteLine("Y不符合要求" + key);
                return null;
            }

            return await _GetTiled(key, GetCollection(level, x, y));
        }


        private IMongoCollection<Tiled> GetCollection(int level, int x, int y)
        {
            string dbname = LevelToDBName(level);
            if (dbname == "")
            {
                return null;
            }
            string tablename = LXYToTableName(level, x, y);

            string key = string.Format("{0}_{1}", dbname, tablename);

            //Console.WriteLine(string.Format("{0},{1},{2}-->{3}",level,x,y,key));
            if (!this.CollectionList.ContainsKey(key))
            {
                database = client.GetDatabase(dbname);
                if (database == null)
                {
                    return null;
                }
                collection = database.GetCollection<Tiled>(tablename);//数据库表
                this.CollectionList.Add(key, collection);
            }
            return this.CollectionList[key];
        }

        private string LXYToTableName(int level, int x, int y)
        {
            string ret = "Titles";

            if (level > 15)
            {
                int num = (x - ((int)(Math.Pow(2.0, (double)level) / 4.0))) / ((int)Math.Pow(2.0, 14.0));
                int num2 = y / (int)(Math.Pow(2, 15));

                int num3 = num * (int)Math.Pow(2, level - 15) + num2 + 1;

                ret = string.Format("Titles{0:D2}", num3);

            }
            return ret;
        }

        private string LevelToDBName(int level)
        {
            if (level >= 1 && level <= 14)
            {
                return "Level1-Level14";
            }
            else if (level > 14)
            {
                return string.Format("Level{0}", level);

            }
            return "";
        }

        /// <summary>
        /// Insert tile in partial database
        /// </summary>
        /// <param name="key"></param>
        /// <param name="image"></param>
        public bool InsertTiledToMongoDB(string key, byte[] image)
        {
            if(clientTarget == null)
            {
                return false;
            }
            string[] tmplist = key.Split('-');
            if (tmplist.Length != 3)
            {
                Console.WriteLine("[InsertTiledToMongoDB]Tile关键字不符合要求 " + key);
                return false;
            }
            int level = 0;
            int x = 0;
            int y = 0;
            if (!int.TryParse(tmplist[0], out level))
            {
                Console.WriteLine("[InsertTiledToMongoDB]Tile级别不符合要求 " + key);
                return false;
            }
            if (!int.TryParse(tmplist[1], out x))
            {
                Console.WriteLine("[InsertTiledToMongoDB]X不符合要求 " + key);
                return false;
            }
            if (!int.TryParse(tmplist[2], out y))
            {
                Console.WriteLine("[InsertTiledToMongoDB]Y不符合要求 " + key);
                return false;
            }

            string dbname = LevelToDBName(level);
            if (dbname == "")
            {
                return false;
            }
            dbname += "_Partial";
            string tablename = LXYToTableName(level, x, y);

            string keyy = string.Format("{0}_{1}", dbname, tablename);
            if (!this.CollectionList.ContainsKey(keyy))
            {
                databaseTarget = clientTarget.GetDatabase(dbname);
                if (databaseTarget == null)
                {
                    return false;
                }
                collection = databaseTarget.GetCollection<Tiled>(tablename);//数据库表
                this.CollectionList.Add(keyy, collection);
            }


            Tile.Key = key;
            Tile.ByteImg = image;
            this.CollectionList[keyy].InsertOneAsync(Tile);
            return true;
        }

        public List<string> GetCollectionNames(string DBName)
        {
            List<string> collectionNames = new List<string>();
            if (server.DatabaseExists(DBName))
            {
                MongoDatabase db = server.GetDatabase(DBName);
                collectionNames = db.GetCollectionNames().ToList();
                return collectionNames;
            }
            return null;
        }
    }

    public class Tiled
    {
        /// <summary>
        /// 图片流
        /// </summary>
        public byte[] ByteImg { get; set; }
        [BsonId]
        public string Key { get; set; }
    }

    public class Entity
    {
        public MongoDB.Bson.ObjectId Id { get; set; }
        public string Name { get; set; }
    }
}
