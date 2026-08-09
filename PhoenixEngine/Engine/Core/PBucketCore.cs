using System;
using System.Collections.Generic;
using System.Linq;
using PhoenixEngine.Sequence;
using PhoenixEngine.Unit;

namespace PhoenixEngine.Engine.Core
{
    public class P_Bucket_Core
    {
        public static UnitGroup ConvertToUnitGroup(P_Bucket Bucket, int ID,bool IsLink)
        {
            var Group = new UnitGroup();
            var Units = Bucket.GetUnits();

            Group.Mode = AggregationMode.Aggregation;
            Group.IsLink = IsLink;

            if (Bucket.Head != null)
            {
                Group.Key = ID.ToString();
            }
            else
            {
                Group.Key = Units.Count > 0 ? Units[0].Key : string.Empty;
            }

            Group.Units = new List<BaseUnit>(Units);
            Group.Bucket = Bucket;

            return Group;
        }

        public static int CalcBucketTokenEstimate(P_Bucket Bucket)
        {
            UnitGroup Item = ConvertToUnitGroup(Bucket, 0, Bucket.Type == 1);
            var HtmlItem = new HTMLGenerator().Generate(
                Item.Units,
                Item.IsLink,
                Phoenix.Config.PreserveConversationContext
            );

            if (HtmlItem == null || string.IsNullOrEmpty(HtmlItem.Html))
                return 0;

            double Length = 0;

            foreach (char C in HtmlItem.Html)
            {
                if (C <= 0x7F)
                {
                    // ASCII characters (English + HTML tags)
                    Length += 0.45;
                }
                else if (C >= 0x4E00 && C <= 0x9FFF)
                {
                    // CJK Unified Ideographs
                    Length += 1.8;
                }
                else if (C >= 0x3040 && C <= 0x30FF)
                {
                    // Japanese Hiragana/Katakana
                    Length += 1.8;
                }
                else if (C >= 0xAC00 && C <= 0xD7AF)
                {
                    // Korean Hangul
                    Length += 1.8;
                }
                else if (C >= 0x0400 && C <= 0x04FF)
                {
                    // Cyrillic
                    Length += 1.0;
                }
                else
                {
                    Length += 1.2;
                }
            }

            return (int)Math.Ceiling(Length * 1.10);
        }


    }
}
