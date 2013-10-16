using System;
using System.Collections.Generic;
using GreenQloud.Repository;
using System.Linq;
using System.Xml;
using System.Threading;
using GreenQloud.Model;
using GreenQloud.Repository;
using GreenQloud.Persistence.SQLite;
using System.IO;

 namespace GreenQloud.Synchrony
{
    public class RecoverySynchronizer : AbstractSynchronizer<RecoverySynchronizer>
    {
        private IRemoteRepositoryController remoteRepository;
        private IPhysicalRepositoryController localRepository;
        private SQLiteEventDAO eventDAO;
        private Dictionary<string, Thread> executingThreads;
        private Object lokkThreads = new object();


        public RecoverySynchronizer (LocalRepository repo) : base (repo) {
            remoteRepository = new RemoteRepositoryController (repo);
            localRepository = new PhysicalRepositoryController (repo);
            eventDAO = new SQLiteEventDAO(repo);
            this.executingThreads = new Dictionary<string, Thread>();
        }

        public override void Run() {
            CheckRemoteFolder();
            Synchronize ();
        }

        void CheckRemoteFolder ()
        {
            if (repo.RemoteFolder.Length > 0)
            {
                Event e = new Event(repo);
                RepositoryItem item1 = RepositoryItem.CreateInstance(repo, repo.RemoteFolder);
                e.Item = item1;
                e.RepositoryType = RepositoryType.LOCAL;
                e.EventType = EventType.CREATE;
                eventDAO.Create(e);
                if (remoteRepository.Exists(item1))
                {
                    eventDAO.UpdateToSynchronized(e, RESPONSE.IGNORED);
                }
            }
        }

        private void SolveFromPrefix(string prefix) {
            Thread t = new Thread(delegate()
            {
                List<RepositoryItem> localItems = localRepository.GetItems(Path.Combine(repo.Path, prefix));
                List<RepositoryItem> remoteItems = remoteRepository.GetItems(prefix);
                SolveItems(localItems, remoteItems, prefix);
            });
            lock (lokkThreads)
            {
                if (!this.executingThreads.ContainsKey(prefix))
                {
                    this.executingThreads.Add(prefix, t);
                    t.Start();
                }
            }
            
        }

        public void Synchronize (){
            while (!_stoped){
               string prefix = repo.RemoteFolder;
               SolveFromPrefix(repo.RemoteFolder);
               int count = 0;
               do {
                   Thread.Sleep(5000);
                   Console.WriteLine("Executando com " + executingThreads.Count + " Threads");
                   lock (lokkThreads)
                   {
                       count = executingThreads.Count;
                   }
               } while (!_stoped &&  count > 0) ;
               Thread.Sleep(10000);
               Console.WriteLine("Iniciando de novo! " + executingThreads.Count + " Threads");  
            }
        }

        void SolveItems (List<RepositoryItem> localItems, List<RepositoryItem> remoteItems, string prefix)
        {
            //items exists on remote...
            for (int i = 0; i < remoteItems.Count; i++) {
                RepositoryItem item1 = remoteItems [i];
                Event e = SolveFromRemote (item1);
                localItems.RemoveAll( it => it.Key == item1.Key);
                if (e != null) {
                    if ((e.EventType == EventType.DELETE || e.EventType == EventType.MOVE) && e.Item.IsFolder) {
                        localItems.RemoveAll( it => it.Key.StartsWith(item1.Key));
                        remoteItems.RemoveAll( it => it.Key.StartsWith(item1.Key));
                    }
                    eventDAO.CreateIfNotExistsAny (e);
                }
                if (item1.IsFolder)
                    SolveFromPrefix(item1.Key);
            }

            //Items here is not on remote.... so, it only can be created or removed remote
            for (int i = 0; i < localItems.Count; i++) {
                RepositoryItem item2 = localItems [i];
                Event e = SolveFromLocal (item2);
                if (e != null) {
                    if ((e.EventType == EventType.DELETE || e.EventType == EventType.MOVE) && e.Item.IsFolder) {
                        localItems.RemoveAll( it => it.Key.StartsWith(item2.Key));
                        remoteItems.RemoveAll( it => it.Key.StartsWith(item2.Key));
                    }
                    eventDAO.CreateIfNotExistsAny(e);
                }
                if (item2.IsFolder)
                    SolveFromPrefix(item2.Key);
            }
            lock (lokkThreads)
            {
                this.executingThreads.Remove(prefix);
            }
        }


        private Event SolveFromRemote (RepositoryItem item)
        {
            Event e = new Event (repo);
            e.Item = item;
            if (localRepository.Exists (e.Item)) {
                string actualRemoteEtag = remoteRepository.RemoteETAG (e.Item);
                string actualLocalEtag = new Crypto().md5hash (e.Item);
                string savedEtag = e.Item.ETag;

                if (actualRemoteEtag != actualLocalEtag) {
                    if (savedEtag != actualRemoteEtag && savedEtag == actualLocalEtag) {//changed remote but still the same on local....
                        e.RepositoryType = RepositoryType.REMOTE;
                        e.EventType = EventType.UPDATE;
                    } else if (savedEtag == actualRemoteEtag && savedEtag != actualLocalEtag) {//changed local but still the same on remote....
                        e.RepositoryType = RepositoryType.LOCAL;
                        e.EventType = EventType.UPDATE;
                    } else {
                        Logger.LogInfo ("WARNING", "Recovery Synchronizer found both update local and remote on " + item.Key + " and cannot merge this."); //TODO MAKE A MANUAL MERGE DECISION
                        return null;
                    }
                    return e;
                }
            } else {
                if(e.Item.Id != 0 && (e.Item.UpdatedAt != null && e.Item.Moved == false)){
                    e.RepositoryType = RepositoryType.LOCAL;
                    e.EventType = EventType.DELETE;
                    return e;
                } else {
                    e.RepositoryType = RepositoryType.REMOTE;
                    e.EventType = EventType.CREATE;
                    return e;
                }
            }
            return null;
        }

        private Event SolveFromLocal (RepositoryItem item)
        {
            Event e = new Event (repo);
            e.Item = item;
            if (localRepository.Exists (e.Item)) {
                if (e.Item.UpdatedAt == null) {
                    e.RepositoryType = RepositoryType.LOCAL;
                    e.EventType = EventType.CREATE;
                    return e;
                } else if (!remoteRepository.Exists(e.Item)) {
                    e.RepositoryType = RepositoryType.REMOTE;
                    e.EventType = EventType.DELETE;
                    return e;
                }
            }
            return null;
        }
    }
}


