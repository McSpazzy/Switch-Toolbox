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

        public static Dictionary<short, string> StringName = new Dictionary<short, string>()
        {
            {1, "Name"},
            {9, "Island"}
        };

        public static string FunctionName(byte index) => Function.ContainsKey(index) ? Function[index] : "Unknown";
        public static string GetStringName(short index) => StringName.ContainsKey(index) ? StringName[index] : "Unknown";

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
                case 0x5A: // GetNumber
                    return new Tuple<string, int>($"GetNumber({val1}, {val2})", 10);
                case 0x6E: // GetString
                    return new Tuple<string, int>($"GetString({GetStringName(val1)})", 8);
                case 0x32: // GetArticle
                    return new Tuple<string, int>($"GetArticle({data[8]}, {data[9]}, {data[10]})", 12);
                default:
                    break;
            }

            return new Tuple<string, int>($"UnknownFunc{data[2]}({val1}, {val2})", data[8] == 0x0e ? 8 : 10);
        }
    }
}
