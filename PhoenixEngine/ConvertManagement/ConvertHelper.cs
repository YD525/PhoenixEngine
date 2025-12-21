using System;

// Copyright (c) 2025 YD525
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//https://github.com/YD525/PhoenixEngine

namespace PhoenixEngine.ConvertManager
{
    public class ConvertHelper
    {
        public static string DateTimeToStr(DateTime Time)
        {
            return Time.ToString("yyyy-MM-dd HH:mm:ss");
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
      
        public static string ObjToStr(object Item)
        {
            string GetConvertStr = "";

            if (Item != null)
            {
                GetConvertStr = Item.ToString();
            }

            return GetConvertStr;
        }
        public static int ObjToInt(object Item)
        {
            int Number = -1;
            if (Item != null)
            {
                int.TryParse(Item.ToString(), out Number);
            }
            return Number;
        }
        public static double ObjToDouble(object Item)
        {
            double Number = -1;
            if (Item != null)
            {
                double.TryParse(Item.ToString(), out Number);
            }
            return Number;
        }
        public static bool ObjToBool(object Item)
        {
            bool Check = false;
            if (Item != null)
            {
                Boolean.TryParse(Item.ToString(), out Check);
            }
            return Check;
        }

        public static long ObjToLong(object Item)
        {
            long Number = -1;
            if (Item != null)
            {
                long.TryParse(Item.ToString(), out Number);
            }
            return Number;
        }
    }
}
