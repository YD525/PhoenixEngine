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

        //public static int CalcBucketSize(P_Bucket Bucket)
        //{ 
        //    UnitGroup Item = ConvertToUnitGroup(Bucket,0);

        //    Item.GenContent()；
        //}
    }
}
