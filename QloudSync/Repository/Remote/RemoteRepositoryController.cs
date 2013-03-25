using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.S3.Model;
using GreenQloud.Util;
using GreenQloud.Model;
using GreenQloud.Repository.Local;

namespace GreenQloud.Repository.Remote
{
    public abstract class RemoteRepositoryController : RepositoryController
    {
        protected LogicalRepositoryController logicalController;

        public RemoteRepositoryController (){
        }


        public RemoteRepositoryController (LogicalRepositoryController logicalController)
        {
            CurrentTransfer = new Transfer();
            this.logicalController = logicalController;
        }

        public long FullSizeTransfer {
            get;
            set;
        }

        public Transfer CurrentTransfer{
            set; get;
        }

        public abstract List<RepositoryItem> AllItems {
            get;
        }

        public abstract List<RepositoryItem> Items{
            get;
        }


        public abstract List<RepositoryItem> TrashItems {
            get;
        }
       

        public abstract List<RepositoryItem> GetCopys (RepositoryItem file);

        public abstract TimeSpan DiffClocks{
            get;              
        }

        public static void Authenticate (string username, string password){
        }

        public static string DefaultBucketName {
            get 
            {
                return string.Concat(Credential.Username, GlobalSettings.SuffixNameBucket).ToLower();
            }
        }

        public abstract bool ExistsVersion (RepositoryItem file);
        public abstract Transfer Download (RepositoryItem request);
        public abstract Transfer Upload (RepositoryItem request);
        public abstract Transfer MoveFileToTrash (RepositoryItem request);
        public abstract Transfer MoveFolderToTrash (RepositoryItem folder);
        public abstract Transfer Delete(RepositoryItem request);
        public abstract Transfer SendLocalVersionToTrash (RepositoryItem request);
        public abstract Transfer CreateFolder (RepositoryItem request);
        public abstract Transfer Copy (RepositoryItem source, RepositoryItem destination);
        public abstract bool ExistsFolder (RepositoryItem folder);
        public abstract bool Exists (RepositoryItem sqObject);
        public abstract void DownloadFull (RepositoryItem  file);
        public abstract bool ExistsCopys (RepositoryItem item);
        public abstract List<RepositoryItem> RecentChangedItems {
            get;
            set;
        }
    }
}

