/*
 * 
 * Welcome to this template plugin.
 * Level: All Levels
 * Purpose: To get a working model for new plugins to be built off.  This plugin will
 * compile immediately, all you have to do is rename TemplatePlugin to reflect 
 * the purpose of the plugin.
 * 
 * You may need to delete the references to TerrariaServer and TShockAPI.  They 
 * could be pointing to my current folder.  Just remove them and then right-click the
 * references folder, go to browse go to the dll folder, and select both.
 * 
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using Terraria;

namespace NPCBlocker
{
    [ApiVersion(1, 15)]
    public class NPCBlocker : TerrariaPlugin
    {
        private IDbConnection db;
        private List<int> blockedNPC = new List<int>();
        public override Version Version
        {
            get { return new Version(1,10); }
        }

        public override string Name
        {
            get { return "NPC Blocker"; }
        }

        public override string Author
        {
            get { return "Zack Piispanen"; }
        }

        public override string Description
        {
            get { return "Blocks npcs from being spawned."; }
        }

        public NPCBlocker(Main game)
            : base(game)
        {
            Order = 4;
        }

        public override void Initialize()
        {
            TShockAPI.Commands.ChatCommands.Add(new Command("resnpc", AddNpc, "blacknpc"));
            TShockAPI.Commands.ChatCommands.Add(new Command("resnpc", DelNpc, "whitenpc"));
            ServerApi.Hooks.NpcSpawn.Register(this, OnSpawn);
            StartDB();
        }

        public void StartDB()
        {
            SetupDb();
            ReadDb();
        }

        private void SetupDb()
        {
            if (TShock.Config.StorageType.ToLower() == "sqlite")
            {
                string sql = Path.Combine(TShock.SavePath, "npc_blocker.sqlite");
                db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
            }
            else if (TShock.Config.StorageType.ToLower() == "mysql")
            {
                try
                {
                    var hostport = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection();
                    db.ConnectionString =
                        String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                                      hostport[0],
                                      hostport.Length > 1 ? hostport[1] : "3306",
                                      TShock.Config.MySqlDbName,
                                      TShock.Config.MySqlUsername,
                                      TShock.Config.MySqlPassword
                            );
                }
                catch (MySqlException ex)
                {
                    Log.Error(ex.ToString());
                    throw new Exception("MySql not setup correctly");
                }
            }
            else
            {
                throw new Exception("Invalid storage type");
            }
            
            var table2 = new SqlTable("Blocked_NPC",
                                     new SqlColumn("ID", MySqlDbType.Int32)
                );
            var creator2 = new SqlTableCreator(db,
                                              db.GetSqlType() == SqlType.Sqlite
                                                ? (IQueryBuilder)new SqliteQueryCreator()
                                                : new MysqlQueryCreator());
            creator2.EnsureExists(table2);
        }

        private void ReadDb()
        {
            String query = "SELECT * FROM Blocked_NPC";

            var reader = db.QueryReader(query);
	
			while (reader.Read())
			{
                blockedNPC.Add(reader.Get<int>("ID"));
			}
        }

        private void AddNpc(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("You must specify a npc id to add.", Color.Red);
                return;
            }
            string tile = args.Parameters[0];
            int id;
            if (!int.TryParse(tile, out id))
            {
                args.Player.SendMessage(String.Format("Npc id '{0}' is not a valid number.", id), Color.Red);
                return;
            }

            String query = "INSERT INTO Blocked_NPC (ID) VALUES (@0);";

            if (db.Query(query, id) != 1)
            {
                Log.ConsoleError("Inserting into the database has failed!");
                args.Player.SendMessage(String.Format("Inserting into the database has failed!", id), Color.Red);
            }
            else
            {
                args.Player.SendMessage(String.Format("Successfully banned {0}", id), Color.Red);
                blockedNPC.Add(id);
            }
        }

        private void DelNpc(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("You must specify a npc id to remove.", Color.Red);
                return;
            }
            string tile = args.Parameters[0];
            int id;
            if (!int.TryParse(tile, out id))
            {
                args.Player.SendMessage(String.Format("Npc id '{0}' is not a valid number.", id), Color.Red);
                return;
            }
            String query = "DELETE FROM Blocked_NPC WHERE ID = @0;";

            if (db.Query(query, id) != 1)
            {
                Log.ConsoleError("Removing from the database has failed!");
                args.Player.SendMessage(String.Format("Removing from the database has failed!  Are you sure {0} is banned?", id), Color.Red);
            }
            else
            {
                args.Player.SendMessage(String.Format("Successfully unbanned {0}", id), Color.Green);
                blockedNPC.Remove(id);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
				ServerApi.Hooks.NpcSpawn.Deregister(this, OnSpawn);
            }

            base.Dispose(disposing);
        }

        private void OnSpawn( NpcSpawnEventArgs args)
        {
            if (args.Handled)
                return;
            if (blockedNPC.Contains(args.Npc.netID))
            {
                args.Handled = true;
                return;
            }
        }
    }
}
