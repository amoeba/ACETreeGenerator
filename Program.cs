using System;
using System.Collections.Generic;
using System.Xml.Linq;
using MySqlConnector;

namespace ACETreeGenerator
{
    internal class Program
    {
        // Configuration you must change
        //
        // Set up your database credentials here
        static string connectionString = "Server=127.0.0.1;User ID=root;Database=ace_shard";
        // Create an account and set it here
        static uint account_id = 2;
        // Create a character on that account and set the id here
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

        static uint GetId()
        {
            return current_id++;
        }

        static void ResetOurWork(MySqlConnection connection)
        {
            Console.WriteLine("Reset");

            string query = @$"
                delete from ace_shard.character where id >= {object_id_start};
                delete from ace_shard.biota where id >= {object_id_start};
                delete from ace_shard.biota_properties_i_i_d where Object_Id >= {object_id_start};
                ";

            try
            {
                var insert_character = new MySqlCommand(query, connection).ExecuteNonQuery();
                Console.WriteLine($"Deleted {insert_character} rows.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void CreateNode(MySqlConnection connection, uint depth, uint id, string name, uint patron_id, uint monarch_id)
        {
            // This is the stop condition and terminates all recursion
            if (depth > max_depth)
            {
                Console.WriteLine($"Quitting due to hitting max_depth of {max_depth};");
                return;
            }

            Console.WriteLine($"CreateNode at depth {depth} for {id}, patron {patron_id}");

            string query = @$"
                insert into ace_shard.character  (id, account_Id, name, is_Plussed, is_Deleted) values ('{id}', '{account_id}', '{name}', 0, 0);
                insert into ace_shard.biota (id, weenie_Class_Id, weenie_Type) values ('{id}', '1', '10');

                insert into ace_shard.biota_properties_string (object_Id, type, value) values ('{id}', '1', '{name}');
                insert into ace_shard.biota_properties_int (object_Id, type, value) values ('{id}', '188', '{heritage_id}');
                insert into ace_shard.biota_properties_int (object_Id, type, value) values ('{id}', '113', '{gender_id}');
                insert into ace_shard.biota_properties_int (object_Id, type, value) values ('{id}', '25', '{character_level}');

                insert into ace_shard.biota_properties_i_i_d (object_Id, type, value) values ({id}, 25, {patron_id});
                insert into ace_shard.biota_properties_i_i_d (object_Id, type, value) values ({id}, 26, {monarch_id});
                ";

            try
            {
                var insert_character = new MySqlCommand(query, connection).ExecuteNonQuery();
                Console.WriteLine($"Inserted {insert_character} rows;");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            // Create two vassals for this node
            uint id_left = GetId();
            uint id_right = GetId();
            CreateNode(connection, depth + 1, id_left, $"{name_prefix}{id_left}", id, monarch_id);
            CreateNode(connection, depth + 1, id_right, $"{name_prefix}{id_right}", id, monarch_id);
        }

        static void Run()
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            // We delete everything we did on any previous run
            ResetOurWork(connection);

            // This recurses up to max_depth
            uint id = GetId();
            CreateNode(connection, 1, id, $"{name_prefix}{id}", monarch_id, monarch_id);

            Console.WriteLine("Done!");
        }

        static void Main(string[] args)
        {
            try
            {
                Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
