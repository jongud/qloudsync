using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Threading;

using GreenQloud.Model;
using GreenQloud.Repository;
using GreenQloud.Util;
using System.Net.Sockets;
using LitS3;
using GreenQloud.Core;
using GreenQloud.Persistence.SQLite;

namespace GreenQloud.Synchrony
{
    
    public enum SyncStatus{
        IDLE,
        UPLOADING,
        DOWNLOADING,
        VERIFING
    }

    public class SynchronizerResolver : AbstractSynchronizer<SynchronizerResolver>
    {
        private SyncStatus status;
        protected SQLiteEventDAO eventDAO;
        protected SQLiteRepositoryItemDAO repositoryItemDAO;
        protected IPhysicalRepositoryController physicalLocalRepository;
        protected RemoteRepositoryController remoteRepository;


        public delegate void SyncStatusChangedHandler (SyncStatus status);
        public event SyncStatusChangedHandler SyncStatusChanged = delegate {};

        public SynchronizerResolver (LocalRepository repo, SynchronizerUnit unit) : base (repo, unit)
        {
            eventDAO = new SQLiteEventDAO(repo);
            repositoryItemDAO = new SQLiteRepositoryItemDAO();
            physicalLocalRepository = new PhysicalRepositoryController (repo);
            remoteRepository = new RemoteRepositoryController(repo);
        }

        public SyncStatus SyncStatus {
            get {
                return status;
            }
            set {
                status = value;
                SyncStatusChanged(status);
            }
        }

        private int eventsToSync;
        public int EventsToSync{
            get { return eventsToSync; }
        }

        //TODO CREATE a query for this
        public string[] Warnings {
            get {
                return new string[0];
            }
        }

        public bool Done {
            set; get;
        }

        public override void Run(){
            while (!Stoped){
                SolveAll();
            }
        }

        public void SolveAll ()
        {
            eventsToSync = eventDAO.EventsToSync.Count;
            while (eventsToSync > 0 && !Stoped)
            {
                Synchronize();
                eventsToSync = eventDAO.EventsToSync.Count;
            }
            SyncStatus = SyncStatus.IDLE;
            Done = true;
            Wait(1000);
        }

        private bool VerifyIgnoreRemote (Event remoteEvent)
        {
            GetObjectResponse meta = null;

            //Ignore events without metadata....
            if(remoteEvent.EventType != EventType.DELETE) {
                if (remoteEvent.HaveResultItem) { 
                    meta = remoteRepository.GetMetadata (remoteEvent.Item.ResultItem.Key, true);
                } else {
                    meta = remoteRepository.GetMetadata (remoteEvent.Item.Key, true);
                }
                if(meta == null){
                    Logger.LogInfo("ERROR ON VERIFY IGNORE REMOTE", "File " + (remoteEvent.HaveResultItem ? remoteEvent.Item.ResultItem.Key : remoteEvent.Item.Key) + " ignored. Metadata not found!");
                    return true;
                }
            }

            return false;
        }
        private bool VerifyIgnoreLocal (Event localEvent)
        {
            return false;
        }
        private bool VerifyIgnore (Event e)
        {
            if (e.Item.Name.Equals (".DS_Store") || e.Item.Name.Equals ("Icon\r") || e.Item.Name.Equals ("Icon"))
                return true;
            if(e.RepositoryType == RepositoryType.REMOTE)
               return VerifyIgnoreRemote (e);
            if(e.RepositoryType == RepositoryType.LOCAL)
                return VerifyIgnoreLocal (e);

            return false;
        }

