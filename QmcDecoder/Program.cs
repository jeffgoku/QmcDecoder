using System;
using System.IO;
using System.Threading.Tasks;

namespace QmcDecoder
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("must supply some input files or directories");
            }

            Parallel.For(0, args.Length, i =>
            {
                var path = args[i];
                var fileAttr = File.GetAttributes(path);
                if ((fileAttr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    Parallel.ForEach(Directory.EnumerateFiles(path, "*.qmc*", SearchOption.AllDirectories), ProcessFile);
                }
                else
                {
                    ProcessFile(path);
                }
            });
        }

        static void ProcessFile(string file)
        {
            var seed = new Seed();

            var ext = Path.GetExtension(file);
            string outfile;

            switch (ext)
            {
                case ".qmcflac":
                    outfile = file.Substring(0, file.Length - ext.Length) + ".flac";
                    break;
                case ".qmcogg":
                    outfile = file.Substring(0, file.Length - ext.Length) + ".ogg";
                    break;
                case ".qmc0":
                case ".qmc3":
                    outfile = file.Substring(0, file.Length - ext.Length) + ".mp3";
                    break;
                default:
                    Console.WriteLine($"Unknown extension : {ext}");
                    return;
            }

            if (File.Exists(outfile))
            {
                Console.WriteLine($"{outfile} already exist");
                return;
            }

            byte[] buffer = new byte[20480];
            using var fs = File.Open(file, FileMode.Open);
            using var outfs = File.Open(outfile, FileMode.Create);

            int n;
            do
            {
                n = fs.Read(buffer, 0, buffer.Length);
                if (n == 0)
                    break;
                for (int i = 0; i < n; ++i)
                {
                    buffer[i] = (byte)(seed.NextMask ^ buffer[i]);
                }
                outfs.Write(buffer, 0, n);
            } while (n == buffer.Length);
        }
    }
}
