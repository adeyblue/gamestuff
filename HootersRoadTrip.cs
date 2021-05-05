using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameStuff
{
    class HootersRoadTrip
    {
        static byte[] SwapEndianness(byte[] array)
        {
            int len = array.Length;
            for (int i = 0; i < len / 2; ++i)
            {
                byte temp = array[i];
                array[i] = array[len - 1 - i];
                array[len - 1 - i] = temp;
            }
            return array;
        }

        static int ReadBigEndianInt32(BinaryReader br)
        {
            return BitConverter.ToInt32(SwapEndianness(br.ReadBytes(4)), 0);
        }

        static void ExplodeHooters()
        {
            byte[] fileBytes = File.ReadAllBytes(@"I:\game.dat");
            MemoryStream ms = new MemoryStream(fileBytes, false);
            BinaryReader br = new BinaryReader(ms);
            string baseDir = @"C:\Users\Adrian\Downloads\Hooters Road Trip\exploded\";
            br.ReadBytes(4); // header "MFS "
            int headerSize = ReadBigEndianInt32(br);
            int numDirs = ReadBigEndianInt32(br);
            int fileSize = ReadBigEndianInt32(br);
            br.ReadBytes(0x10); // skip
            char[] trimChars = new char[] { '\0' };
            for (int i = 0; i < numDirs; ++i)
            {
                string dirName = new string(br.ReadChars(12));
                dirName = dirName.TrimEnd(trimChars);
                string outDir = Path.Combine(baseDir, dirName);
                Directory.CreateDirectory(outDir);
                br.ReadBytes(4); // unk, always 0x10
                int dirFilesOffset = ReadBigEndianInt32(br);
                int numFiles = ReadBigEndianInt32(br);
                br.ReadBytes(8);
                long nextDirPos = br.BaseStream.Position;
                br.BaseStream.Seek(dirFilesOffset, SeekOrigin.Begin);
                for (int j = 0; j < numFiles; ++j)
                {
                    string fileName = new string(br.ReadChars(12)).TrimEnd(trimChars);
                    string fullPath = Path.Combine(outDir, fileName);
                    br.ReadBytes(4); // unk always 0x10
                    int offset = ReadBigEndianInt32(br);
                    int size = ReadBigEndianInt32(br);
                    br.ReadBytes(4);
                    int crcMaybe = ReadBigEndianInt32(br);
                    byte[] data = new byte[size];
                    Buffer.BlockCopy(fileBytes, offset, data, 0, size);
                    File.WriteAllBytes(fullPath, data);
                }
                br.BaseStream.Seek(nextDirPos, SeekOrigin.Begin);
            }
        }
    }
}
