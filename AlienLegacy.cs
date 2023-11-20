using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GameStuff
{
    class AlienLegacy
    {
        static MemoryStream g_wholeFileWav;
        static byte[] g_clipSpace;
        const string FULL_FILE_NAME = "AlienLegacy.m4a";

        static void CloseInStream(IAsyncResult result)
        {
            Stream s = (Stream)result.AsyncState;
            s.EndWrite(result);
            s.Close();
        }

        static bool FFMPEGMakeWav(string outputFile, byte[] data)
        {
            // can't have pcm_u8 or u16 in a wav file
            //const string COMMAND_LINE_FORMAT = "-f u8 -ac 1 -ar 11025 -i - -c:a pcm_s16le -threads 2 -y \"{0}\"";
            const string COMMAND_LINE_FORMAT = "-f u8 -ac 1 -ar 11025 -i - -c:a aac -b:a 96k -threads 2 -y \"{0}\"";
            string ffmpegPath = Environment.GetEnvironmentVariable("FFMPEG");
            ProcessStartInfo psi = new ProcessStartInfo(
                ffmpegPath,
                String.Format(COMMAND_LINE_FORMAT, outputFile)
            );
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;
            bool success;
            using (Process p = Process.Start(psi))
            {
                int dataLen = data.Length;
                //int cursor = 0;
                //const int chunkSize = 32768;
                Stream stdInStream = p.StandardInput.BaseStream;
                stdInStream.BeginWrite(data, 0, dataLen, CloseInStream, stdInStream);
                /*while (dataLen > 0)
                {
                    int writeSize = Math.Min(chunkSize, dataLen);
                    stdInStream.Write(data, cursor, writeSize);
                    System.Threading.Thread.Sleep(10);
                    dataLen -= writeSize;
                    cursor += writeSize;
                }
                //stdInStream.Write(data, 0, data.Length);
                p.StandardInput.Close();*/
                string ffmpegOutput = p.StandardError.ReadToEnd();
                p.WaitForExit();
                success = (p.ExitCode == 0);
            }
            return success;
        }

        static void DumpAudioFileIndividual(BinaryReader br, int fileLocation, string outputFileName)
        {
            long curPos = br.BaseStream.Position;
            br.BaseStream.Position = fileLocation;
            int fileSize = br.ReadInt32();
            byte[] data = br.ReadBytes(fileSize);
            if(!FFMPEGMakeWav(outputFileName, data))
            {
                Console.WriteLine("Failed to make wav from data at {0:x}", fileLocation);
            }
            br.BaseStream.Position = curPos;
        }

        static void DumpAudioFileWhole(BinaryReader br, int fileLocation)
        {
            long curPos = br.BaseStream.Position;
            br.BaseStream.Position = fileLocation;
            int fileSize = br.ReadInt32();
            byte[] data = br.ReadBytes(fileSize);
            g_wholeFileWav.Write(data, 0, data.Length);
            g_wholeFileWav.Write(g_clipSpace, 0, g_clipSpace.Length);
            br.BaseStream.Position = curPos;
        }

        delegate void AudioSetDumper(BinaryReader br, int fileSetLocation, string outFileStem, ref int outFileCounter);

        static void DumpAudioFileSetsIndividual(BinaryReader br, int fileSetLocation, string outFileStem, ref int outFileCounter)
        {
            long curPos = br.BaseStream.Position;
            br.BaseStream.Position = fileSetLocation;
            int filesInSet = br.ReadInt32();
            for (int i = 0; i < filesInSet; ++i, ++outFileCounter)
            {
                int fileLocation = br.ReadInt32();
                DumpAudioFileIndividual(br, fileLocation, outFileStem + String.Format("-{0}.wav", outFileCounter));
            }
            br.BaseStream.Position = curPos;
        }

        static void DumpAudioFileSetsWhole(BinaryReader br, int fileSetLocation, string outFileStem, ref int outFileCounter)
        {
            long curPos = br.BaseStream.Position;
            br.BaseStream.Position = fileSetLocation;
            int filesInSet = br.ReadInt32();
            for (int i = 0; i < filesInSet; ++i, ++outFileCounter)
            {
                int fileLocation = br.ReadInt32();
                DumpAudioFileWhole(br, fileLocation);
            }
            br.BaseStream.Position = curPos;
        }

        static void ProcessFile(string inFile, string outFileStem, bool separateFiles)
        {
            using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(inFile)))
            using (BinaryReader br = new BinaryReader(ms))
            {
                int numSections = br.ReadInt32();
                List<int> sectionOffsets = new List<int>(numSections);
                for (int i = 0; i < numSections; ++i)
                {
                    int offset = br.ReadInt32();
                    if (offset != 0)
                    {
                        sectionOffsets.Add(offset);
                    }
                }
                AudioSetDumper dumper = separateFiles ? new AudioSetDumper(DumpAudioFileSetsIndividual) : new AudioSetDumper(DumpAudioFileSetsWhole);
                int outFileCounter = 1;
                foreach (int sectionOffset in sectionOffsets)
                {
                    Console.WriteLine("--- section at {0:x}", sectionOffset);
                    br.BaseStream.Position = sectionOffset;
                    int fileSetsInFirstGroup = br.ReadInt32();
                    for (int i = 0; i < fileSetsInFirstGroup; ++i, ++outFileCounter)
                    {
                        int fileSetLocation = br.ReadInt32();
                        dumper(br, fileSetLocation, outFileStem, ref outFileCounter);
                    }
                    int secondGroupFileSets = br.ReadInt32();
                    for (int i = 0; i < secondGroupFileSets; ++i, ++outFileCounter)
                    {
                        int fileSetLocation = br.ReadInt32();
                        dumper(br, fileSetLocation, outFileStem, ref outFileCounter);
                    }
                }
            }
        }

        static void ProcessDir(string inDir, string outDir, bool separateFiles)
        {
            string[] dvfFiles = Directory.GetFiles(inDir, "*.dvf");
            foreach (string dvf in dvfFiles)
            {
                string filename = Path.GetFileNameWithoutExtension(dvf);
                Console.WriteLine("Processing {0}", filename);
                ProcessFile(dvf, Path.Combine(outDir, filename), separateFiles);
            }
            if(!separateFiles)
            {
                string file = Path.Combine(outDir, FULL_FILE_NAME);
                Console.WriteLine("Creating {0}...", file);
                if(!FFMPEGMakeWav(file, g_wholeFileWav.ToArray()))
                {
                    Console.WriteLine("Failed to make {0} from whole data", FULL_FILE_NAME);
                }
            }
        }

        static void Main(string[] args)
        {
            if ((args.Length < 2) || (!(Directory.Exists(args[0])) && (Directory.Exists(args[1]))))
            {
                Console.WriteLine(
                    "Usage: ALWavExtract <ALDir> <OutDir> <IndividualFiles>{0}" +
                    "{0}" +
                    "<ALDir> needs to be where the Alien Legacy files (like ADVICE.DVF) are{0}" +
                    "<OutDir> is where to save. If <IndividualFiles> is not set, the wav is named AlienLegacy.wav{0}" +
                    "<IndividualFiles> is 1 to create a wav for each sound file, or 0 for one big wav{0}",
                    Environment.NewLine
                );
                return;
            }
            int individualFiles = 0;
            if (args.Length >= 3)
            {
                Int32.TryParse(args[2], out individualFiles);
            }
            bool separateFiles = (individualFiles != 0);
            if(!separateFiles)
            {
                // the whole thing is about 700MB with spaces
                g_wholeFileWav = new MemoryStream(700 * 1024 * 1024);
                // 11025 bytes is one second of silence at 11025hz u8
                // we want about 1/3 of a second 
                g_clipSpace = new byte[3450];
                for(int i = 0; i < 3450; ++i)
                {
                    g_clipSpace[i] = 0x80;
                }
            }
            ProcessDir(args[0], args[1], separateFiles);
        }
    }
}
