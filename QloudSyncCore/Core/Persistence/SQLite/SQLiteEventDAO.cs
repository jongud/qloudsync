using System;
using GreenQloud.Repository;
using GreenQloud.Persistence;
using GreenQloud.Repository.Local;
using GreenQloud.Model;
using System.Collections.Generic;
using GreenQloud.Persistence.SQLite;
using System.Linq;
using System.Data;

namespace GreenQloud.Persistence.SQLite
{
	public class SQLiteEventDAO : EventDAO
	{

        SQLiteRepositoryItemDAO repositoryItemDAO = new SQLiteRepositoryItemDAO();
        #region implemented abstract members of EventDAO

        SQLiteDatabase database = new SQLiteDatabase();

        public override void Create (Event e)
        {
            if (e == null)
                return;
            bool noConflicts = !ExistsConflict(e);

           if (noConflicts){
                try{
                    repositoryItemDAO.Create (e);

                    string dateOfEvent =  e.InsertTime;
                    if(dateOfEvent==null){
                        dateOfEvent = GlobalDateTime.NowUniversalString;
                    }

                    string sql =string.Format("INSERT INTO EVENT (ITEMID, TYPE, REPOSITORY, SYNCHRONIZED, INSERTTIME, USER, APPLICATION, APPLICATION_VERSION, DEVICE_ID, OS, BUCKET, TRY_QNT, RESPONSE) VALUES (\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\", \"{8}\", \"{9}\", \"{10}\", \"{11}\", \"{12}\")", 
                                              e.Item.Id, e.EventType.ToString(), e.RepositoryType.ToString(), bool.FalseString, dateOfEvent, e.User, e.Application, e.ApplicationVersion, e.DeviceId, e.OS, e.Bucket, e.TryQnt, e.Response.ToString());

                    e.Id = (int) database.ExecuteNonQuery (sql, true);

                    Logger.LogEvent("EVENT CREATED", e);
                }catch(Exception err){
                    Logger.LogInfo("ERROR", err);
                }
            }
        }

        public override List<Event> All
        {
            get{
                return Select ("SELECT * FROM EVENT");
            }
        }
        public override List<Event> LastEvents{
            get{
                return Select(string.Format("SELECT * FROM EVENT INNER JOIN RepositoryItem ON RepositoryItemID = ITEMID WHERE SYNCHRONIZED = \"{0}\" AND RESPONSE = \"{1}\"  AND isfolder <> '{2}'" +
                                            "GROUP BY ItemId ORDER BY EventID DESC LIMIT '{3}'  ", bool.TrueString, RESPONSE.OK, bool.TrueString, 5));
            }
        }
        public override Event FindById(int id)
        {
            return Select (string.Format("SELECT * FROM EVENT WHERE EventID = '{0}'", id)).FirstOrDefault();
        }

        public override void UpdateToSynchronized (Event e, RESPONSE response)
        {            
            database.ExecuteNonQuery (string.Format("UPDATE EVENT SET  SYNCHRONIZED = \"{1}\", RESPONSE = \"{2}\" WHERE EventID =\"{0}\"", e.Id, bool.TrueString, response.ToString()));
        }

        public override void IgnoreAllEquals (Event e)
        {            
            database.ExecuteNonQuery (string.Format("UPDATE EVENT SET  SYNCHRONIZED = \"{1}\", RESPONSE = \"{4}\" WHERE ItemId =\"{0}\" AND TYPE = '{2}' AND EventID > '{3}' AND SYNCHRONIZED <> \"{5}\"", e.Item.Id , bool.TrueString , e.EventType, e.Id, RESPONSE.IGNORED.ToString(), bool.TrueString));
        }

