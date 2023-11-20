using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.IO;

namespace SWOS
{
    class Program
    {
        class BigEndianBinaryReader : BinaryReader
        {
            public BigEndianBinaryReader(Stream s)
                : base(s)
            { }

            public override short ReadInt16()
            {
                byte[] twoBytes = ReadBytes(2);
                Array.Reverse(twoBytes);
                return BitConverter.ToInt16(twoBytes, 0);
            }

            public override int ReadInt32()
            {
                byte[] fourBytes = ReadBytes(4);
                Array.Reverse(fourBytes);
                return BitConverter.ToInt32(fourBytes, 0);
            }
        }



        struct Player
        {
            public readonly static int Passing = 0;
            public readonly static int Shooting = 1;
            public readonly static int Heading = 2;
            public readonly static int Tackling = 3;
            public readonly static int BallControl = 4;
            public readonly static int Speed = 5;
            public readonly static int Finishing = 6;
            public readonly static int NumSkills = 7;

            public string name;
            public string nationality;
            public string positionStr;
            public byte position;
            public byte shirtNum;
            public byte[] skills;
            public string priceText;
            public int price;
        }

        struct Team
        {
            public Player[] players;
            public string name;
            public int nationalityId;
            public string manager;
            public int uniqueId;
            public int tactic;
            public Team(Player[] thePlayers, string teamName, string coach, int nationId, int id, int tacticId)
            {
                players = thePlayers;
                name = teamName;
                nationalityId = nationId;
                uniqueId = id;
                manager = coach;
                tactic = tacticId;
            }
        }

        static readonly string[] g_positions = { "G", "RB", "LB", "D", "RW", "LW", "M", "A" };
        static readonly string[] g_tactics = { 
                                                 "4-4-2", "5-4-1", "4-5-1", "5-3-2", "3-5-2", "4-3-3", "4-2-4", "4-3-3", 
                                                 "Sweep", "5-2-3",  "Attack", "Defend", "User A", "User B", "User C",
                                                 "User D", "User E", "User F"
                                             };

        static IList<int> ParsePrices(string file, out IList<string> pricesText)
        {
            string[] fileLines = File.ReadAllLines(file);
            SortedList<int, int> prices = new SortedList<int, int>(50);
            SortedList<int, string> priceTexts = new SortedList<int, string>(50);
            foreach (string priLine in fileLines)
            {
                if (String.IsNullOrEmpty(priLine))
                {
                    continue;
                }
                string[] parts = priLine.Split(' ');
                int index = Convert.ToInt32(parts[0], 10);
                string priceText = parts[1].Trim();
                int multiplier = (priceText[priceText.Length - 1] == 'K') ? 1000 : 1000000;
                float value = Convert.ToSingle(priceText.Substring(0, priceText.Length - 1));
                int realPrice = (int)(value * multiplier);
                prices.Add(index, realPrice);
                priceTexts.Add(index, priceText);
            }
            pricesText = priceTexts.Values;
            return prices.Values;
        }

        static IList<string> ParseNationalities(string file)
        {
            string[] fileLines = File.ReadAllLines(file);
            SortedList<int, string> nats = new SortedList<int, string>(150);
            foreach (string natLine in fileLines)
            {
                if (String.IsNullOrEmpty(natLine))
                {
                    continue;
                }
                string[] parts = natLine.Split(' ');
                int index = Convert.ToInt32(parts[0], 10);
                string name = parts[1].Trim();
                nats.Add(index, name);
            }
            return nats.Values;
        }

        static void Main(string[] args)
        {
            if((args.Length < 1) || !Directory.Exists(args[0]))
            {
                Console.WriteLine(
                    "Usage: SWOSToCSV swos_data_directory{0}" +
                    "{0}" +
                    "The CSV file will be output into the directory passed as an argument{0}" +
                    "Example{0}" +
                    "-------{0}" +
                    "SWOSToCSV C:\\swos\\disk2\\data",
                    Environment.NewLine
                );
                return;
            }
            Dump(args[0]);
        }

        static string GetExeDir()
        {
            Assembly curAss = Assembly.GetExecutingAssembly();
            Uri curAssLoc = new Uri(curAss.Location);
            string localLoc = curAssLoc.LocalPath;
            return Path.GetDirectoryName(localLoc);
        }

