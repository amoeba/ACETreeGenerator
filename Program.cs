﻿using MySqlConnector;

namespace ACETreeGenerator
{
    internal class Program
    {
        // Configuration you must change
        //
        // Set up your database credentials here
        static string connectionString = "server=127.0.0.1;user=root;password=;database=ace_shard;applicationname=acetreegenerator";
        // Create an account and set it here
        static uint account_id = 2;
        // Create a character on that account and set the id here
        static uint monarch_id = 1342177290;
        // Choose this as you like
        static uint max_depth = 15;

        // Configuration you don't have to change
        static string name_prefix = "Z"; // Names are "{name_prefix}{id}"
        static uint character_level = 1;
        static uint heritage_id = 1; // 1 +. Aluvian
        static uint gender_id = 1; // 1 => Male

        // This just assumes 1400000000 and up are safe to use. This is only true in my testing so YMMV.
        static uint object_id_start = 1400000000;
        static uint current_id = object_id_start;

        static uint GetId()
        {
            return current_id++;
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

        static string QueryForNode(uint id, string name, uint patron_id, uint monarch_id)
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

        static void CreateNode(StringWriter writer, uint depth, uint id, string name, uint patron_id, uint monarch_id)
        {
            if (depth > max_depth)
            {
                return;
            }

            writer.WriteLine(QueryForNode(id, name, patron_id, monarch_id));

            // Create two vassals for this node
            uint id_left = GetId();
            uint id_right = GetId();
            CreateNode(writer, depth + 1, id_left, $"{name_prefix}{id_left}", id, monarch_id);
            CreateNode(writer, depth + 1, id_right, $"{name_prefix}{id_right}", id, monarch_id);
        }

        static void RunMain()
        {
            Console.WriteLine("Creating tree...");

            StringWriter stringWriter = new StringWriter();
            StartTransaction(stringWriter);
            stringWriter.WriteLine(QueryForReset());

            uint id = GetId();
            CreateNode(stringWriter, 1, id, $"{name_prefix}{id}", monarch_id, monarch_id);
            EndTransaction(stringWriter);
            string path = "tree.sql";

            using (StreamWriter streamWriter = new StreamWriter(path))
            {
                stringWriter.Flush(); // Just in case
                streamWriter.Write(stringWriter.ToString());
            }

            Console.WriteLine("...Tree created.");
            Console.WriteLine("Inserting into database...");

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

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
