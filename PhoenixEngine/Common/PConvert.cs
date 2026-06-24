using System;

namespace PhoenixEngine.Common
{
    public class P_Convert
    {
        public static string DateTimeToStr(DateTime Time)
        {
            return Time.ToString("yyyy-MM-dd HH:mm:ss");
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
