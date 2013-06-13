using System;
using GreenQloud.Repository.Local;
using GreenQloud.Persistence;
using System.Threading;
using GreenQloud.Persistence.SQLite;
using GreenQloud.Repository;

namespace GreenQloud.Synchrony
{
    public class StorageQloudRemoteEventsSynchronizer : RemoteEventsSynchronizer
    {
        static StorageQloudRemoteEventsSynchronizer instance;

        Thread threadTimer;

        System.Timers.Timer remote_timer;


        public StorageQloudRemoteEventsSynchronizer (LogicalRepositoryController logicalLocalRepository, IPhysicalRepositoryController physicalLocalRepository, 
                                                     RemoteRepositoryController remoteRepository, EventDAO eventDAO, RepositoryItemDAO repositoryItemDAO) :
            base (logicalLocalRepository, physicalLocalRepository, remoteRepository, eventDAO, repositoryItemDAO)
        {
            threadTimer = new Thread( ()=>{
                try{
                    remote_timer =  new System.Timers.Timer () { Interval = GlobalSettings.IntervalBetweenChecksRemoteRepository };        
                    
                    remote_timer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e)=>{
                        base.AddEvents();               
                    };
                    remote_timer.Disposed += (object sender, EventArgs e) => Logger.LogInfo("Synchronizer","Disposing timer.");
                }catch (DisconnectionException)
                {
                    //SyncStatus = SyncStatus.IDLE;
                    Program.Controller.HandleDisconnection();
                }
            });
        }

        public static StorageQloudRemoteEventsSynchronizer GetInstance(){
            if (instance == null)
                instance = new StorageQloudRemoteEventsSynchronizer (new StorageQloudLogicalRepositoryController(), 
                                                                    new StorageQloudPhysicalRepositoryController(),
                                                                    new RemoteRepositoryController(),
                                                                    new SQLiteEventDAO (),
                                                                    new SQLiteRepositoryItemDAO());
            return instance;
        }

        public new void Start ()
        {
            try{
                if(remote_timer==null)
                {   
                    threadTimer.Start();
                    while (remote_timer==null);
                }
                if (!remote_timer.Enabled)
                    remote_timer.Start ();
                base.Start();
            }catch{
                // do nothing
            }           
        }
        

        public ThreadState ControllerStatus{
            get{
                return threadTimer.ThreadState;
            }
        }
    
        protected double Percent {
            set; get;
        }
        
        protected double Speed {
            set; get;
        }
        
        protected double TimeRemaining {
            set; get;
        }

        protected double BytesTransferred {
            set; get;
        }

        protected void ClearDownloadIndexes()
        {
            Percent = 0;
            Speed = 0;
            TimeRemaining = 0;
        }
        
        public double Size {
            set; get;
        }
    }
        
 }

