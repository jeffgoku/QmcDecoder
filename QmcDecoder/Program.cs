using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace QmcDecoder;

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
                Parallel.ForEach(Directory.EnumerateFiles(path).Where(HaveValidExt), ProcessFile);
            }
            else
            {
                if (!HaveValidExt(path))
                    return;
                ProcessFile(path);
            }
        });
    }

    static bool HaveValidExt(string file)
    {
        var idx = file.LastIndexOf('.');
        if (idx == -1)
            return false;

        var ext = file.AsSpan(idx);
        if (ext.CompareTo(".qmcflac", StringComparison.Ordinal) == 0)
        {
            return true;
        }
        else if (ext.CompareTo(".qmcogg", StringComparison.Ordinal) == 0)
        {
            return true;
        }
        else if (ext.CompareTo(".qmc0", StringComparison.Ordinal) == 0)
        {
            return true;
        }
        else if (ext.CompareTo(".qmc3", StringComparison.Ordinal) == 0)
        {
            return true;
        }
        else if (ext.CompareTo(".mflac", StringComparison.Ordinal) == 0)
        {
            return true;
        }
        else if (ext.CompareTo(".mogg", StringComparison.Ordinal) == 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    static void ProcessFile(string file)
    {
        var ext = Path.GetExtension(file);
        string outfile;

        switch (ext)
        {
            case ".qmcflac":
            case ".mflac":
                outfile = string.Concat(file.AsSpan(0, file.Length - ext.Length), ".flac");
                break;
            case ".qmcogg":
            case ".mogg":
                outfile = string.Concat(file.AsSpan(0, file.Length - ext.Length), ".ogg");
                break;
            case ".qmc0":
            case ".qmc3":
                outfile = string.Concat(file.AsSpan(0, file.Length - ext.Length), ".mp3");
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
        try
        {
            using var outfs = File.Open(outfile, FileMode.Create);

            var decoder = new QmcDecoder2(fs);
            int n;
            do
            {
                n = fs.Read(buffer, 0, buffer.Length);
                if (n == 0)
                    break;

                decoder.Decode(new Span<byte>(buffer, 0, n));
                outfs.Write(buffer, 0, n);
            } while (n == buffer.Length);
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.ToString());
            File.Delete(outfile);
        }
    }
}