        public override void IgnoreAllIfDeleted (Event e)
        {            
            List<Event> list = Select (string.Format("SELECT * FROM EVENT WHERE ItemId =\"{0}\" AND TYPE = '{1}' AND EventID > '{2}'  AND SYNCHRONIZED <> \"{3}\"", e.Item.Id, EventType.DELETE, e.Id, bool.TrueString));
            if(list.Count > 0) { 
                database.ExecuteNonQuery (string.Format("UPDATE EVENT SET  SYNCHRONIZED = \"{0}\", RESPONSE = \"{1}\" WHERE ItemId =\"{2}\" AND EventID < '{3}' ", bool.TrueString, RESPONSE.IGNORED.ToString(), e.Item.Id , list.Last().Id));
                repositoryItemDAO.MarkAsMoved (e.Item);
                if (e.EventType == EventType.CREATE) {
                    database.ExecuteNonQuery (string.Format("UPDATE EVENT SET  SYNCHRONIZED = \"{0}\", RESPONSE = \"{1}\" WHERE EventID = '{2}'", bool.TrueString, RESPONSE.IGNORED.ToString(), list.Last().Id));
                }
            }
        }

        public void CombineMultipleMoves (Event e){
            Event toCombine = null;
            Event combineWith = null;
            if (e.EventType == EventType.MOVE)
                toCombine = e;

            if(e == null) { 
                List<Event> list = Select (string.Format("SELECT * FROM EVENT WHERE ItemId =\"{0}\" AND TYPE = '{1}' AND EventID > '{2}'  AND SYNCHRONIZED <> \"{3}\"", e.Item.Id, EventType.MOVE, e.Id, bool.TrueString));
                if(list.Count > 0) { 
                    toCombine = list.First ();   
                }
            }

            //do while move.hasnext
            //ignore o next
            try {
                if (toCombine != null){
                    List<Event> list2;
                    do {
                        list2 = Select (string.Format("SELECT * FROM EVENT WHERE ItemId =\"{0}\" AND TYPE = '{1}' AND EventID > '{2}'  AND SYNCHRONIZED <> \"{3}\"", toCombine.Item.ResultItemId, EventType.MOVE, e.Id, bool.TrueString));
                        if (list2.Count > 0) { 
                            combineWith = list2.First ();  
                            database.ExecuteNonQuery (string.Format("UPDATE EVENT SET  SYNCHRONIZED = \"{0}\", RESPONSE = \"{1}\" WHERE EventID = '{2}'", bool.TrueString, RESPONSE.IGNORED.ToString(), combineWith.Id));
                            repositoryItemDAO.MarkAsMoved (toCombine.Item.ResultItem);
                            toCombine.Item.ResultItem = combineWith.Item.ResultItem;
                            database.ExecuteNonQuery (string.Format("UPDATE RepositoryItem SET  ResultItemId =\"{0}\" WHERE RepositoryItemID = '{1}'", combineWith.Item.ResultItemId, toCombine.Item.Id));
                        }
                    } while (list2 != null && list2.Count > 0);
                }
            } catch (Exception ex) {
                Logger.LogInfo("ERROR", ex.Message);
            }
        }

        public override void IgnoreAllIfMoved (Event e){
            CombineMultipleMoves (e);
            e = FindById(e.Id);
            List<Event> list = Select (string.Format("SELECT * FROM EVENT WHERE ItemId =\"{0}\" AND TYPE = '{1}' AND EventID > '{2}'  AND SYNCHRONIZED <> \"{3}\"", e.Item.Id, EventType.MOVE, e.Id, bool.TrueString));
            if(list.Count > 0) { 
                if (e.EventType == EventType.CREATE || e.EventType == EventType.UPDATE) {
                    database.ExecuteNonQuery (string.Format("UPDATE EVENT SET  SYNCHRONIZED = \"{0}\", RESPONSE = \"{1}\" WHERE EventID = '{2}'", bool.TrueString, RESPONSE.IGNORED.ToString(), list.First().Id));
                    repositoryItemDAO.MarkAsMoved (e.Item);
                    database.ExecuteNonQuery (string.Format("UPDATE EVENT SET  ItemId =\"{0}\" WHERE EventID = '{1}'", list.First().Item.ResultItem.Id, e.Id));
                }
            }
        }

