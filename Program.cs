﻿using System.IO;
using MySqlConnector;

namespace ACETreeGenerator
{
    internal class Program
    {
        // Configuration you must change
        //
        // Set up your database credentials here
        static string connectionString = "server=127.0.0.1;user=root;password=;database=ace_shard;applicationname=acetreegenerator";

        // Pick an account that monarch_id (below) is a member of
        static uint account_id = 1;

        // Pick a character to be the monarch
        //
        // The main reason we require this is because creating a character from scratch
        // that you can also log in as takes a lot more SQL than I want to write
        static uint monarch_id = 1342177290;

        // Choose this as you like
        static uint max_depth = 10;

        // Configuration you don't have to change
        static string name_prefix = "Z"; // Names are "{name_prefix}{id}"
        static uint character_level = 1;
        static uint heritage_id = 1; // 1 +. Aluvian
        static uint gender_id = 1; // 1 => Male

        // This just assumes 1400000000 and up are safe to use. This is only true in my testing so YMMV.
        static uint object_id_start = 1400000000;
        static uint current_id = object_id_start;

        static uint NextId()
        {
            return ++current_id;
        }

        static void StartTransaction(StringWriter writer)
        {
            writer.WriteLine("START TRANSACTION;");
        }

        static void EndTransaction(StringWriter writer)
        {
            writer.WriteLine("COMMIT;");
        }

        static string QueryForReset()
        {
            return @$"DELETE FROM ace_shard.character WHERE id >= {object_id_start};
DELETE FROM ace_shard.biota WHERE id >= {object_id_start};
DELETE FROM ace_shard.biota_properties_string WHERE Object_Id >= {object_id_start};
DELETE FROM ace_shard.biota_properties_int WHERE Object_Id >= {object_id_start};
DELETE FROM ace_shard.biota_properties_i_i_d WHERE Object_Id >= {object_id_start};";
        }

        static string QueryForNode(uint account_id, uint id, string name, uint patron_id, uint monarch_id)
        {
            return @$"INSERT INTO ace_shard.character (id, account_Id, name, is_Plussed, is_Deleted) VALUES ('{id}', '{account_id}', '{name}', 0, 0);
INSERT INTO  ace_shard.biota (id, weenie_Class_Id, weenie_Type) VALUES ('{id}', '1', '10');
INSERT INTO  ace_shard.biota_properties_string (object_Id, type, value) VALUES ('{id}', '1', '{name}');
INSERT INTO  ace_shard.biota_properties_int (object_Id, type, value) VALUES ('{id}', '188', '{heritage_id}');
INSERT INTO  ace_shard.biota_properties_int (object_Id, type, value) VALUES ('{id}', '113', '{gender_id}');
INSERT INTO  ace_shard.biota_properties_int (object_Id, type, value) VALUES ('{id}', '25', '{character_level}');
INSERT INTO  ace_shard.biota_properties_i_i_d (object_Id, type, value) VALUES ({id}, 25, {patron_id});
INSERT INTO  ace_shard.biota_properties_i_i_d (object_Id, type, value) VALUES ({id}, 26, {monarch_id});";
        }

        static void ResetTree(StringWriter writer)
        {
            writer.WriteLine(QueryForReset());
        }

        static void CreateTree(StringWriter writer, uint account_id, uint monarch_id)
        {
            CreateNode(writer, 2, account_id, monarch_id, monarch_id);
            CreateNode(writer, 2, account_id, monarch_id, monarch_id);
        }
        static void CreateNode(StringWriter writer, uint depth, uint account_id, uint patron_id, uint monarch_id)
        {
            if (depth > max_depth)
            {
                return;
            }

            // Determine next ID and name
            uint id = NextId();
            string name = $"{name_prefix}{id}";

            // Create current node (character)
            writer.WriteLine(QueryForNode(account_id, id, name, patron_id, monarch_id));

            // Create two vassals for this node
            CreateNode(writer, depth + 1, account_id, id, monarch_id);
            CreateNode(writer, depth + 1, account_id, id, monarch_id);
        }
        static uint GetAccount(MySqlConnection connection)
        {
            try
            {
                using (var cmd = new MySqlCommand("SELECT accountId, accountName FROM ace_auth.account ORDER BY accountId ASC LIMIT 1;", connection))
                {
                    var reader = cmd.ExecuteReader();
                    reader.Read();

                    uint id = (uint)reader["accountId"];
                    var name = reader["accountName"];
                    Console.WriteLine($"Found account {id} with name {name}.");
                    reader.Close();
                    return id;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get account id due to error: {ex.Message}");
                return 0;
            }
        }

        static void RunMain()
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                StringWriter stringWriter = new StringWriter();

                // Generate the SQL to make the tree
                Console.WriteLine("1/2: Creating tree...");
                StartTransaction(stringWriter);
                ResetTree(stringWriter);
                CreateTree(stringWriter, account_id, monarch_id);
                EndTransaction(stringWriter);
                Console.WriteLine("...Tree created.");

                // Write SQL file just for debugging
                string path = "tree.sql";

                using (StreamWriter streamWriter = new StreamWriter(path))
                {
                    stringWriter.Flush(); // Just in case
                    streamWriter.Write(stringWriter.ToString());
                }

                Console.WriteLine("3/3: Inserting into database...");

                using (MySqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new MySqlCommand(stringWriter.ToString(), connection, transaction))
                        {
                            var rows = cmd.ExecuteNonQuery();
                            Console.WriteLine($"Insert query affected {rows} rows.");
                        }
                    }
                    catch (MySqlException ex)
                    {
                        Console.WriteLine($"Error executing query: {ex.Message}");
                        throw;
                    }

                    transaction.Commit();
                }
            }

            Console.WriteLine("Done!");
        }

        static void Main(string[] args)
        {
            try
            {
                RunMain();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