        void Synchronize(){
            Exception currentException;
            Event e = eventDAO.EventsToSync.FirstOrDefault();
            do {
                currentException = null;
                if (e  != null){
                    try {
                        PerformIgnores (e);
                        if (VerifyIgnore (e)) {
                            eventDAO.UpdateToSynchronized (e, RESPONSE.IGNORED);
                            Logger.LogInfo ("INFO EVENT IGNORE", "Ignore event on " + e.Item.LocalAbsolutePath);
                            return;
                        }

                        //refresh event
                        e = eventDAO.FindById(e.Id);
                        if(e.Synchronized){
                            Logger.LogInfo ("INFO EVENT ALREADY SYNCED", "Event " + e.Id + " already synchronized with response " + e.Response);
                            return;
                        }
                       
                        Logger.LogEvent ("INFO SYNC TRY (try "+(e.TryQnt+1)+")", e );
                        Program.Controller.HandleItemEvent(e);
                        if (e.RepositoryType == RepositoryType.LOCAL) {
                            SyncStatus = SyncStatus.UPLOADING;
                            Program.Controller.HandleSyncStatusChanged ();

                            switch (e.EventType) {
                            case EventType.CREATE: 
                            case EventType.UPDATE:
                                remoteRepository.Upload (e.Item);
                                break;
                            case EventType.DELETE:
                                //Move for versionizing
                                e.Item.BuildResultItem(e.Item.RemoteTrashPath);
                                remoteRepository.Move (e.Item);
                                break;
                            case EventType.MOVE:
                                remoteRepository.Move (e.Item);
                                break;
                            case EventType.COPY:
                                throw new AbortedOperationException("Copy local not implemented");
                            }
                        } else {
                            SyncStatus = SyncStatus.DOWNLOADING;
                            Program.Controller.HandleSyncStatusChanged ();

                            switch (e.EventType) {
                            case EventType.MOVE:
                                physicalLocalRepository.Move (e.Item);
                                break;
                            case EventType.CREATE: 
                            case EventType.UPDATE:
                                remoteRepository.Download (e.Item);
                                break;
                            case EventType.COPY:
                                physicalLocalRepository.Copy (e.Item);
                                break;
                            case EventType.DELETE:
                                physicalLocalRepository.Delete (e.Item);
                                break;
                            }                
                        }
                        
                        VerifySucess (e);

                        if (e.RepositoryType == RepositoryType.LOCAL) {
                            new JSONHelper ().postJSON (e);
                        }
                        eventDAO.UpdateToSynchronized (e, RESPONSE.OK);

                        SyncStatus = SyncStatus.IDLE;
                        Program.Controller.HandleSyncStatusChanged ();

                        Logger.LogEvent ("INFO EVENT SYNCHRONIZING DONE", e);
                    } catch (WebException webx) {
                        Logger.LogInfo("ERROR CONNECTION FAILURE ON SYNCHRONIZING", webx);
                        currentException = webx;
                    } catch (SocketException sock) {
                        Logger.LogInfo("ERROR CONNECTION FAILURE ON SYNCHRONIZING", sock);
                        currentException = sock;
                    } catch (Exception ex) {
                        Logger.LogInfo("ERROR FAILURE ON SYNCHRONIZING", ex);
                        currentException = ex;
                    }

                    //TODO teste

                    e.TryQnt++;
                    eventDAO.UpdateTryQnt (e);
                }

                if(currentException != null){
                    Wait(10000);
                }

            } while (currentException != null && e.TryQnt < 5 && !Stoped);

            if (currentException != null) {
                eventDAO.UpdateToSynchronized(e, RESPONSE.FAILURE);
                throw currentException;
            }
        }

        void PerformIgnores (Event e)
        {   
            eventDAO.IgnoreAllEquals(e);
            eventDAO.IgnoreAllIfDeleted(e);
            eventDAO.IgnoreAllIfMoved(e);
            eventDAO.IgnoreFromIgnordList(e);
        }

        void VerifySucess (Event e)
        {
            SyncStatus = SyncStatus.VERIFING;

            if (e.RepositoryType == RepositoryType.LOCAL){
                switch (e.EventType){
                    case EventType.MOVE:
                        UpdateETag (e);
                        e.Item.Moved = true;
                        repositoryItemDAO.MarkAsMoved (e.Item);
                        break;
                    case EventType.CREATE:
                        UpdateETag (e);
                        break;
                    case EventType.UPDATE:
                        UpdateETag (e);
                        break;
                    case EventType.COPY:
                        UpdateETag (e);
                        break;
                    case EventType.DELETE:
                        e.Item.Moved = true;
                        repositoryItemDAO.MarkAsMoved (e.Item);
                    break;
                }
            }else{
                switch (e.EventType){
                    case EventType.MOVE:
                        UpdateETag (e);
                        e.Item.Moved = true;
                        repositoryItemDAO.MarkAsMoved (e.Item);
                        break;
                    case EventType.CREATE:
                        UpdateETag (e);
                        break;
                    case EventType.UPDATE:
                        UpdateETag (e);
                        break;
                    case EventType.COPY:
                        UpdateETag (e);
                        break;
                    case EventType.DELETE:
                        e.Item.Moved = true;
                        repositoryItemDAO.MarkAsMoved (e.Item);
                    break;
                }                
            }

            e.Item.UpdatedAt = GlobalDateTime.NowUniversalString;
            repositoryItemDAO.ActualizeUpdatedAt (e.Item);
            if(e.HaveResultItem){
                e.Item.ResultItem.UpdatedAt = GlobalDateTime.NowUniversalString;
                repositoryItemDAO.ActualizeUpdatedAt (e.Item.ResultItem);
            }
        }


        void UpdateETag (Event e)
        {
            if (e.HaveResultItem) {
                e.Item.ResultItem.ETag = remoteRepository.RemoteETAG (e.Item.ResultItem);
                e.Item.ResultItem.LocalETag = new Crypto ().md5hash (e.Item.ResultItem);
                if (!e.Item.ResultItem.ETag.Equals (e.Item.ResultItem.LocalETag))
                    throw new QloudSync.VerificationException ();
            } else {
                e.Item.ETag = remoteRepository.RemoteETAG (e.Item);
                e.Item.LocalETag = new Crypto ().md5hash (e.Item);
                if (!e.Item.ETag.Equals (e.Item.LocalETag))
                    throw new QloudSync.VerificationException ();
            }

            repositoryItemDAO.UpdateETAG (e.Item);
        }
    }
}