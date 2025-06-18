using System.Drawing;
using System.Web;

// Copyright (C) 2025 YD525
// Licensed under the GNU GPLv3
// See LICENSE for details
//https://github.com/YD525/YDSkyrimToolR/

namespace PhoenixEngine.ConvertManager
{
    public class ConvertHelper
    {
        public static string FileToBase64String(string FilePath)
        {
            FileStream NFileStream = new FileStream(FilePath, FileMode.Open);
            string Base64Str = "";
            try
            {
                NFileStream.Seek(0, SeekOrigin.Begin);
                byte[] Bytes = new byte[NFileStream.Length];
                int Log = Convert.ToInt32(NFileStream.Length);
                NFileStream.Read(Bytes, 0, Log);
                Base64Str = Convert.ToBase64String(Bytes);
                return Base64Str;
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
                Console.ReadLine();
                return Base64Str;
            }
            finally
            {
                NFileStream.Close();
            }
        }
        public static bool Base64StringToFile(string Base64String, string TargetPath,string TargetName)
        {
            bool OPResult = false;
            try
            {
                if (!Directory.Exists(TargetPath))
                {
                    Directory.CreateDirectory(TargetPath);
                }

                string StrBase64 = Base64String.Trim().Substring(Base64String.IndexOf(",") + 1);   //Delete the redundant string before ","
                MemoryStream Stream = new MemoryStream(Convert.FromBase64String(StrBase64));
                FileStream NFileStream = new FileStream(TargetPath + "\\" + TargetName, FileMode.OpenOrCreate, FileAccess.Write);
                byte[] Bytes = Stream.ToArray();
                NFileStream.Write(Bytes, 0, Bytes.Length);
                NFileStream.Close();

                OPResult = true;
            }
            catch (Exception e)
            {
              
            }
            return OPResult;
        }
        public static byte[] Base64ToBytes(string ByteStr)
        {
            byte[] ImageBytes = Convert.FromBase64String(ByteStr);
            MemoryStream Memory = new MemoryStream(ImageBytes, 0, ImageBytes.Length);
            Memory.Write(ImageBytes, 0, ImageBytes.Length);

            return ImageBytes;
        }
        public static string UrlEnCode(string Msg)
        {
            return HttpUtility.UrlEncode(Msg);
        }
        public static string UrlDeCode(string Msg)
        {
            return HttpUtility.UrlDecode(Msg);
        }
        public static double GetRate(double A, double B)
        {
            double Value = (A / B);
            var T1 = Math.Round(Value, 2); 
            return  T1 * 100; 
        }
        public static double RoundDouble(double v, int x)
        {
            return ChinaRound(v, x);
        }
        public static double ChinaRound(double Value, int Decimals)
        {
            if (Value < 0)
            {
                return Math.Round(Value + 5 / Math.Pow(10, Decimals + 1), Decimals, MidpointRounding.AwayFromZero);
            }
            else
            {
                return Math.Round(Value, Decimals, MidpointRounding.AwayFromZero);
            }
        }
        public static string StringDivision(string Message, string Left, string Right)
        {
            if (Message.Contains(Left) && Message.Contains(Right))
            {
                string GetLeftString = Message.Substring(Message.IndexOf(Left) + Left.Length);
                string GetRightString = GetLeftString.Substring(0, GetLeftString.IndexOf(Right));
                return GetRightString;
            }
            else
            {
                return string.Empty;
            }
        }
        public static string GetStringNoEmp(string Message)
        {
            return Message.Replace(" ", "").Replace("    ", "").Replace("　", "");
        }
        public static string ObjToStr(object? Item)
        {
            string GetConvertStr = string.Empty;
            if (Item == null == false)
            {
                GetConvertStr = Item.ToString();
            }
            return GetConvertStr;
        }
        public static int ObjToInt(object Item)
        {
            int Number = -1;
            if (Item == null == false)
            {
                int.TryParse(Item.ToString(), out Number);
            }
            return Number;
        }
        public static double ObjToDouble(object Item)
        {
            double Number = -1;
            if (Item == null == false)
            {
                double.TryParse(Item.ToString(), out Number);
            }
            return Number;
        }
        public static bool ObjToBool(object Item)
        {
            bool Check = false;
            if (Item == null == false)
            {
                Boolean.TryParse(Item.ToString(), out Check);
            }
            return Check;
        }

        public static long ObjToLong(object Item)
        {
            long Number = 0;
            if (Item == null == false)
            {
                long.TryParse(Item.ToString(), out Number);
            }
            return Number;
        }


    }
}