        public override void UpdateTryQnt (Event e)
        {
            database.ExecuteNonQuery (string.Format("UPDATE EVENT SET  TRY_QNT = \"{0}\" WHERE EventID =\"{1}\"", e.TryQnt, e.Id));
        }

        public override List<Event> EventsNotSynchronized {
            get {
                string sql = string.Format ("SELECT * FROM EVENT WHERE SYNCHRONIZED =\"{0}\" AND INSERTTIME < '{1}' ORDER BY EventID ASC", bool.FalseString, GlobalDateTime.Now.AddSeconds (-10).ToString ("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"));
                List<Event> list = Select (sql);
                return list;
            }
        }

        public override void SetEventType (Event e)
        {
            database.ExecuteNonQuery (string.Format("UPDATE EVENT SET  TYPE = \"{0}\" WHERE EventID =\"{1}\"", e.EventType, e.Id));
        }

        public override void RemoveAllUnsynchronized ()
        {
            List<Event> list = EventsNotSynchronized;
            foreach (Event e in list)
            {
                UpdateToSynchronized (e, RESPONSE.IGNORED);
            }
        }

        
        public override string LastSyncTime{
            get{
                List<Event> events = Select("SELECT * FROM EVENT WHERE REPOSITORY = \"REMOTE\" ORDER BY INSERTTIME DESC LIMIT 1");
                if (events.Count == 0)
                    events = Select("SELECT * FROM EVENT WHERE REPOSITORY = \"LOCAL\" ORDER BY INSERTTIME DESC LIMIT 1");

                string time = null;
                if (events.Count > 0)
                    time = events[0].InsertTime;
                if(time == null)
                    return string.Empty;
                try{

                    DateTime dtime =  Convert.ToDateTime(time);// DateTime.ParseExact(time, "dd/MM/yyyy hh:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                    return dtime.AddSeconds(1).ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                }catch(Exception e )
                {
                    Logger.LogInfo("ERROR", e.Message);
                }
                return DateTime.MaxValue.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
            }
        }

        #endregion

        public bool ExistsConflict (Event e)
        {
            System.Object temp = database.ExecuteScalar (
                            string.Format ("SELECT count(*) FROM EVENT WHERE ItemId =\"{0}\" AND TYPE = '{1}' AND SYNCHRONIZED <> \"{2}\"", e.Item.Id, e.EventType.ToString(), bool.TrueString)
                        );
            int count = int.Parse (temp.ToString());

            if (count > 0) {
                return true;
            }

            return false;
        }

        
        public bool Exists (Event e)
        {
            return All.Count!=0;
        }

        public List<Event> Select (string sql){
            List<Event> events = new List<Event>();
            DataTable dt = database.GetDataTable(sql);
            foreach(DataRow dr in dt.Rows){
                Event e = new Event();
                e.Id = int.Parse (dr[0].ToString());
                e.Item = repositoryItemDAO.GetById (int.Parse (dr[1].ToString()));
                e.EventType = (EventType) Enum.Parse(typeof(EventType), dr[2].ToString());
                e.RepositoryType = (RepositoryType) Enum.Parse(typeof(RepositoryType),dr[3].ToString());
                e.Synchronized = bool.Parse (dr[4].ToString());
                e.InsertTime = dr[5].ToString();
                e.User = dr[6].ToString();
                e.Application = dr[7].ToString();
                e.ApplicationVersion = dr[8].ToString();
                e.DeviceId = dr[9].ToString();
                e.OS = dr[10].ToString();
                e.Bucket = dr[11].ToString();
                e.TryQnt = int.Parse (dr[12].ToString());
                e.Response = (RESPONSE) Enum.Parse(typeof(RESPONSE),dr[13].ToString());
                events.Add (e);
            }
            return events;
        }
  	}   
}

