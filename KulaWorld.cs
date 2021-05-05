using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

// The output Level/Bonus/Hidden files are still zlib compressed
// using inflate() from zlib will decompress them, C# DeflateStream
// can't do it

namespace GameStuff
{
    class KulaWorld
    {
        class FileData
        {
            public int Offset { get; private set; }
            public int CompSize { get; private set; }
            public string Name { get; set; }
            public FileData(int offset, int compLen)
            {
                Offset = offset;
                CompSize = compLen;
            }
        }

        static void ExtractFile(BinaryReader br, string dir, FileData fileData)
        {
            string newFileName = Path.Combine(dir, fileData.Name + ".raw");
            Console.WriteLine("\tExtracting {0} from {1:x}", fileData.Name, fileData.Offset);
            br.BaseStream.Seek(fileData.Offset, SeekOrigin.Begin);
            byte[] compData = br.ReadBytes(fileData.CompSize);
            File.WriteAllBytes(newFileName, compData);
            //MemoryStream ms = new MemoryStream(compData);
            //MemoryStream decompData = new MemoryStream(compData.Length * 2);
            //for (int i = 0; i < 0x100; ++i)
            //{
            //    try
            //    {
            //        ms.Position = i;
            //        using (DeflateStream def = new DeflateStream(ms, CompressionMode.Decompress))
            //        {
            //            byte[] buffer = new byte[0x400];
            //            int read = 0;
            //            while ((read = def.Read(buffer, 0, buffer.Length)) > 0)
            //            {
            //                decompData.Write(buffer, 0, read);
            //            }
            //        }
            //        Console.WriteLine("Successful deoomp starting at offset {0:x}", i);
            //        break;
            //    }
            //    catch (Exception e)
            //    {
            //        ;
            //    }
            //}
            //string decompFileName = Path.Combine(dir, "decomp");
            //Directory.CreateDirectory(decompFileName);
            //decompFileName = Path.Combine(decompFileName, fileData.Name + ".raw.bin");
            //File.WriteAllBytes(decompFileName, decompData.ToArray());
        }

        static void ExtractPak(string pakFile, string baseOutDir)
        {
            string pakName = Path.GetFileName(pakFile);
            string outDir = Path.Combine(baseOutDir, pakName);
            MemoryStream ms = new MemoryStream(File.ReadAllBytes(pakFile));
            using (BinaryReader br = new BinaryReader(ms))
            {
                int numFiles = br.ReadInt32();
                List<FileData> files = new List<FileData>(numFiles);
                for (int i = 0; i < numFiles; ++i)
                {
                    int offset = br.ReadInt32();
                    int compLen = br.ReadInt32();
                    FileData fd = new FileData(offset, compLen);
                    files.Add(fd);
                }
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < numFiles; ++i)
                {
                    int nameOffset = br.ReadInt32();
                    long curPos = br.BaseStream.Position;
                    br.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
                    byte ch = 0;
                    while ((ch = br.ReadByte()) != 0)
                    {
                        sb.Append((char)ch);
                    }
                    br.BaseStream.Seek(curPos, SeekOrigin.Begin);
                    string fileName = sb.ToString().TrimEnd();
                    files[i].Name = fileName;
                    sb.Length = 0;
                }
                Directory.CreateDirectory(outDir);
                foreach (FileData fd in files)
                {
                    ExtractFile(br, outDir, fd);
                }
            }
        }

        static void ExtractFSEntries(string dir, string outDir)
        {
            Console.WriteLine("Processing {0}", Path.GetFileName(dir));
            string[] entries = Directory.GetFileSystemEntries(dir);
            foreach (string entry in entries)
            {
                if (Directory.Exists(entry))
                {
                    ExtractFSEntries(entry, outDir);
                }
                else if(Path.GetExtension(entry).ToUpperInvariant() == ".PAK")
                {
                    ExtractPak(entry, outDir);
                }
            }
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine(
                    "Usage: KulaExtract <InBaseDir> <OutBaseDir>{0}" +
                    "{0}" +
                    "<InBaseDir> needs to be where the system.cnf file is{0}" +
                    "<OutBaseDir> is where the Arctic.pak folders will be created{0}",
                    Environment.NewLine
                );
            }
            ExtractFSEntries(args[0], args[1]);
        }
    }
}
