using System;
using System.IO;
using LZ4;

namespace ElinExportDump
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)
            {
                Console.Error.WriteLine("Usage: ElinExportDump <compressed-export-path> [output-json-path]");
                return 1;
            }

            try
            {
                string json;
                using (FileStream file = File.OpenRead(args[0]))
                using (LZ4Stream lz4 = new LZ4Stream(file, LZ4StreamMode.Decompress))
                using (StreamReader reader = new StreamReader(lz4))
                {
                    json = reader.ReadToEnd();
                }

                if (args.Length == 2)
                {
                    File.WriteAllText(args[1], json);
                }
                else
                {
                    Console.Write(json);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 2;
            }
        }
    }
}
