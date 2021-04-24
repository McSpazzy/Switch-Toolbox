using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Toolbox.Library;
using Toolbox.Library.IO;
using Toolbox.Library.Security.Cryptography;

namespace FirstPlugin
{
    public class BCSVParse
    {
        public class Field
        {
            public uint Hash { get; set; }
            public uint Offset { get; set; }

            public object Value;
        }

        public class DataEntry
        {
            public Dictionary<string, object> Fields;
        }

        static Dictionary<uint, string> hashes = new Dictionary<uint, string>();
        static Dictionary<uint, string> mmhashes = new Dictionary<uint, string>();
        static Dictionary<uint, string> overridehashes = new Dictionary<uint, string>();


        public static Dictionary<uint, string> Hashes
        {
            get
            {
                if (hashes.Count == 0)
                    CalculateHashes();
                return hashes;
            }
        }

        public List<DataEntry> Entries = new List<DataEntry>();

        private static bool OnlyHexInString(string test)
        {
            if (test.Length < 5)
            {
                return false;
            }
            // For C-style hex notation (0xFF) you can use @"\A\b(0[xX])?[0-9a-fA-F]+\b\Z"
            return Regex.IsMatch(test, @"\A\b[0-9a-fA-F]+\b\Z");
        }

        public void Read(FileReader reader)
        {
            var headers = new List<string>();
            var valses = new List<string>();


            var capVals = new List<string>();

            var sst = (FileStream) reader.BaseStream;
            
            capVals.Add(sst.Name);

            uint numEntries = reader.ReadUInt32();
            uint entrySize = reader.ReadUInt32();
            ushort numFields = reader.ReadUInt16();
            byte flag1 = reader.ReadByte();
            byte flag2 = reader.ReadByte();
            if (flag1 == 1)
            {
                uint magic = reader.ReadUInt32();
                uint unk = reader.ReadUInt32(); //Always 100000
                reader.ReadUInt32();//0
                reader.ReadUInt32();//0
            }

            Field[] fields = new Field[numFields];
            for (int i = 0; i < numFields; i++)
            {
                fields[i] = new Field()
                {
                    Hash = reader.ReadUInt32(),
                    Offset = reader.ReadUInt32(),
                };
            }
            for (int i = 0; i < numEntries; i++)
            {
                DataEntry entry = new DataEntry();
                Entries.Add(entry);
                entry.Fields = new Dictionary<string, object>();

                long pos = reader.Position;
                for (int f = 0; f < fields.Length; f++)
                {
                    DataType type = DataType.String;
                    uint size = entrySize - fields[f].Offset;
                    if (f < fields.Length - 1)
                    {
                        size = fields[f + 1].Offset - fields[f].Offset;
                    }
                    if (size == 1)
                        type = DataType.Byte;
                    if (size == 2)
                        type = DataType.Int16;
                    if (size == 4)
                        type = DataType.Int32;

                    reader.SeekBegin(pos + fields[f].Offset);
                    object value = 0;
                    string name = fields[f].Hash.ToString("x");

                    string fullName = "";

                    var hashtype = "";

                    if (Hashes.ContainsKey(fields[f].Hash))
                    {
                        fullName = Hashes[fields[f].Hash];
                        name = Hashes[fields[f].Hash].Split(' ')[0];
                        hashtype = Hashes[fields[f].Hash];
                        capVals.Add(fields[f].Hash.ToString("x").PadLeft(8, '0') + " : " + name);

                    }
                    else if (overridehashes.ContainsKey(fields[f].Hash))
                    {
                        headers.Add(fields[f].Hash.ToString("x") + ":00000000");

                        name = overridehashes[fields[f].Hash];

                        capVals.Add(fields[f].Hash.ToString("x").PadLeft(8,'0') + " : " + name);
                    }
                    else
                    {
                        headers.Add(fields[f].Hash.ToString("x") + ":00000000");
                        capVals.Add(name);
                    }

                    if (fullName.Contains("u8"))
                    {
                        type = DataType.Byte;
                    }

                    if (fullName.Contains("s8"))
                    {
                        type = DataType.SByte;
                    }

                    switch (type)
                    {
                        case DataType.Byte:
                            value = reader.ReadByte();
                            break;
                        case DataType.SByte:
                            value = reader.ReadSByte();
                            break;
                        case DataType.Float:
                            value = reader.ReadSingle();
                            break;
                        case DataType.Int16:
                            value = reader.ReadInt16();

                            if ((fullName.Contains(" u16") || fullName.Contains(" u8") || fullName.Contains(" u32")) && !fullName.Contains("Color"))
                            {
                                value = BitConverter.ToUInt16(BitConverter.GetBytes((short)value), 0);
                            }

                            break;
                        case DataType.Int32:
                            value = reader.ReadInt32();

                            var checkVal = BitConverter.ToUInt32(BitConverter.GetBytes((int)value), 0);

                            if (Hashes.ContainsKey(checkVal) && checkVal > 0)
                            {
                                value = Hashes[checkVal];
                                type = DataType.String;
                                break;
                            }

                            if (mmhashes.ContainsKey(checkVal) && checkVal > 0)
                            {
                                value = mmhashes[checkVal];
                                type = DataType.String;
                                break;
                            }

                            

                            if ((name.Contains(".hshCstringRef") || name.Contains(".HashRef") || hashtype.Contains("string")) && checkVal != 0 || name.Contains(".HashRef"))
                            {
                                value = checkVal.ToString("X");
                                type = DataType.String;
                                valses.Add(value.ToString().PadLeft(8, '0') + ":00000000");
                                break;
                            }

                            if (((fullName.Contains(" u16") || fullName.Contains(" u8") || fullName.Contains(" u32")) && !fullName.Contains("Color")) && checkVal != 0)
                            {
                                value = Convert.ToUInt32(value);
                            } else if (fullName.Contains("UniqueID"))
                            {
                                value = Convert.ToUInt32(value);
                            }
                            else
                            {
                                if (IsFloatValue((int)value))
                                {
                                    reader.Seek(-4);
                                    value = reader.ReadSingle();
                                    type = DataType.Float;
                                }
                            }

                            

                            if (value.ToString().Contains("E+") || value.ToString().Contains("E-"))
                            {
                                value = checkVal.ToString("X");
                                type = DataType.String;
                                if (!name.Contains("Color") && !name.Contains("color"))
                                {
                                    valses.Add(value.ToString().PadLeft(8, '0') + ":00000000");
                                }
                            }

                            if (name == "Color")
                            {
                                value = "#" + value.ToString().PadLeft(6, '0');
                            }

                            /*


                                                        if (value.ToString().Length >= 8 && !name.Contains("Depth") && !name.Contains("Height"))
                                                        {
                                                            value = checkVal.ToString("X");
                                                            type = DataType.String;
                                                        }*/

                            break;
                        case DataType.String:
                            value = reader.ReadZeroTerminatedString(Encoding.UTF8);

                            if (!OnlyHexInString(value.ToString()) && !value.ToString().Contains("|"))
                            {
                                break;
                            }

                            var result = "";

                            var spl = value.ToString().Split('|');

                            foreach (var s in spl)
                            {
                                if (!OnlyHexInString(s))
                                {
                                    result += s + "|";
                                    continue;
                                }

                                var sHash = Convert.ToUInt32(s.ToString(), 16);

                                if (Hashes.ContainsKey(sHash) && sHash > 0)
                                {
                                    result += Hashes[sHash] + "|";
                                    continue;
                                }

                                if (mmhashes.ContainsKey(sHash) && sHash > 0)
                                {
                                    result += mmhashes[sHash] + "|";
                                    continue;
                                }
                            }

                            value = result.TrimEnd('|');
                            break;
                    }

                    if (Hashes.ContainsKey(fields[f].Hash))
                    {
                        name = Hashes[fields[f].Hash].Split(' ')[0];
                    }
                    else if (overridehashes.ContainsKey(fields[f].Hash))
                    {
                        name = overridehashes[fields[f].Hash];
                    }
                    else
                    {
                        if (type == DataType.String)
                        {
                            if (size > 4)
                            {
                                name += " string" + size;
                            }
                            else
                            {
                                name += " string";
                            }
                        }
                        else
                        {
                            switch (type)
                            {
                                case DataType.Byte:
                                    name += " u8";
                                    break;
                                case DataType.Int16:
                                    name += " u16";
                                    break;
                                case DataType.Int32:
                                    name += " u32";
                                    break;
                                case DataType.Float:
                                    name += " f32";
                                    break;
                            }
                        }
                    }


                    //   name = fields[f].Hash.ToString("x");

                    //    name = fields[f].Hash.ToString("x");

                    entry.Fields.Add(name.Replace(".hshCstringRef", ""), value);


                }
                reader.SeekBegin(pos + entrySize);
            }

            File.AppendAllLines(@"d:\CapVals.txt", capVals.Distinct());

            File.AppendAllLines(@"d:\Bcsvheaderunknown.txt", headers.Distinct());
                File.AppendAllLines(@"d:\Bcsvvalsunknown.txt", valses.Distinct());

        }