        // reference
        // https://github.com/zlatkok/swospp/blob/master/doc/teams.txt
        static void Dump(string directory)
        {
            string exeDir = GetExeDir();
            IList<string> nationalities = ParseNationalities(Path.Combine(exeDir, "swosnat.txt"));
            IList<string> pricesText;
            IList<int> prices = ParsePrices(Path.Combine(exeDir, "swosprices.txt"), out pricesText);
            string[] files = Directory.GetFiles(directory, "TEAM*");
            using(FileStream csvFile = new FileStream(Path.Combine(directory, "swosdata.csv"), FileMode.Create, FileAccess.Write, FileShare.None))
            using(StreamWriter csv = new StreamWriter(csvFile))
            {
                csv.WriteLine("Raw numbers are the skill values as found in the game files - non-raw are constrained to 0-7 for easier comparison");
                csv.WriteLine("Team,PlayerID,Name,Position,Nationality,RawPassing,RawShooting,RawHeading,RawTackling,RawBallControl,RawSpeed,RawFinishing,Price,Passing,Shooting,Heading,Tackling,BallControl,Speed,Finishing,Overall,PriceWhole");
                foreach (string team in files)
                {
                    Team[] teams;
                    FileStream teamFile = File.OpenRead(team);
                    BinaryReader bebr = BitConverter.IsLittleEndian ? new BigEndianBinaryReader(teamFile) : new BinaryReader(teamFile);
                    using (bebr)
                    {
                        int numTeams = bebr.ReadInt16();
                        teams = new Team[numTeams];
                        for (int i = 0; i < numTeams; ++i)
                        {
                            int countryId = bebr.ReadByte();
                            int index = bebr.ReadByte();
                            Debug.Assert(index == i);
                            int teamId = bebr.ReadInt16();
                            bebr.ReadByte(); // team status, player/computer controlled
                            string teamName = new string(bebr.ReadChars(18)).TrimEnd('\0');
                            Console.WriteLine("Parsing {0} from {1}", teamName, team);
                            bebr.ReadByte(); // unk
                            int tacticId = bebr.ReadByte();
                            int division = bebr.ReadByte();
                            bebr.ReadBytes(5); // home kit type & colour - type, shirt 1, shirt 2, shorts, socks
                            bebr.ReadBytes(5); // away kit type & colour - type, shirt 1, shirt 2, shorts, socks
                            string manager = new string(bebr.ReadChars(23)).TrimEnd('\0');
                            bebr.ReadByte(); // unk
                            byte[] playerIndexes = bebr.ReadBytes(16);
                            Player[] players = new Player[16];
                            int indexesCheck = 0;
                            for (int j = 0; j < 16; ++j)
                            {
                                Player thisPlayer = new Player();
                                byte natByte = bebr.ReadByte();
                                thisPlayer.nationality = nationalities[natByte];
                                bebr.ReadByte(); // unk
                                thisPlayer.shirtNum = bebr.ReadByte();
                                thisPlayer.name = new string(bebr.ReadChars(23)).TrimEnd('\0');
                                // one reference says they're card/injuries then position, another says the other way
                                int position = bebr.ReadByte();
                                position = position >> 5;
                                thisPlayer.positionStr = g_positions[position];
                                thisPlayer.position = (byte)position;
                                bebr.ReadByte(); // career mode cards/injuries
                                thisPlayer.skills = new byte[Player.NumSkills];
                                int passUnkSkill = bebr.ReadByte();
                                thisPlayer.skills[Player.Passing] = (byte)(passUnkSkill & 0xf);
                                int shootHeadSkill = bebr.ReadByte();
                                thisPlayer.skills[Player.Heading] = (byte)(shootHeadSkill & 0xf);
                                thisPlayer.skills[Player.Shooting] = (byte)(shootHeadSkill >> 4);
                                int tackControlSkill = bebr.ReadByte();
                                thisPlayer.skills[Player.BallControl] = (byte)(tackControlSkill & 0xf);
                                thisPlayer.skills[Player.Tackling] = (byte)(tackControlSkill >> 4);
                                int speedFinishSkill = bebr.ReadByte();
                                thisPlayer.skills[Player.Finishing] = (byte)(speedFinishSkill & 0xf);
                                thisPlayer.skills[Player.Speed] = (byte)(speedFinishSkill >> 4);
                                int valueByte = bebr.ReadByte();
                                thisPlayer.priceText = pricesText[valueByte];
                                thisPlayer.price = prices[valueByte];
                                bebr.ReadBytes(5); // unk
                                int playerIndex = thisPlayer.shirtNum - 1;
                                players[playerIndex] = thisPlayer;
                                indexesCheck |= (1 << playerIndex);
                            }
                            Debug.Assert(indexesCheck == 0xffff);
                            teams[i] = new Team(players, teamName, manager, countryId, teamId, tacticId);
                        }
                    }

                    foreach (Team t in teams)
                    {
                        foreach(Player p in t.players)
                        {
                            int normPass = p.skills[Player.Passing] % 8;
                            int normShoot = p.skills[Player.Shooting] % 8;
                            int normHeading = p.skills[Player.Heading] % 8;
                            int normTackling = p.skills[Player.Tackling] % 8;
                            int normControl = p.skills[Player.BallControl] % 8;
                            int normSpeed = p.skills[Player.Speed] % 8;
                            int normFinish = p.skills[Player.Finishing] % 8;
                            int overall = normPass + normShoot + normHeading + normTackling + normControl + normSpeed + normFinish;
                            //Team,PlayerID,Name,Position,Nationality,
                            //RawPassing,RawShooting,RawHeading,RawTackling,RawBallControl,
                            //RawSpeed,RawFinishing,Price,Passing,Shooting,
                            //Heading,Tackling,BallControl,Speed,Finishing,
                            //overall,PriceWhole
                            csv.WriteLine(
                                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21}",
                                t.name, (t.uniqueId << 4) | p.shirtNum, p.name, p.positionStr, p.nationality,
                                p.skills[Player.Passing], p.skills[Player.Shooting], p.skills[Player.Heading], p.skills[Player.Tackling], p.skills[Player.BallControl],
                                p.skills[Player.Speed], p.skills[Player.Finishing], p.priceText, normPass, normShoot, 
                                normHeading, normTackling, normControl, normSpeed, normFinish,
                                overall, p.price
                            );
                        }
                    }
                }
            }
        }
    }
}
