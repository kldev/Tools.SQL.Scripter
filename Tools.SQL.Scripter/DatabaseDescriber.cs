using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Common = Microsoft.SqlServer.Management.Common;
using Smo = Microsoft.SqlServer.Management.Smo;

namespace Tools.SQL.Scripter {
    public class DatabaseDescriber {
        private DescriberConfig Config { get; set; }
        private ILogger Logger { get; set; }

        public DatabaseDescriber(DescriberConfig config, ILogger<DatabaseDescriber> logger) {
            Config = config;
            Logger = logger;
        }

        public void Execute() {
            var stopwatch = new Stopwatch ( );
            stopwatch.Start ( );
            var srvConnection = new Common.SqlConnectionInfo (Config.Server, Config.User, Config.Password);
            var srv = new Smo.Server (new Common.ServerConnection (srvConnection));

            Logger.LogInformation ($"Describe database {Config.Schema}");
            Logger.LogInformation ($"Output directory {Config.OutputDir}");

            var scrp = new Smo.Scripter (srv);

            scrp.Options.AnsiPadding = false;
            scrp.Options.AppendToFile = true;
            scrp.Options.ContinueScriptingOnError = true;
            scrp.Options.ConvertUserDefinedDataTypesToBaseType = false;
            scrp.Options.WithDependencies = false;
            scrp.Options.IncludeHeaders = false;
            scrp.Options.IncludeIfNotExists = false;
            scrp.Options.SchemaQualify = true;
            scrp.Options.Bindings = false;
            scrp.Options.NoCollation = false;
            scrp.Options.ScriptDrops = false;
            scrp.Options.ScriptSchema = true;
            scrp.Options.ScriptData = false;
            scrp.Options.ScriptBatchTerminator = true;
            scrp.Options.NoCommandTerminator = false;
            scrp.Options.Indexes = true;
            scrp.Options.DriIndexes = true;
            scrp.Options.DriNonClustered = true;
            scrp.Options.NonClusteredIndexes = true;
            scrp.Options.ClusteredIndexes = true;
            scrp.Options.FullTextIndexes = true;
            scrp.Options.ToFileOnly = false;
            scrp.Options.IncludeDatabaseContext = true;
            scrp.Options.AllowSystemObjects = false;
            scrp.Options.SchemaQualify = true;

            foreach (Smo.Database db in srv.Databases) {

                if (!db.Name.ToLower ( ).Equals (Config.Schema.ToLower ( ))) continue;

                if (!Directory.Exists (Config.OutputDir)) {
                    Directory.CreateDirectory (Config.OutputDir);
                }

                var destinationDirectory = Path.Combine (Config.OutputDir, Config.Schema.ToLower ( ));

                if (!Directory.Exists (destinationDirectory)) {
                    Directory.CreateDirectory (destinationDirectory);
                }
                else {
                    Logger.LogInformation ($"Delete old files");
                    DeleteAllFiles (destinationDirectory);
                }

                var urns = new StoreObjectList ( );
                var tableTriggers = new StoreObjectList ( );
                var tableIndexes = new StoreObjectList ( );

                #region tables 

                foreach (Smo.Table table in db.Tables) {
                    urns.AddObject (table);

                    if (table.Triggers.Count > 0) {
                        foreach (Smo.Trigger tableTrigger in table.Triggers) {
                            tableTriggers.AddObject (tableTrigger, true);
                        }
                    }

                    if (table.Indexes.Count > 0) {
                        foreach (Smo.Index tableIndex in table.Indexes) {
                            tableIndexes.AddObject (tableIndex);
                        }
                    }
                }

                WriteData (destinationDirectory, "Tables", scrp, urns);
                WriteData (destinationDirectory, "TableTriggers", scrp, tableTriggers);
                WriteData (destinationDirectory, "TableIndexes", scrp, tableIndexes);

                #endregion

                #region views 

                urns = new StoreObjectList ( );

                foreach (Smo.View view in db.Views) {
                    urns.AddObject (view);
                }

                WriteData (destinationDirectory, "Views", scrp, urns);

                #endregion

                #region views 

                urns = new StoreObjectList ( );

                foreach (Smo.StoredProcedure procedure in db.StoredProcedures) {
                    urns.AddObject (procedure);
                }

                WriteData (destinationDirectory, "StoredProcedures", scrp, urns);

                #endregion

                #region triggers 

                urns = new StoreObjectList ( );

                foreach (Smo.Trigger trigger in db.Triggers) {

                    urns.AddObject (trigger, false);
                }

                WriteData (destinationDirectory, "DatabaseTriggers", scrp, urns);

                #endregion

                #region user_defined function

                urns = new StoreObjectList ( );


                foreach (Smo.UserDefinedFunction userDefinedFunction in db.UserDefinedFunctions) {
                    urns.AddObject (userDefinedFunction);
                }

                WriteData (destinationDirectory, "UserDefinedFunctions", scrp, urns);
                #endregion

                #region user defined types 

                urns = new StoreObjectList ( );

                foreach (Smo.UserDefinedTableType trigger in db.UserDefinedTableTypes) {

                    urns.AddObject (trigger);
                }

                WriteData (destinationDirectory, "UserDefinedTableType", scrp, urns);

                #endregion

            }

            stopwatch.Stop ( );

            Console.WriteLine ("Program executed in: {0:hh\\:mm\\:ss} {1} ms", stopwatch.Elapsed,
                stopwatch.ElapsedMilliseconds);

        }