        private bool IsFloatValue(int value)
        {
            return value.ToString().Length > 6;
        }

        public enum DataType
        {
            Byte,
            Int16,
            Int32,
            Int64,
            Float,
            String,
            SInt16,
            SByte,
        }

        public void Write(FileWriter writer)
        {
            writer.Write(Entries.FirstOrDefault().Fields.Count);
        }

        private static void CalculateHashes()
        {
            string dir = Path.Combine(Runtime.ExecutableDir ?? AppDomain.CurrentDomain.BaseDirectory, "Hashes");
            if (!Directory.Exists(dir))
                return;

            foreach (var file in Directory.GetFiles(dir))
            {
                if (Utils.GetExtension(file) != ".txt")
                    continue;

                foreach (string hashStr in File.ReadAllLines(file))
                {
                    CheckHash(hashStr);
                }
            }

            foreach (var file in Directory.GetFiles(dir))
            {
                if (Utils.GetExtension(file) != ".ovr")
                    continue;

                foreach (var hashStr in File.ReadAllLines(file))
                {
                    if (string.IsNullOrEmpty(hashStr))
                    {
                        continue;
                    }

                    var item = hashStr.Split(':');
                    var ind = Convert.ToUInt32(item[0], 16);

                    if (!overridehashes.ContainsKey(ind))
                        overridehashes.Add(ind, item[1]);
                }
            }
        }

        private static void CheckHash(string hashStr)
        {
            uint hash = Crc32.Compute(hashStr);
            if (!hashes.ContainsKey(hash))
                hashes.Add(hash, hashStr);

            uint mmhash = MurMurHash3.Hash(hashStr);
            if (!mmhashes.ContainsKey(mmhash))
                mmhashes.Add(mmhash, hashStr);
        }
    }
}
