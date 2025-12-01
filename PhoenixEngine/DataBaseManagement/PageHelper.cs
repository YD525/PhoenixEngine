using System.Data;
using PhoenixEngine.ConvertManager;
using PhoenixEngine.EngineManagement;

namespace PhoenixEngine.DataBaseManagement
{
    public class PageHelper
    {
        public static int GetPageCount(string TableName, string Where)
        {
            int GetCount = ConvertHelper.ObjToInt(Engine.LocalDB.ExecuteScalar(string.Format("Select Count(*) From {0} ", TableName) + Where));
            int PageCount = GetCount / EngineConfig.DefPageSize;
            if (GetCount % EngineConfig.DefPageSize > 0)
            {
                PageCount++;
            }
            return PageCount;
        }

        public static List<Dictionary<string, object>> GetTablePageData(string TableName, int PageNo, int Count, string Where = "")
        {
            if (Where.Trim().Length > 0)
            {
                Where += " ";
            }
            string SqlOrder = string.Format("Select Rowid,* From {0} ", TableName) + Where + string.Format("Order BY Rowid Desc Limit (({0}-1)*{1}),{1};", PageNo, Count);
            return Engine.LocalDB.ExecuteQuery(SqlOrder);
        }

    }

    public class PageItem<T> where T : new()
    {
        public T CurrentPage = new T();
        public int PageNo = 0;
        public int MaxPage = 0;

        public PageItem(T Source, int PageNo, int MaxPage)
        {
            this.CurrentPage = Source;
            this.PageNo = PageNo;
            this.MaxPage = MaxPage;
        }
    }
}