        private void WriteData(string path, string subDirectory, Smo.Scripter scripter, StoreObjectList urns) {

            Console.WriteLine ($"Describe and save: {subDirectory}");
            var storeObjectPath = Path.Combine (path, subDirectory);
            if (!Directory.Exists (subDirectory)) {
                Directory.CreateDirectory (storeObjectPath);
                DeleteAllFiles (storeObjectPath);
            }

            try {
                foreach (var urn in urns) {

                    Logger.LogInformation ($"Write script for: {urn.Name}.sql");
                    scripter.Options.FileName = Path.Combine (storeObjectPath, urn.Name + ".sql");
                    scripter.Options.AppendToFile = true;

                    scripter.Script (new[] { urn.Urn });
                }

            }
            catch (Exception ex) {
                Logger.LogError ($"Describe object failed: {string.Join (", ", from x in urns select x.ToString ( ))}");
                Logger.LogError ($"Message: {ex.Message}");
                Logger.LogError ($"Stack: {ex.StackTrace}");
            }

        }

        private void DeleteAllFiles(string path) {
            var files = Directory.GetFiles (path, "*.sql");
            if (files.Length > 0) {
                foreach (var file in files) {
                    File.Delete (file);
                }
            }
        }
    }

    public enum StoreObjectType {
        Table,
        View,
        UserDefinedFunction,
        UserDefinedTableType,
        Index,
        TableTrigger,
        DatabaseTrigger,
        StoredProcedure
    }

    public class StoreObject {
        public StoreObject(string name, Urn urn, StoreObjectType type) {
            Name = name;
            Urn = urn;
            Type = type;
        }

        public string Name { get; }
        public Urn Urn { get; }

        public StoreObjectType Type { get; }

    }

    public class StoreObjectList : List<StoreObject> {

        public string[] AllowedSchema = { "dbo" };
        public void AddObject(Smo.StoredProcedure value) {
            if (!AllowedSchema.Contains (value.Schema)) return;
            Add (new StoreObject (value.Name, value.Urn, StoreObjectType.StoredProcedure));
        }

        public void AddObject(Smo.Table value) {
            if (!AllowedSchema.Contains (value.Schema)) return;

            Add (new StoreObject (value.Name, value.Urn, StoreObjectType.Table));
        }

        public void AddObject(Smo.Trigger value, bool table) {

            Add (new StoreObject (value.Name, value.Urn, table ? StoreObjectType.TableTrigger : StoreObjectType.DatabaseTrigger));
        }

        public void AddObject(Smo.UserDefinedFunction value) {
            if (!AllowedSchema.Contains (value.Schema)) return;

            Add (new StoreObject (value.Name, value.Urn, StoreObjectType.UserDefinedFunction));
        }

        public void AddObject(Smo.UserDefinedTableType value) {
            if (!AllowedSchema.Contains (value.Schema)) return;

            Add (new StoreObject (value.Name, value.Urn, StoreObjectType.UserDefinedTableType));
        }

        public void AddObject(Smo.View value) {
            if (!AllowedSchema.Contains (value.Schema)) return;

            Add (new StoreObject (value.Name, value.Urn, StoreObjectType.View));
        }

        public void AddObject(Smo.Index value) {

            Add (new StoreObject (value.Name, value.Urn, StoreObjectType.Index));
        }
    }
}
