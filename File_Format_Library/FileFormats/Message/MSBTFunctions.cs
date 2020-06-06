using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPlugin.FileFormats.Message
{

    public class MSBTFunctions
    {
        public static string[] Article = { "a", "an", "the", "some" };

        public static Dictionary<byte, string> Function = new Dictionary<byte, string>()
        {
            {0x6E, "GetString"},
            {0x5A, "GetNumber"}
        };

        public static Dictionary<short, string> PlayerStringName = new Dictionary<short, string>()
        {
            {0, "..."},
            {1, "PlayerName"},
            {3, "Nickname"},
            {5, "Catchphrase"},
            {8, "OtherIsland"},
            {9, "Island"}
        };

        public static Dictionary<short, string> NumberName = new Dictionary<short, string>()
        {
            {2, "Units"},
            {17, "Bells"},
            {20, "TurnipBuyPrice"},
            {21, "OfferPrice"},
            {26, "BuyPrice"},
            {34, "RelativeYear"},
            {35, "RelativeMonth"},
            {36, "RelativeDay"},
            {37, "RelativeHour"},
            {38, "RelativeMinute"},
            {0x69, "HHAPoints"}
        };

        public static string FunctionName(byte index) => Function.ContainsKey(index) ? Function[index] : "Unknown";
        public static string GetPlayerStringName(short index) => PlayerStringName.ContainsKey(index) ? PlayerStringName[index] : "Unknown";
        public static string GetNumberName(short index) => NumberName.ContainsKey(index) ? NumberName[index] : "Unknown";

        static MSBTFunctions()
        {

        }

        public static byte[] ReplaceFunctions(byte[] array)
        {
            var outArray = new List<byte>();
            for (var i = 0; i < array.Length; i++)
            {
                switch (array[i])
                {
                    case 0x0E: // String?
                        var func = GetFunctionString(array.SubArrayDeepClone(i, 12));
                        var str = Encoding.Unicode.GetBytes(func.Item1);
                        outArray.AddRange(str);
                        i += func.Item2 - 1;
                        break;
                    default:
                        outArray.Add(array[i]);
                        break;
                }
            }
            return outArray.ToArray();
        }

        public static Tuple<string, int> GetFunctionString(byte[] data)
        {
            var val1 = BitConverter.ToInt16(data, 4);
            var val2 = BitConverter.ToInt16(data, 6);
            switch (data[2])
            {
                case 0x5A: // Assorted values
                    return new Tuple<string, int>($"Value({GetNumberName(val1)}, {val2})", 10);
                case 0x6E: // Player info
                    return new Tuple<string, int>($"String({GetPlayerStringName(val1)})", 8);
                case 0x32: // Language article based on STR_Article
                    return new Tuple<string, int>($"Article({data[8]}, {data[9]}, {data[10]})", 12);
                case 0x73: // Other player info
                    return new Tuple<string, int>($"String({data[8]}, {data[9]}, {data[10]})", 12);
                default:
                    break;
            }

            return new Tuple<string, int>($"UnknownFunc{data[2]}({val1}, {val2})", data[8] == 0x0e ? 8 : 10);
        }
    }
}
