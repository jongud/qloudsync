using System;
using GreenQloud.Repository.Local;
using GreenQloud.Repository.Remote;
using GreenQloud.Persistence;
using System.Threading;
using GreenQloud.Persistence.SQLite;

namespace GreenQloud.Synchrony
{
    public class StorageQloudBacklogSynchronizer : AbstractBacklogSynchronizer
    {
        Thread threadSync;
        static StorageQloudBacklogSynchronizer instance;

        public StorageQloudBacklogSynchronizer (LogicalRepositoryController logicalLocalRepository, PhysicalRepositoryController physicalLocalRepository, 
                                                RemoteRepositoryController remoteRepository, TransferDAO transferDAO, EventDAO eventDAO) :
            base (logicalLocalRepository, physicalLocalRepository, remoteRepository, transferDAO, eventDAO)
        {
            threadSync = new Thread( ()=>{
                while (Working){
                    try{
                        Synchronize();
                    }catch (DisconnectionException){
                        SyncStatus = SyncStatus.IDLE;
                        Program.Controller.HandleDisconnection();
                    }
                }
            });
        }

        public static StorageQloudBacklogSynchronizer GetInstance(){
            if (instance == null)
                instance = new StorageQloudBacklogSynchronizer (new StorageQloudLogicalRepositoryController(), 
                                                                    new StorageQloudPhysicalRepositoryController(),
                                                                    new StorageQloudRemoteRepositoryController(),
                                                                    new SQLiteTransferDAO (),
                                                                    new SQLiteEventDAO ());
            return instance;
        }

        #region implemented abstract members of Synchronizer

        public override void Start ()
        {
            try{
                Working = true;
                threadSync.Start();
            }catch{
                // do nothing
            }
        }

        public override void Pause ()
        {
            Working = false;
        }

        public override void Stop ()
        {
            Working = false;
            threadSync.Join();
        }

        #endregion

        public ThreadState ControllerStatus{
            get{
                return threadSync.ThreadState;
            }
        }
    }
}
